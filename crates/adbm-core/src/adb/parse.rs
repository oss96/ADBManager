//! Pure parsers for adb / device output. Kept free of I/O so they are unit
//! tested against captured real-world output.

use crate::api::{DeviceInfo, DeviceState, Package};

#[derive(Debug, Clone, PartialEq)]
pub struct AdbDeviceLine {
    pub serial: String,
    pub state: DeviceState,
    pub product: Option<String>,
    pub model: Option<String>,
    pub device: Option<String>,
    pub transport_id: Option<u64>,
}

/// Parse `devices -l` / `track-devices-l` output.
///
/// ```text
/// 3A281FDJH00B7C         device usb:1-1 product:husky model:Pixel_8_Pro device:husky transport_id:7
/// ZY22J4KQ9P             unauthorized usb:1-2 transport_id:9
/// 0123456789ABCDEF       no permissions (missing udev rules?); see [http://...] usb:1-3 transport_id:3
/// ```
pub fn parse_device_list(text: &str) -> Vec<AdbDeviceLine> {
    let mut out = Vec::new();
    for line in text.lines() {
        let line = line.trim();
        if line.is_empty() || line.starts_with("List of devices") || line.starts_with('*') {
            continue;
        }
        let mut tokens = line.split_whitespace();
        let Some(serial) = tokens.next() else { continue };
        let Some(state_word) = tokens.next() else { continue };
        let mut d = AdbDeviceLine {
            serial: serial.to_string(),
            state: DeviceState::from_adb(state_word),
            product: None,
            model: None,
            device: None,
            transport_id: None,
        };
        for t in tokens {
            let Some((k, v)) = t.split_once(':') else { continue };
            match k {
                "product" => d.product = Some(v.to_string()),
                "model" => d.model = Some(v.replace('_', " ")),
                "device" => d.device = Some(v.to_string()),
                "transport_id" => d.transport_id = v.parse().ok(),
                _ => {}
            }
        }
        out.push(d);
    }
    out
}

/// The shell command whose output `parse_info` understands. One round trip
/// for everything the device list shows.
pub const INFO_COMMAND: &str = "echo manufacturer=$(getprop ro.product.manufacturer);\
echo model=$(getprop ro.product.model);\
echo release=$(getprop ro.build.version.release);\
echo sdk=$(getprop ro.build.version.sdk);\
echo build=$(getprop ro.build.id);\
dumpsys battery 2>/dev/null | grep -E '^ *(level|status|AC powered|USB powered):'";

pub fn parse_info(text: &str) -> DeviceInfo {
    let mut info = DeviceInfo::default();
    let mut powered = false;
    let mut status_charging = None;
    let non_empty = |v: &str| {
        let v = v.trim();
        (!v.is_empty()).then(|| v.to_string())
    };
    for line in text.lines() {
        let line = line.trim();
        if let Some((k, v)) = line.split_once('=') {
            match k {
                "manufacturer" => info.manufacturer = non_empty(v),
                "model" => info.model = non_empty(v),
                "release" => info.android_version = non_empty(v),
                "sdk" => info.sdk = v.trim().parse().ok(),
                "build" => info.build_id = non_empty(v),
                _ => {}
            }
        } else if let Some((k, v)) = line.split_once(':') {
            let v = v.trim();
            match k.trim() {
                "level" => info.battery_level = v.parse::<u8>().ok().filter(|l| *l <= 100),
                // BatteryManager.BATTERY_STATUS_CHARGING = 2, FULL = 5
                "status" => status_charging = v.parse::<u8>().ok().map(|s| s == 2 || s == 5),
                "AC powered" | "USB powered" => powered |= v == "true",
                _ => {}
            }
        }
    }
    info.charging = status_charging.or(info.battery_level.map(|_| powered));
    info
}

/// Command that prints the IMEI as a Parcel dump. Works on Android ≤ 13 for
/// the shell user on many devices; newer releases refuse it, which is shown
/// as "Not readable".
pub const IMEI_COMMAND: &str = "service call iphonesubinfo 1 s16 com.android.shell";

/// Decode the Parcel dump printed by `service call`:
///
/// ```text
/// Result: Parcel(
///   0x00000000: 00000000 0000000f 00350033 00380036 '........3.5.6.8.'
///   0x00000010: 00380039 00300035 00310033 00340032 '9.8.5.0.3.1.2.4.'
///   0x00000020: 00350035 00000037                   '5.5.7...        ')
/// ```
///
/// Word 0 is the status, word 1 the UTF-16 length, then two UTF-16 code
/// units per word, low half first.
pub fn parse_parcel_string(text: &str) -> Option<String> {
    if !text.contains("Parcel(") {
        return None;
    }
    let mut words = Vec::new();
    for line in text.lines() {
        let Some((_, rest)) = line.split_once(": ") else { continue };
        let hex_part = rest.split('\'').next().unwrap_or("");
        for w in hex_part.split_whitespace() {
            if w.len() == 8 {
                if let Ok(v) = u32::from_str_radix(w, 16) {
                    words.push(v);
                }
            }
        }
    }
    if words.len() < 2 || words[0] != 0 {
        return None;
    }
    let len = words[1] as usize;
    if len == 0 || len == 0xFFFF_FFFF {
        return None;
    }
    let mut units = Vec::with_capacity(len);
    for w in &words[2..] {
        units.push((w & 0xFFFF) as u16);
        units.push((w >> 16) as u16);
    }
    units.truncate(len);
    let s = String::from_utf16(&units).ok()?;
    let s = s.trim().to_string();
    (!s.is_empty() && s.chars().all(|c| c.is_ascii_alphanumeric())).then_some(s)
}

