//! The public contract between the core and every UI.
//!
//! These types are serialised as JSON across the C ABI (Swift, C#) and used
//! directly by the GTK app. Changing a field name here is a breaking change
//! for all three apps.

use serde::{Deserialize, Serialize};

use crate::apk::ApkInfo;
use crate::routines::Routine;

pub type JobId = u64;

// ---------------------------------------------------------------- config ---

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(default)]
pub struct Config {
    /// Explicit path to `adb`. `None` means auto-detect.
    pub adb_path: Option<String>,
    /// Explicit path to `fastboot`. `None` means auto-detect.
    pub fastboot_path: Option<String>,
    pub adb_host: String,
    pub adb_port: u16,
    /// Start the adb server when it is not running.
    pub auto_start_server: bool,
    /// Maximum number of devices worked on at the same time.
    pub max_concurrency: usize,
    pub fastboot_poll_ms: u64,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            adb_path: None,
            fastboot_path: None,
            adb_host: "127.0.0.1".into(),
            adb_port: 5037,
            auto_start_server: true,
            max_concurrency: 8,
            fastboot_poll_ms: 1500,
        }
    }
}

impl Config {
    pub fn adb_addr(&self) -> String {
        format!("{}:{}", self.adb_host, self.adb_port)
    }
}

// --------------------------------------------------------------- devices ---

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(rename_all = "snake_case")]
pub enum DeviceState {
    Online,
    Unauthorized,
    Authorizing,
    Offline,
    Connecting,
    NoPermissions,
    Recovery,
    Rescue,
    Sideload,
    Bootloader,
    Fastbootd,
    /// Rebooted by us and currently between modes.
    Rebooting,
    Unknown,
}

impl DeviceState {
    /// Parse the state word that adb prints in `devices -l` / `track-devices`.
    pub fn from_adb(word: &str) -> Self {
        match word {
            "device" => Self::Online,
            "unauthorized" => Self::Unauthorized,
            "authorizing" => Self::Authorizing,
            "offline" => Self::Offline,
            "connecting" => Self::Connecting,
            "no" => Self::NoPermissions, // "no permissions (...)"
            "recovery" => Self::Recovery,
            "rescue" => Self::Rescue,
            "sideload" => Self::Sideload,
            "bootloader" => Self::Bootloader,
            _ => Self::Unknown,
        }
    }

    /// States in which adb shell / sync work.
    pub fn is_adb_usable(self) -> bool {
        matches!(self, Self::Online | Self::Recovery)
    }

    pub fn is_fastboot(self) -> bool {
        matches!(self, Self::Bootloader | Self::Fastbootd)
    }

    /// User-facing label, matching `design/DESIGN.md`.
    pub fn label(self) -> &'static str {
        match self {
            Self::Online => "Online",
            Self::Unauthorized => "Unauthorized",
            Self::Authorizing => "Authorizing…",
            Self::Offline => "Offline",
            Self::Connecting => "Connecting…",
            Self::NoPermissions => "No permission",
            Self::Recovery => "Recovery",
            Self::Rescue => "Rescue",
            Self::Sideload => "Sideload",
            Self::Bootloader => "Bootloader",
            Self::Fastbootd => "Fastbootd",
            Self::Rebooting => "Rebooting…",
            Self::Unknown => "Unknown",
        }
    }

    /// One-line hint on how to get the device usable, if it is not.
    pub fn hint(self) -> Option<&'static str> {
        match self {
            Self::Unauthorized => Some("Confirm the USB debugging prompt on the device."),
            Self::Offline => Some("Reconnect the cable or restart adb."),
            Self::NoPermissions => Some("Your user can't open the USB device. Add a udev rule for Android devices."),
            _ => None,
        }
    }
}

impl std::fmt::Display for DeviceState {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.label())
    }
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum Link {
    Usb,
    Tcp,
    Emulator,
}

