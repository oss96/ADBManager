//! One list of devices, merged by serial from adb and fastboot, so a device
//! that reboots into the bootloader keeps its row and its name.

use std::collections::HashMap;
use std::time::{Duration, Instant};

use crate::adb::AdbDeviceLine;
use crate::api::{Device, DeviceInfo, DeviceState, Link};

#[derive(Debug, Clone)]
struct Entry {
    order: u64,
    adb: Option<AdbDeviceLine>,
    fastboot: Option<bool>, // Some(userspace)
    info: DeviceInfo,
    /// When the device vanished from both adb and fastboot.
    missing_since: Option<Instant>,
    /// Set when we rebooted it: while it is between modes it is shown as
    /// "Rebooting…" instead of disappearing. Holds the deadline and the
    /// state it was rebooted from.
    rebooting: Option<(Instant, Option<DeviceState>)>,
}

/// How long a vanished device's row (position, name, details) is
/// remembered, and how long a rebooting device may be absent before its row
/// is hidden.
const REMEMBER_GONE: Duration = Duration::from_secs(90);

#[derive(Debug, Default)]
pub struct Registry {
    entries: HashMap<String, Entry>,
    next_order: u64,
}

/// What changed after an update; used to decide which devices need their
/// details (re)read.
#[derive(Debug, Default, PartialEq)]
pub struct Changes {
    pub changed: bool,
    /// Devices that just became usable over adb.
    pub became_ready: Vec<String>,
}

impl Registry {
    fn entry(&mut self, serial: &str) -> &mut Entry {
        if !self.entries.contains_key(serial) {
            let order = self.next_order;
            self.next_order += 1;
            let e = Entry {
                order,
                adb: None,
                fastboot: None,
                info: DeviceInfo::default(),
                missing_since: None,
                rebooting: None,
            };
            self.entries.insert(serial.to_string(), e);
        }
        self.entries.get_mut(serial).expect("inserted above")
    }

    fn state_of(e: &Entry) -> Option<DeviceState> {
        if let Some(a) = &e.adb {
            return Some(a.state);
        }
        if let Some(u) = e.fastboot {
            return Some(if u { DeviceState::Fastbootd } else { DeviceState::Bootloader });
        }
        e.rebooting.filter(|(t, _)| *t > Instant::now()).map(|_| DeviceState::Rebooting)
    }

    /// Show `serial` as rebooting until it reappears in adb or fastboot.
    pub fn mark_rebooting(&mut self, serial: &str) {
        if let Some(e) = self.entries.get_mut(serial) {
            let from = Self::state_of(e);
            e.rebooting = Some((Instant::now() + REMEMBER_GONE, from));
        }
    }

    pub fn state(&self, serial: &str) -> Option<DeviceState> {
        self.entries.get(serial).and_then(Self::state_of)
    }

    /// Replace the adb view with a full snapshot from track-devices.
    pub fn update_adb(&mut self, list: Vec<AdbDeviceLine>) -> Changes {
        let mut ch = Changes::default();
        let before: HashMap<String, Option<DeviceState>> =
            self.entries.iter().map(|(k, e)| (k.clone(), e.adb.as_ref().map(|a| a.state))).collect();
        let seen: Vec<String> = list.iter().map(|d| d.serial.clone()).collect();
        for d in list {
            let serial = d.serial.clone();
            let prev = before.get(&serial).copied().flatten();
            if prev != Some(d.state) {
                ch.changed = true;
                if d.state.is_adb_usable() {
                    ch.became_ready.push(serial.clone());
                }
            }
            let e = self.entry(&serial);
            if e.adb.as_ref() != Some(&d) {
                ch.changed = true;
            }
            e.adb = Some(d);
        }
        for (serial, e) in self.entries.iter_mut() {
            if e.adb.is_some() && !seen.contains(serial) {
                e.adb = None;
                ch.changed = true;
            }
        }
        self.prune();
        ch
    }

    /// Replace the fastboot view with a full snapshot. Returns true if
    /// anything changed.
    pub fn update_fastboot(&mut self, list: &[(String, bool)]) -> bool {
        let mut changed = false;
        for (serial, userspace) in list {
            let e = self.entry(serial);
            if e.fastboot != Some(*userspace) {
                e.fastboot = Some(*userspace);
                changed = true;
            }
        }
        for (serial, e) in self.entries.iter_mut() {
            if e.fastboot.is_some() && !list.iter().any(|(s, _)| s == serial) {
                e.fastboot = None;
                changed = true;
            }
        }
        self.prune();
        changed
    }

    /// Track vanished devices and forget them after `REMEMBER_GONE`.
    fn prune(&mut self) {
        let now = Instant::now();
        for e in self.entries.values_mut() {
            let present = e.adb.is_some() || e.fastboot.is_some();
            if present {
                // The reboot is over once the device is back after a gap, or
                // shows up in a different mode. Until then it may still be
                // listed in its old state for a moment.
                let now_state = Self::state_of(e);
                if e.missing_since.is_some() || e.rebooting.is_some_and(|(_, from)| from != now_state) {
                    e.rebooting = None;
                }
                e.missing_since = None;
            } else if e.missing_since.is_none() {
                e.missing_since = Some(now);
            }
        }
        self.entries.retain(|_, e| e.missing_since.is_none_or(|t| now.duration_since(t) < REMEMBER_GONE));
    }

    /// Called periodically. Returns true when a "Rebooting…" row expired and
    /// the visible list changed.
    pub fn tick(&mut self) -> bool {
        let now = Instant::now();
        let mut changed = false;
        for e in self.entries.values_mut() {
            if e.rebooting.is_some_and(|(t, _)| t <= now) {
                e.rebooting = None;
                changed = true;
            }
        }
        self.prune();
        changed
    }

