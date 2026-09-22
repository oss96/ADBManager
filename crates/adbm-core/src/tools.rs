//! Locating `adb` / `fastboot` and running them.

use std::path::{Path, PathBuf};
use std::process::Stdio;
use std::time::Duration;

use tokio::process::Command;

use crate::error::{Error, Result};

#[cfg(windows)]
const EXE: &str = ".exe";
#[cfg(not(windows))]
const EXE: &str = "";

/// Candidate locations for a platform-tools binary, most specific first.
pub fn candidates(tool: &str, configured: Option<&str>) -> Vec<PathBuf> {
    let file = format!("{tool}{EXE}");
    let mut v = Vec::new();
    if let Some(c) = configured.filter(|c| !c.trim().is_empty()) {
        v.push(PathBuf::from(c));
    }
    if let Some(path) = std::env::var_os("PATH") {
        v.extend(std::env::split_paths(&path).map(|d| d.join(&file)));
    }
    for var in ["ANDROID_HOME", "ANDROID_SDK_ROOT"] {
        if let Some(root) = std::env::var_os(var) {
            v.push(Path::new(&root).join("platform-tools").join(&file));
        }
    }
    if let Some(home) = home_dir() {
        #[cfg(target_os = "macos")]
        v.push(home.join("Library/Android/sdk/platform-tools").join(&file));
        #[cfg(target_os = "linux")]
        v.push(home.join("Android/Sdk/platform-tools").join(&file));
        #[cfg(windows)]
        if let Some(local) = std::env::var_os("LOCALAPPDATA") {
            v.push(Path::new(&local).join("Android\\Sdk\\platform-tools").join(&file));
        }
        let _ = &home;
    }
    #[cfg(target_os = "macos")]
    {
        v.push(PathBuf::from("/opt/homebrew/bin").join(&file));
        v.push(PathBuf::from("/usr/local/bin").join(&file));
    }
    // Next to our own executable (for users who drop platform-tools there).
    if let Some(dir) = std::env::current_exe().ok().and_then(|p| p.parent().map(Path::to_path_buf)) {
        v.push(dir.join("platform-tools").join(&file));
        v.push(dir.join(&file));
    }
    v
}

pub fn locate(tool: &str, configured: Option<&str>) -> Option<PathBuf> {
    candidates(tool, configured).into_iter().find(|p| p.is_file())
}

fn home_dir() -> Option<PathBuf> {
    std::env::var_os(if cfg!(windows) { "USERPROFILE" } else { "HOME" }).map(PathBuf::from)
}

pub struct ToolOutput {
    pub code: i32,
    pub stdout: String,
    pub stderr: String,
}

/// Run a tool with a timeout. The child is killed if the future is dropped.
pub async fn run(program: &Path, args: &[&str], timeout: Duration) -> Result<ToolOutput> {
    let mut cmd = Command::new(program);
    cmd.args(args).stdin(Stdio::null()).stdout(Stdio::piped()).stderr(Stdio::piped()).kill_on_drop(true);
    #[cfg(windows)]
    {
        // CREATE_NO_WINDOW: don't flash a console window from a GUI app.
        cmd.creation_flags(0x0800_0000);
    }
    let child = cmd.spawn()?;
    let out = tokio::time::timeout(timeout, child.wait_with_output())
        .await
        .map_err(|_| Error::Timeout(format!("{} {}", program.display(), args.join(" "))))??;
    Ok(ToolOutput {
        code: out.status.code().unwrap_or(-1),
        stdout: String::from_utf8_lossy(&out.stdout).into_owned(),
        stderr: String::from_utf8_lossy(&out.stderr).into_owned(),
    })
}

/// `adb start-server`. The server daemonises, so this returns quickly.
pub async fn start_adb_server(adb: &Path, port: u16) -> Result<()> {
    let port = port.to_string();
    let out = run(adb, &["-P", &port, "start-server"], Duration::from_secs(20)).await?;
    if out.code != 0 {
        return Err(Error::ToolFailed { tool: "adb", code: out.code, output: out.stderr.trim().to_string() });
    }
    Ok(())
}