impl Link {
    pub fn from_serial(serial: &str) -> Self {
        if serial.starts_with("emulator-") {
            Link::Emulator
        } else if serial.contains(':') && !serial.starts_with("usb:") {
            Link::Tcp
        } else {
            Link::Usb
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
pub struct DeviceInfo {
    pub manufacturer: Option<String>,
    pub model: Option<String>,
    pub android_version: Option<String>,
    pub sdk: Option<u32>,
    pub build_id: Option<String>,
    pub battery_level: Option<u8>,
    pub charging: Option<bool>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Device {
    pub serial: String,
    pub state: DeviceState,
    /// Localised label for `state`.
    pub state_label: String,
    pub state_hint: Option<String>,
    pub link: Link,
    /// Model reported by adb (`model:` in `devices -l`), underscores replaced.
    pub model: Option<String>,
    pub product: Option<String>,
    pub device: Option<String>,
    pub transport_id: Option<u64>,
    /// Details read from the device once it is online. Kept while the device
    /// reboots through bootloader / recovery so the row keeps its name.
    pub info: DeviceInfo,
    /// Best display name: marketing model if known, else serial.
    pub display_name: String,
}

// ----------------------------------------------------------------- files ---

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct DirEntry {
    pub name: String,
    pub kind: EntryKind,
    pub size: u64,
    pub mode: u32,
    pub mtime: u64,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum EntryKind {
    File,
    Dir,
    Symlink,
    Other,
}

impl EntryKind {
    pub fn from_mode(mode: u32) -> Self {
        match mode & 0o170000 {
            0o040000 => EntryKind::Dir,
            0o100000 => EntryKind::File,
            0o120000 => EntryKind::Symlink,
            _ => EntryKind::Other,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Package {
    pub name: String,
    pub system: bool,
}

// --------------------------------------------------------------- actions ---

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum RebootMode {
    System,
    Recovery,
    Bootloader,
    Fastboot,
    Sideload,
}

impl RebootMode {
    pub fn label(self) -> &'static str {
        match self {
            RebootMode::System => "System",
            RebootMode::Recovery => "Recovery",
            RebootMode::Bootloader => "Bootloader",
            RebootMode::Fastboot => "Fastboot",
            RebootMode::Sideload => "Sideload",
        }
    }
}

/// A request from a UI. Commands that touch devices start a job and return
/// `Response::Job` immediately; their results arrive as events.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum Command {
    /// Snapshot of everything the UI needs to render from scratch.
    GetState,
    SetConfig {
        config: Config,
    },
    RestartServer,
    Connect {
        address: String,
    },
    Disconnect {
        address: String,
    },
    RefreshDevice {
        serial: String,
    },
    ReadImei {
        serial: String,
    },

    InspectApk {
        path: String,
    },
    Install {
        serials: Vec<String>,
        path: String,
        #[serde(default)]
        allow_downgrade: bool,
    },
    Uninstall {
        serials: Vec<String>,
        package: String,
        #[serde(default)]
        keep_data: bool,
    },
    ListPackages {
        serial: String,
        #[serde(default)]
        include_system: bool,
    },

    Reboot {
        serials: Vec<String>,
        mode: RebootMode,
    },
    Shell {
        serials: Vec<String>,
        command: String,
    },

    ListDir {
        serial: String,
        path: String,
    },
    Push {
        serials: Vec<String>,
        local: String,
        remote: String,
    },
    Pull {
        serial: String,
        remote: String,
        local: String,
    },
    MakeDir {
        serials: Vec<String>,
        path: String,
    },
    Delete {
        serial: String,
        path: String,
    },

    FastbootBoot {
        serials: Vec<String>,
        image: String,
    },
    FastbootGetVar {
        serial: String,
        name: String,
    },

    RoutineTemplates,
    RunRoutine {
        serials: Vec<String>,
        routine: Routine,
    },

    Cancel {
        job_id: JobId,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum Response {
    Ok,
    Job { job_id: JobId },
    State { devices: Vec<Device>, jobs: Vec<JobInfo>, server: ServerStatus, config: Config, core_version: String },
    Apk { apk: ApkInfo },
    Routines { routines: Vec<Routine> },
    Error { message: String },
}

impl Response {
    pub fn error(e: impl std::fmt::Display) -> Self {
        Response::Error { message: e.to_string() }
    }
}

// ------------------------------------------------------------------ jobs ---

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct JobInfo {
    pub id: JobId,
    pub title: String,
    pub serials: Vec<String>,
    /// Background jobs (listing a folder, reading packages) are not shown in
    /// the Activity list.
    pub visible: bool,
    pub started_at_ms: u64,
    pub status: JobStatus,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum JobStatus {
    Running,
    Succeeded,
    PartiallyFailed,
    Failed,
    Cancelled,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum Stream {
    Stdout,
    Stderr,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(tag = "state", rename_all = "snake_case")]
pub enum ServerStatus {
    Starting,
    Running { version: u32, adb_path: Option<String>, fastboot_path: Option<String> },
    AdbNotFound { searched: Vec<String> },
    Error { message: String },
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum LogLevel {
    Debug,
    Info,
    Warn,
    Error,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum Event {
    DevicesChanged {
        devices: Vec<Device>,
    },
    Server {
        status: ServerStatus,
    },
    JobStarted {
        job: JobInfo,
    },
    JobProgress {
        job_id: JobId,
        serial: Option<String>,
        done: u64,
        total: u64,
        message: String,
    },
    JobOutput {
        job_id: JobId,
        serial: String,
        stream: Stream,
        text: String,
    },
    JobDeviceFinished {
        job_id: JobId,
        serial: String,
        ok: bool,
        message: String,
    },
    JobFinished {
        job_id: JobId,
        status: JobStatus,
        summary: String,
        #[serde(skip_serializing_if = "Option::is_none")]
        data: Option<serde_json::Value>,
    },
    Log {
        level: LogLevel,
        message: String,
    },
}