    pub fn set_info(&mut self, serial: &str, info: DeviceInfo) -> bool {
        match self.entries.get_mut(serial) {
            Some(e) => {
                // Keep previously known fields when the new read is partial
                // (e.g. in recovery there is no battery service).
                let merged = DeviceInfo {
                    manufacturer: info.manufacturer.or(e.info.manufacturer.take()),
                    model: info.model.or(e.info.model.take()),
                    android_version: info.android_version.or(e.info.android_version.take()),
                    sdk: info.sdk.or(e.info.sdk),
                    build_id: info.build_id.or(e.info.build_id.take()),
                    battery_level: info.battery_level,
                    charging: info.charging,
                };
                let changed = merged != e.info;
                e.info = merged;
                changed
            }
            None => false,
        }
    }

    pub fn snapshot(&self) -> Vec<Device> {
        let mut v: Vec<(&u64, Device)> = self
            .entries
            .iter()
            .filter_map(|(serial, e)| {
                let state = Self::state_of(e)?;
                let adb = e.adb.as_ref();
                let model = adb.and_then(|a| a.model.clone());
                let display_name = e.info.model.clone().or_else(|| model.clone()).unwrap_or_else(|| serial.clone());
                Some((
                    &e.order,
                    Device {
                        serial: serial.clone(),
                        state,
                        state_label: state.label().to_string(),
                        state_hint: state.hint().map(str::to_string),
                        link: Link::from_serial(serial),
                        model,
                        product: adb.and_then(|a| a.product.clone()),
                        device: adb.and_then(|a| a.device.clone()),
                        transport_id: adb.and_then(|a| a.transport_id),
                        info: e.info.clone(),
                        display_name,
                    },
                ))
            })
            .collect();
        v.sort_by_key(|(o, _)| **o);
        v.into_iter().map(|(_, d)| d).collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn line(serial: &str, state: DeviceState) -> AdbDeviceLine {
        AdbDeviceLine {
            serial: serial.into(),
            state,
            product: None,
            model: Some("Pixel 7a".into()),
            device: None,
            transport_id: Some(1),
        }
    }

    #[test]
    fn device_keeps_row_through_bootloader() {
        let mut r = Registry::default();
        let c = r.update_adb(vec![line("A", DeviceState::Online), line("B", DeviceState::Online)]);
        assert!(c.changed);
        assert_eq!(c.became_ready, vec!["A", "B"]);
        r.set_info("A", DeviceInfo { model: Some("Pixel 7a".into()), sdk: Some(34), ..Default::default() });

        // A reboots to bootloader: gone from adb for a moment, then in fastboot.
        r.mark_rebooting("A");
        r.update_adb(vec![line("B", DeviceState::Online)]);
        assert_eq!(r.state("A"), Some(DeviceState::Rebooting));
        assert_eq!(r.snapshot()[0].display_name, "Pixel 7a");
        assert!(r.update_fastboot(&[("A".into(), false)]));
        let snap = r.snapshot();
        assert_eq!(snap.len(), 2);
        assert_eq!(snap[0].serial, "A", "A keeps its position");
        assert_eq!(snap[0].state, DeviceState::Bootloader);
        assert_eq!(snap[0].display_name, "Pixel 7a");
        assert_eq!(snap[0].info.sdk, Some(34), "details survive the reboot");

        // Back in recovery over adb.
        r.update_fastboot(&[]);
        let c = r.update_adb(vec![line("A", DeviceState::Recovery), line("B", DeviceState::Online)]);
        assert_eq!(c.became_ready, vec!["A"]);
        assert_eq!(r.state("A"), Some(DeviceState::Recovery));
    }

    #[test]
    fn rebooting_mark_survives_polls_while_still_listed() {
        let mut r = Registry::default();
        r.update_adb(vec![line("A", DeviceState::Online)]);
        r.mark_rebooting("A");
        // Periodic polls while adb still lists the device in its old state.
        r.update_fastboot(&[]);
        r.tick();
        r.update_adb(vec![line("A", DeviceState::Online)]);
        // Now it drops off adb.
        r.update_adb(vec![]);
        assert_eq!(r.state("A"), Some(DeviceState::Rebooting));
        // Back in a new mode: the mark is gone, so a later unplug hides it.
        r.update_fastboot(&[("A".into(), false)]);
        assert_eq!(r.state("A"), Some(DeviceState::Bootloader));
        r.update_fastboot(&[]);
        assert_eq!(r.state("A"), None);
    }

    #[test]
    fn unplugged_device_disappears() {
        let mut r = Registry::default();
        r.update_adb(vec![line("A", DeviceState::Online)]);
        r.set_info("A", DeviceInfo { model: Some("Pixel 7a".into()), ..Default::default() });
        r.update_adb(vec![line("B", DeviceState::Online)]);
        let c = r.update_adb(vec![line("B", DeviceState::Online)]);
        assert!(!c.changed);
        assert_eq!(r.snapshot().len(), 1, "unplugged A is hidden at once");
        // Plugged back in: same position, name remembered.
        r.update_adb(vec![line("A", DeviceState::Online), line("B", DeviceState::Online)]);
        let snap = r.snapshot();
        assert_eq!(snap[0].serial, "A");
        assert_eq!(snap[0].info.model.as_deref(), Some("Pixel 7a"));
    }

    #[test]
    fn unchanged_snapshot_reports_no_change() {
        let mut r = Registry::default();
        r.update_adb(vec![line("A", DeviceState::Online)]);
        assert_eq!(r.update_adb(vec![line("A", DeviceState::Online)]), Changes::default());
        assert!(!r.update_fastboot(&[]));
    }
}