/// Parse `pm list packages` output (`package:com.example`).
pub fn parse_packages(text: &str, system: bool) -> Vec<Package> {
    let mut v: Vec<Package> = text
        .lines()
        .filter_map(|l| l.trim().strip_prefix("package:"))
        .filter(|n| !n.is_empty())
        .map(|n| Package { name: n.to_string(), system })
        .collect();
    v.sort_by(|a, b| a.name.cmp(&b.name));
    v
}

/// Parse `fastboot devices` output: `SERIAL\tfastboot` per line.
pub fn parse_fastboot_devices(text: &str) -> Vec<(String, bool)> {
    text.lines()
        .filter_map(|l| {
            let mut it = l.split_whitespace();
            let serial = it.next()?;
            let mode = it.next().unwrap_or("fastboot");
            if serial.starts_with('<') || serial.starts_with("???") {
                return None;
            }
            Some((serial.to_string(), mode == "fastbootd"))
        })
        .collect()
}

/// Extract `name: value` from fastboot's stderr (`getvar` writes there).
pub fn parse_getvar(text: &str, name: &str) -> Option<String> {
    let prefix = format!("{name}:");
    text.lines()
        .map(str::trim)
        .find_map(|l| l.strip_prefix(&prefix).map(|v| v.trim().to_string()))
        .filter(|v| !v.is_empty())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn device_list_long_format() {
        let text = "List of devices attached\n\
3A281FDJH00B7C         device usb:1-1 product:husky model:Pixel_8_Pro device:husky transport_id:7\n\
ZY22J4KQ9P             unauthorized usb:1-2 transport_id:9\n\
0123456789ABCDEF       no permissions (missing udev rules? user is in the plugdev group); see [http://developer.android.com/tools/device.html] usb:1-3 transport_id:3\n\
192.168.1.42:5555      offline transport_id:12\n\
b7e41c09               recovery usb:1-4 product:twrp model:OnePlus_12 device:aries transport_id:14\n";
        let d = parse_device_list(text);
        assert_eq!(d.len(), 5);
        assert_eq!(d[0].serial, "3A281FDJH00B7C");
        assert_eq!(d[0].state, DeviceState::Online);
        assert_eq!(d[0].model.as_deref(), Some("Pixel 8 Pro"));
        assert_eq!(d[0].transport_id, Some(7));
        assert_eq!(d[1].state, DeviceState::Unauthorized);
        assert_eq!(d[2].state, DeviceState::NoPermissions);
        assert_eq!(d[2].transport_id, Some(3));
        assert_eq!(d[3].state, DeviceState::Offline);
        assert_eq!(d[4].state, DeviceState::Recovery);
    }

    #[test]
    fn device_info() {
        let text = "manufacturer=Google\nmodel=Pixel 8 Pro\nrelease=15\nsdk=35\nbuild=AP4A.250105.002\n  AC powered: false\n  USB powered: true\n  status: 2\n  level: 82\n";
        let i = parse_info(text);
        assert_eq!(i.manufacturer.as_deref(), Some("Google"));
        assert_eq!(i.model.as_deref(), Some("Pixel 8 Pro"));
        assert_eq!(i.android_version.as_deref(), Some("15"));
        assert_eq!(i.sdk, Some(35));
        assert_eq!(i.battery_level, Some(82));
        assert_eq!(i.charging, Some(true));
    }

    #[test]
    fn device_info_in_recovery_has_no_battery() {
        let i = parse_info("manufacturer=\nmodel=\nrelease=14\nsdk=34\nbuild=\n");
        assert_eq!(i.model, None);
        assert_eq!(i.battery_level, None);
        assert_eq!(i.charging, None);
    }

    #[test]
    fn imei_parcel() {
        let text = "Result: Parcel(\n\
  0x00000000: 00000000 0000000f 00350033 00380036 '........3.5.6.8.'\n\
  0x00000010: 00380039 00300035 00310033 00340032 '9.8.5.0.3.1.2.4.'\n\
  0x00000020: 00350035 00000037                   '5.5.7...        ')\n";
        assert_eq!(parse_parcel_string(text).as_deref(), Some("356898503124557"));
    }

    #[test]
    fn imei_refused() {
        let text = "Result: Parcel(\n  0x00000000: ffffffe0 00000045 '....E...')\n";
        assert_eq!(parse_parcel_string(text), None);
        assert_eq!(parse_parcel_string("service: not found"), None);
    }

    #[test]
    fn packages() {
        let p = parse_packages("package:com.b\npackage:com.a\n\n", false);
        assert_eq!(p.iter().map(|p| p.name.as_str()).collect::<Vec<_>>(), ["com.a", "com.b"]);
    }

    #[test]
    fn fastboot_list_and_getvar() {
        let d = parse_fastboot_devices("29051JEGR09412\tfastboot\nABC\tfastbootd\n");
        assert_eq!(d, vec![("29051JEGR09412".into(), false), ("ABC".into(), true)]);
        let err = "is-userspace: yes\nFinished. Total time: 0.001s\n";
        assert_eq!(parse_getvar(err, "is-userspace").as_deref(), Some("yes"));
        assert_eq!(parse_getvar(err, "product"), None);
    }
}
