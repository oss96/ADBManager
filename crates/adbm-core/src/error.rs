use std::io;

/// Errors produced by the core. Messages are written for end users: they say
/// what went wrong and, where possible, how to fix it.
#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("adb server is not reachable at {addr}: {source}")]
    ServerUnreachable { addr: String, source: io::Error },

    #[error("adb was not found. Install Android platform-tools or set the adb path in Settings.")]
    AdbNotFound,

    #[error("fastboot was not found. Install Android platform-tools or set the fastboot path in Settings.")]
    FastbootNotFound,

    #[error("adb refused the request: {0}")]
    Fail(String),

    #[error("device {0} is not connected")]
    DeviceNotFound(String),

    #[error("device {serial} is {state}, it must be online for this action")]
    DeviceNotReady { serial: String, state: String },

    #[error("protocol error: {0}")]
    Protocol(String),

    #[error("{0}")]
    Io(#[from] io::Error),

    #[error("could not read APK: {0}")]
    Apk(String),

    #[error("{0}")]
    Invalid(String),

    #[error("timed out: {0}")]
    Timeout(String),

    #[error("cancelled")]
    Cancelled,

    #[error("{tool} exited with {code}: {output}")]
    ToolFailed { tool: &'static str, code: i32, output: String },
}

pub type Result<T, E = Error> = std::result::Result<T, E>;

impl Error {
    pub(crate) fn protocol(msg: impl Into<String>) -> Self {
        Error::Protocol(msg.into())
    }
}
