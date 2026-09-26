//! Routines: an ordered list of steps run on every target device.
//! Devices run in parallel; steps within a device run in order and stop at
//! the first failure.

use serde::{Deserialize, Serialize};

use crate::api::RebootMode;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Routine {
    pub id: String,
    pub name: String,
    #[serde(default)]
    pub description: String,
    pub steps: Vec<Step>,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum WaitTarget {
    /// Online in Android.
    System,
    Recovery,
    /// Any fastboot state (bootloader or fastbootd).
    Fastboot,
    /// Any state where adb shell works.
    Adb,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum Step {
    Install {
        path: String,
    },
    Uninstall {
        package: String,
    },
    Shell {
        command: String,
        #[serde(default)]
        ignore_failure: bool,
    },
    Reboot {
        mode: RebootMode,
    },
    Push {
        local: String,
        remote: String,
    },
    Pull {
        remote: String,
        local: String,
    },
    WaitFor {
        target: WaitTarget,
        #[serde(default = "default_timeout")]
        timeout_s: u64,
    },
    FastbootBoot {
        image: String,
    },
    Delay {
        ms: u64,
    },
}

fn default_timeout() -> u64 {
    120
}

impl Step {
    pub fn title(&self) -> String {
        match self {
            Step::Install { path } => format!("Install {}", or(short(path), "an APK")),
            Step::Uninstall { package } => format!("Uninstall {package}"),
            Step::Shell { command, .. } => format!("Run “{command}”"),
            Step::Reboot { mode } => format!("Reboot to {}", mode.label()),
            Step::Push { local, remote } => format!("Push {} to {remote}", or(short(local), "a folder")),
            Step::Pull { remote, .. } => format!("Pull {remote}"),
            Step::WaitFor { target, timeout_s } => format!(
                "Wait for {} (up to {timeout_s} s)",
                match target {
                    WaitTarget::System => "Android",
                    WaitTarget::Recovery => "recovery",
                    WaitTarget::Fastboot => "fastboot",
                    WaitTarget::Adb => "adb",
                }
            ),
            Step::FastbootBoot { image } => format!("Boot {} (without flashing)", or(short(image), "an image")),
            Step::Delay { ms } => format!("Wait {:.1} s", *ms as f64 / 1000.0),
        }
    }

    /// Empty required fields, as a message naming the step.
    fn missing(&self) -> Option<&'static str> {
        let blank = |s: &str| s.trim().is_empty();
        match self {
            Step::Install { path } if blank(path) => Some("choose an APK"),
            Step::Uninstall { package } if blank(package) => Some("enter a package name"),
            Step::Shell { command, .. } if blank(command) => Some("enter a command"),
            Step::Push { local, remote } if blank(local) || blank(remote) => Some("choose what to push and where"),
            Step::Pull { remote, local } if blank(remote) || blank(local) => Some("choose what to pull and where"),
            Step::FastbootBoot { image } if blank(image) => Some("choose a boot image"),
            _ => None,
        }
    }
}

fn or<'a>(s: &'a str, fallback: &'a str) -> &'a str {
    if s.is_empty() {
        fallback
    } else {
        s
    }
}

fn short(p: &str) -> &str {
    p.rsplit(['/', '\\']).next().filter(|s| !s.is_empty()).unwrap_or(p)
}

impl Routine {
    pub fn validate(&self) -> Result<(), String> {
        if self.steps.is_empty() {
            return Err(format!("“{}” has no steps.", self.name));
        }
        for (i, s) in self.steps.iter().enumerate() {
            if let Some(m) = s.missing() {
                return Err(format!("Step {} ({}): {m}.", i + 1, s.title()));
            }
        }
        Ok(())
    }
}

/// Built-in templates. Paths are left empty for the user to fill in.
pub fn templates() -> Vec<Routine> {
    vec![
        Routine {
            id: "twrp-push".into(),
            name: "Boot TWRP and push folder".into(),
            description: "Boots a recovery image without flashing it, pushes a folder to /system, \
                          clears the Dalvik cache and reboots. This is the provisioning flow from ADB Manager 1."
                .into(),
            steps: vec![
                Step::Reboot { mode: RebootMode::Bootloader },
                Step::WaitFor { target: WaitTarget::Fastboot, timeout_s: 90 },
                Step::FastbootBoot { image: String::new() },
                Step::WaitFor { target: WaitTarget::Recovery, timeout_s: 180 },
                Step::Shell { command: "mount system".into(), ignore_failure: true },
                Step::Push { local: String::new(), remote: "/system/".into() },
                Step::Shell { command: "rm -r /data/dalvik-cache".into(), ignore_failure: true },
                Step::Shell { command: "rm -r /sdcard/TWRP".into(), ignore_failure: true },
                Step::Shell { command: "umount system".into(), ignore_failure: true },
                Step::Reboot { mode: RebootMode::System },
            ],
        },
        Routine {
            id: "install-and-reboot".into(),
            name: "Install app and reboot".into(),
            description: "Installs an APK, reboots and waits until Android is back.".into(),
            steps: vec![
                Step::Install { path: String::new() },
                Step::Reboot { mode: RebootMode::System },
                Step::Delay { ms: 3000 },
                Step::WaitFor { target: WaitTarget::System, timeout_s: 180 },
            ],
        },
        Routine {
            id: "reboot-cycle".into(),
            name: "Reboot and wait".into(),
            description: "Reboots each device and waits until Android is back online.".into(),
            steps: vec![
                Step::Reboot { mode: RebootMode::System },
                Step::Delay { ms: 3000 },
                Step::WaitFor { target: WaitTarget::System, timeout_s: 180 },
            ],
        },
    ]
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn template_requires_paths() {
        let t = &templates()[0];
        let err = t.validate().unwrap_err();
        assert_eq!(err, "Step 3 (Boot an image (without flashing)): choose a boot image.");
    }

    #[test]
    fn json_round_trip() {
        let r = Routine {
            id: "x".into(),
            name: "X".into(),
            description: String::new(),
            steps: vec![Step::WaitFor { target: WaitTarget::Recovery, timeout_s: 10 }],
        };
        let j = serde_json::to_string(&r).unwrap();
        assert!(j.contains(r#""type":"wait_for""#));
        assert_eq!(serde_json::from_str::<Routine>(&j).unwrap(), r);
        let s: Step = serde_json::from_str(r#"{"type":"wait_for","target":"fastboot"}"#).unwrap();
        assert_eq!(s, Step::WaitFor { target: WaitTarget::Fastboot, timeout_s: 120 });
    }
}
