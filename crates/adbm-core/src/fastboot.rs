//! fastboot, driven through the platform-tools binary. fastboot has no
//! "track" service, so devices are polled.

use std::path::{Path, PathBuf};
use std::time::Duration;

use crate::adb::parse;
use crate::api::RebootMode;
use crate::error::{Error, Result};
use crate::tools;

#[derive(Debug, Clone)]
pub struct Fastboot {
    pub path: PathBuf,
}

#[derive(Debug, Clone, PartialEq)]
pub struct FastbootDevice {
    pub serial: String,
    pub userspace: bool,
}

impl Fastboot {
    pub fn new(path: PathBuf) -> Self {
        Self { path }
    }

    async fn run(&self, args: &[&str], timeout: Duration) -> Result<tools::ToolOutput> {
        tools::run(&self.path, args, timeout).await
    }

    async fn run_ok(&self, args: &[&str], timeout: Duration) -> Result<String> {
        let out = self.run(args, timeout).await?;
        // fastboot prints progress and errors on stderr.
        let text = format!("{}{}", out.stdout, out.stderr);
        if out.code != 0 {
            return Err(Error::ToolFailed { tool: "fastboot", code: out.code, output: last_line(&text) });
        }
        Ok(text)
    }

    pub async fn devices(&self) -> Result<Vec<(String, bool)>> {
        let out = self.run(&["devices"], Duration::from_secs(10)).await?;
        Ok(parse::parse_fastboot_devices(&out.stdout))
    }

    pub async fn getvar(&self, serial: &str, name: &str) -> Result<Option<String>> {
        let out = self.run(&["-s", serial, "getvar", name], Duration::from_secs(10)).await?;
        Ok(parse::parse_getvar(&format!("{}\n{}", out.stderr, out.stdout), name))
    }

    pub async fn is_userspace(&self, serial: &str) -> bool {
        matches!(self.getvar(serial, "is-userspace").await, Ok(Some(v)) if v == "yes")
    }

    pub async fn reboot(&self, serial: &str, mode: RebootMode) -> Result<()> {
        let mut args = vec!["-s", serial, "reboot"];
        match mode {
            RebootMode::System => {}
            RebootMode::Bootloader => args.push("bootloader"),
            RebootMode::Fastboot => args.push("fastboot"),
            RebootMode::Recovery => args.push("recovery"),
            RebootMode::Sideload => {
                return Err(Error::Invalid("Sideload is only reachable from adb or recovery".into()))
            }
        }
        self.run_ok(&args, Duration::from_secs(30)).await.map(|_| ())
    }

    pub async fn boot(&self, serial: &str, image: &Path) -> Result<()> {
        let img = image.to_string_lossy();
        self.run_ok(&["-s", serial, "boot", &img], Duration::from_secs(180)).await.map(|_| ())
    }
}

fn last_line(s: &str) -> String {
    s.lines().map(str::trim).rfind(|l| !l.is_empty() && !l.starts_with("Finished.")).unwrap_or("").to_string()
}
