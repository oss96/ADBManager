//! Per-device operations used by jobs.

use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::{Duration, Instant};

use serde_json::json;
use tokio_util::sync::CancellationToken;

use crate::adb::shell::{self, quote, ShellOutput};
use crate::adb::sync::{file_name, remote_join, Sync};
use crate::adb::{parse, wire};
use crate::api::{DeviceState, RebootMode};
use crate::engine::{count, DeviceOk, Inner, JobCtx};
use crate::error::{Error, Result};
use crate::routines::{Routine, Step, WaitTarget};

/// Progress events are throttled to this interval per device.
const PROGRESS_EVERY: Duration = Duration::from_millis(120);

fn ensure_adb(inner: &Inner, serial: &str) -> Result<()> {
    match inner.state(serial) {
        None => Err(Error::DeviceNotFound(serial.into())),
        Some(s) if s.is_adb_usable() => Ok(()),
        Some(s) => Err(Error::DeviceNotReady { serial: inner.display_name(serial), state: s.label().to_lowercase() }),
    }
}

async fn supports_v2(addr: &str, serial: &str) -> bool {
    wire::device_features(addr, serial).await.map(|f| f.iter().any(|x| x == "shell_v2")).unwrap_or(false)
}

/// Run a command without streaming.
async fn sh(addr: &str, serial: &str, command: &str, cancel: &CancellationToken) -> Result<ShellOutput> {
    let v2 = supports_v2(addr, serial).await;
    shell::run(addr, serial, command, v2, cancel, |_, _| {}).await
}

pub(crate) async fn refresh_info(inner: &Arc<Inner>, serial: &str) -> Result<()> {
    ensure_adb(inner, serial)?;
    let addr = inner.config().adb_addr();
    let out = sh(&addr, serial, parse::INFO_COMMAND, &CancellationToken::new()).await?;
    let info = parse::parse_info(&out.stdout);
    let changed = inner.registry.lock().unwrap().set_info(serial, info);
    if changed {
        let devices = inner.registry.lock().unwrap().snapshot();
        inner.emit(crate::api::Event::DevicesChanged { devices });
    }
    Ok(())
}

pub(crate) async fn read_imei(ctx: &JobCtx, serial: &str) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let out = sh(&ctx.addr(), serial, parse::IMEI_COMMAND, &ctx.cancel).await?;
    let imei = parse::parse_parcel_string(&out.stdout);
    let msg = imei.clone().unwrap_or_else(|| "Not readable on this device".into());
    Ok(DeviceOk::with(msg, json!({ "imei": imei })))
}

pub(crate) async fn shell_job(ctx: &JobCtx, serial: &str, command: &str) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let out = shell_stream(ctx, serial, command).await?;
    let data = json!({ "exit_code": out.exit_code, "stdout": out.stdout, "stderr": out.stderr });
    match out.exit_code {
        Some(0) | None => Ok(DeviceOk::with("Done", data)),
        Some(c) => Err(Error::Fail(format!("exited with code {c}"))),
    }
}

async fn shell_stream(ctx: &JobCtx, serial: &str, command: &str) -> Result<ShellOutput> {
    let addr = ctx.addr();
    let v2 = supports_v2(&addr, serial).await;
    shell::run(&addr, serial, command, v2, &ctx.cancel, |stream, text| ctx.output(serial, stream, text)).await
}

pub(crate) async fn simple_shell(ctx: &JobCtx, serial: &str, command: &str, ok: &str) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let out = sh(&ctx.addr(), serial, command, &ctx.cancel).await?;
    let text = out.combined();
    // shell v1 has no exit code; tools print errors on the same stream.
    let v1_failed = out.exit_code.is_none() && !text.trim().is_empty();
    if !out.succeeded() || v1_failed {
        return Err(Error::Fail(last_line(&text)));
    }
    Ok(DeviceOk::msg(ok))
}

fn last_line(s: &str) -> String {
    s.lines().map(str::trim).rfind(|l| !l.is_empty()).unwrap_or("failed").to_string()
}

// ------------------------------------------------------------------ apps ---

pub(crate) async fn install(ctx: &JobCtx, serial: &str, apk: &Path, allow_downgrade: bool) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let meta = tokio::fs::metadata(apk).await?;
    let safe: String = serial.chars().map(|c| if c.is_ascii_alphanumeric() { c } else { '_' }).collect();
    let tmp = format!("/data/local/tmp/adbm-{}-{safe}.apk", ctx.id);
    let addr = ctx.addr();
    {
        let mut sync = Sync::open(&addr, serial).await?;
        let file = tokio::fs::File::open(apk).await?;
        let total = meta.len();
        let mut last = Instant::now() - PROGRESS_EVERY;
        sync.send(file, &tmp, 0o644, mtime(&meta), &ctx.cancel, |sent| {
            if last.elapsed() >= PROGRESS_EVERY || sent == total {
                last = Instant::now();
                ctx.progress(Some(serial), sent, total, "Copying");
            }
        })
        .await?;
        let _ = sync.quit().await;
    }
    ctx.progress(Some(serial), meta.len(), meta.len(), "Installing");
    let flags = if allow_downgrade { "-r -d" } else { "-r" };
    let out = sh(&addr, serial, &format!("pm install {flags} {}", quote(&tmp)), &ctx.cancel).await;
    let _ = sh(&addr, serial, &format!("rm -f {}", quote(&tmp)), &CancellationToken::new()).await;
    let text = out?.combined();
    if text.lines().any(|l| l.trim() == "Success") {
        return Ok(DeviceOk::msg("Installed"));
    }
    Err(Error::Fail(install_failure(&text)))
}

/// Turn `pm install` output into a message with a fix where we know one.
fn install_failure(text: &str) -> String {
    let line = text
        .lines()
        .map(str::trim)
        .find(|l| l.starts_with("Failure") || l.contains("INSTALL_"))
        .unwrap_or_else(|| text.lines().map(str::trim).rfind(|l| !l.is_empty()).unwrap_or("Install failed"));
    let code = line.split(['[', ':', ' ', ']']).find(|t| t.starts_with("INSTALL_")).unwrap_or("");
    let fix = match code {
        "INSTALL_FAILED_VERSION_DOWNGRADE" => " Uninstall the newer version first, or allow downgrade.",
        "INSTALL_FAILED_UPDATE_INCOMPATIBLE" => {
            " The installed app is signed with a different key. Uninstall it first."
        }
        "INSTALL_FAILED_INSUFFICIENT_STORAGE" => " Free up space on the device.",
        "INSTALL_FAILED_OLDER_SDK" => " The device's Android version is too old for this app.",
        "INSTALL_FAILED_USER_RESTRICTED" => " Allow installs over USB in the device's developer options.",
        _ => "",
    };
    format!("{line}{fix}")
}

pub(crate) async fn uninstall(ctx: &JobCtx, serial: &str, package: &str, keep_data: bool) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let k = if keep_data { "-k " } else { "" };
    let out = sh(&ctx.addr(), serial, &format!("pm uninstall {k}{}", quote(package)), &ctx.cancel).await?;
    let text = out.combined();
    if text.lines().any(|l| l.trim() == "Success") {
        Ok(DeviceOk::msg("Uninstalled"))
    } else if text.contains("Unknown package")
        || text.contains("not installed")
        || text.contains("DELETE_FAILED_INTERNAL_ERROR")
    {
        Err(Error::Fail(format!("{package} is not installed")))
    } else {
        Err(Error::Fail(last_line(&text)))
    }
}

pub(crate) async fn list_packages(ctx: &JobCtx, serial: &str, include_system: bool) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let addr = ctx.addr();
    let mut pkgs = parse::parse_packages(&sh(&addr, serial, "pm list packages -3", &ctx.cancel).await?.stdout, false);
    if include_system {
        pkgs.extend(parse::parse_packages(&sh(&addr, serial, "pm list packages -s", &ctx.cancel).await?.stdout, true));
        pkgs.sort_by(|a, b| a.name.cmp(&b.name));
    }
    let n = pkgs.len();
    Ok(DeviceOk::with(count(n, "app", "apps"), serde_json::to_value(pkgs).unwrap_or_default()))
}

// ----------------------------------------------------------------- power ---

pub(crate) async fn reboot(ctx: &JobCtx, serial: &str, mode: RebootMode, wait_leave: bool) -> Result<DeviceOk> {
    let state = ctx.inner.state(serial).ok_or_else(|| Error::DeviceNotFound(serial.into()))?;
    if state.is_fastboot() {
        ctx.inner.fastboot()?.reboot(serial, mode).await?;
    } else if matches!(state, DeviceState::Unauthorized | DeviceState::Offline | DeviceState::NoPermissions) {
        return Err(Error::DeviceNotReady {
            serial: ctx.inner.display_name(serial),
            state: state.label().to_lowercase(),
        });
    } else {
        let target = match mode {
            RebootMode::System => "",
            RebootMode::Recovery => "recovery",
            RebootMode::Bootloader => "bootloader",
            RebootMode::Fastboot => "fastboot",
            RebootMode::Sideload => "sideload",
        };
        wire::reboot(&ctx.addr(), serial, target).await?;
    }
    ctx.inner.mark_rebooting(serial);
    if wait_leave {
        // So a following "wait for" step doesn't see the old state.
        let deadline = Instant::now() + Duration::from_secs(20);
        while ctx.inner.state(serial) == Some(state) && Instant::now() < deadline {
            tokio::time::sleep(Duration::from_millis(250)).await;
        }
    }
    Ok(DeviceOk::msg(format!("Rebooting to {}", mode.label())))
}

pub(crate) async fn fastboot_boot(ctx: &JobCtx, serial: &str, image: &Path) -> Result<DeviceOk> {
    match ctx.inner.state(serial) {
        Some(s) if s.is_fastboot() => {}
        Some(s) => {
            return Err(Error::DeviceNotReady {
                serial: ctx.inner.display_name(serial),
                state: format!("{} (reboot to Bootloader first)", s.label().to_lowercase()),
            })
        }
        None => return Err(Error::DeviceNotFound(serial.into())),
    }
    if !image.is_file() {
        return Err(Error::Invalid(format!("{} does not exist", image.display())));
    }
    ctx.progress(Some(serial), 0, 0, "Sending image");
    ctx.inner.fastboot()?.boot(serial, image).await?;
    Ok(DeviceOk::msg(format!("Booted {}", file_name(image))))
}

pub(crate) async fn wait_for(ctx: &JobCtx, serial: &str, target: WaitTarget, timeout: Duration) -> Result<()> {
    let deadline = Instant::now() + timeout;
    loop {
        let st = ctx.inner.state(serial);
        let reached = match (target, st) {
            (WaitTarget::System, Some(DeviceState::Online)) => true,
            (WaitTarget::Recovery, Some(DeviceState::Recovery)) => true,
            (WaitTarget::Fastboot, Some(s)) => s.is_fastboot(),
            (WaitTarget::Adb, Some(s)) => s.is_adb_usable(),
            _ => false,
        };
        if reached {
            return Ok(());
        }
        if Instant::now() >= deadline {
            let now = st.map(|s| s.label().to_lowercase()).unwrap_or_else(|| "disconnected".into());
            return Err(Error::Timeout(format!("still {now} after {} s", timeout.as_secs())));
        }
        tokio::select! {
            _ = ctx.cancel.cancelled() => return Err(Error::Cancelled),
            _ = tokio::time::sleep(Duration::from_millis(300)) => {}
        }
    }
}

// ----------------------------------------------------------------- files ---

pub(crate) async fn list_dir(ctx: &JobCtx, serial: &str, path: &str) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let mut sync = Sync::open(&ctx.addr(), serial).await?;
    let entries = sync.list(path).await?;
    let _ = sync.quit().await;
    let msg = count(entries.len(), "item", "items");
    Ok(DeviceOk::with(msg, json!({ "path": path, "entries": entries })))
}

fn mtime(m: &std::fs::Metadata) -> u32 {
    m.modified()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| d.as_secs() as u32)
        .unwrap_or(0)
}

struct LocalTree {
    dirs: Vec<String>,
    files: Vec<(PathBuf, String, u64)>,
}

fn walk(root: &Path) -> std::io::Result<LocalTree> {
    let mut t = LocalTree { dirs: vec![], files: vec![] };
    let mut stack = vec![(root.to_path_buf(), String::new())];
    while let Some((dir, rel)) = stack.pop() {
        for e in std::fs::read_dir(&dir)? {
            let e = e?;
            let name = e.file_name().to_string_lossy().into_owned();
            let r = if rel.is_empty() { name } else { format!("{rel}/{name}") };
            let ft = e.file_type()?;
            if ft.is_dir() {
                t.dirs.push(r.clone());
                stack.push((e.path(), r));
            } else if ft.is_file() {
                t.files.push((e.path(), r, e.metadata()?.len()));
            }
        }
    }
    t.dirs.sort();
    t.files.sort_by(|a, b| a.1.cmp(&b.1));
    Ok(t)
}

pub(crate) async fn push(ctx: &JobCtx, serial: &str, local: &Path, remote: &str) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let meta = tokio::fs::metadata(local).await.map_err(|e| Error::Invalid(format!("{}: {e}", local.display())))?;
    let addr = ctx.addr();
    let mut sync = Sync::open(&addr, serial).await?;
    let rstat = sync.stat(remote).await?;
    let target = if (rstat.exists() && rstat.is_dir()) || remote.ends_with('/') {
        remote_join(remote, &file_name(local))
    } else {
        remote.to_string()
    };

    if meta.is_file() {
        let total = meta.len();
        let mut last = Instant::now() - PROGRESS_EVERY;
        let f = tokio::fs::File::open(local).await?;
        sync.send(f, &target, 0o644, mtime(&meta), &ctx.cancel, |sent| {
            if last.elapsed() >= PROGRESS_EVERY || sent == total {
                last = Instant::now();
                ctx.progress(Some(serial), sent, total, file_name(local));
            }
        })
        .await?;
        let _ = sync.quit().await;
        return Ok(DeviceOk::msg(format!("Pushed to {target}")));
    }

    let root = local.to_path_buf();
    let tree = tokio::task::spawn_blocking(move || walk(&root)).await.map_err(|e| Error::Invalid(e.to_string()))??;
    // Create the folder structure (covers empty folders; SEND creates parents itself).
    let mut dirs = vec![quote(&target)];
    dirs.extend(tree.dirs.iter().map(|d| quote(&remote_join(&target, d))));
    for chunk in dirs.chunks(200) {
        let out = sh(&addr, serial, &format!("mkdir -p {}", chunk.join(" ")), &ctx.cancel).await?;
        if !out.succeeded() {
            return Err(Error::Fail(last_line(&out.combined())));
        }
    }
    let total: u64 = tree.files.iter().map(|f| f.2).sum();
    let mut done = 0u64;
    let mut last = Instant::now() - PROGRESS_EVERY;
    for (path, rel, size) in &tree.files {
        let f = tokio::fs::File::open(path).await?;
        let m = f.metadata().await?;
        let base = done;
        sync.send(f, &remote_join(&target, rel), 0o644, mtime(&m), &ctx.cancel, |sent| {
            if last.elapsed() >= PROGRESS_EVERY {
                last = Instant::now();
                ctx.progress(Some(serial), base + sent, total, rel.clone());
            }
        })
        .await
        .map_err(|e| Error::Fail(format!("{rel}: {e}")))?;
        done += size;
    }
    ctx.progress(Some(serial), total, total, "Done");
    let _ = sync.quit().await;
    Ok(DeviceOk::msg(format!("Pushed {} to {target}", count(tree.files.len(), "file", "files"))))
}

pub(crate) async fn pull(ctx: &JobCtx, serial: &str, remote: &str, local: &Path) -> Result<DeviceOk> {
    ensure_adb(&ctx.inner, serial)?;
    let mut sync = Sync::open(&ctx.addr(), serial).await?;
    let st = sync.stat(remote).await?;
    if !st.exists() {
        return Err(Error::Fail(format!("{remote} does not exist on the device")));
    }
    let base_name =
        remote.trim_end_matches('/').rsplit('/').next().filter(|s| !s.is_empty()).unwrap_or("root").to_string();
    let dest = if local.is_dir() { local.join(&base_name) } else { local.to_path_buf() };

    let mut files = 0usize;
    let mut bytes = 0u64;
    let mut last = Instant::now() - PROGRESS_EVERY;
    if !st.is_dir() {
        recv_file(&mut sync, remote, &dest, &ctx.cancel).await?;
        let _ = sync.quit().await;
        return Ok(DeviceOk::msg(format!("Saved to {}", dest.display())));
    }

    let mut stack = vec![(remote.trim_end_matches('/').to_string(), dest.clone())];
    tokio::fs::create_dir_all(&dest).await?;
    while let Some((rdir, ldir)) = stack.pop() {
        let rdir = if rdir.is_empty() { "/".to_string() } else { rdir };
        for e in sync.list(&rdir).await? {
            let r = remote_join(&rdir, &e.name);
            let l = ldir.join(&e.name);
            match e.kind {
                crate::api::EntryKind::Dir => {
                    tokio::fs::create_dir_all(&l).await?;
                    stack.push((r, l));
                }
                crate::api::EntryKind::File => {
                    recv_file(&mut sync, &r, &l, &ctx.cancel).await?;
                    files += 1;
                    bytes += e.size;
                    if last.elapsed() >= PROGRESS_EVERY {
                        last = Instant::now();
                        ctx.progress(Some(serial), bytes, 0, format!("{} copied", count(files, "file", "files")));
                    }
                }
                _ => {}
            }
        }
    }
    let _ = sync.quit().await;
    Ok(DeviceOk::msg(format!("Saved {} to {}", count(files, "file", "files"), dest.display())))
}

async fn recv_file(sync: &mut Sync, remote: &str, local: &Path, cancel: &CancellationToken) -> Result<()> {
    if let Some(parent) = local.parent() {
        tokio::fs::create_dir_all(parent).await?;
    }
    let f = tokio::fs::File::create(local).await?;
    sync.recv(remote, f, cancel, |_| {}).await.map_err(|e| Error::Fail(format!("{remote}: {e}")))?;
    Ok(())
}

// -------------------------------------------------------------- routines ---

pub(crate) async fn run_routine(ctx: &JobCtx, serial: &str, routine: &Routine) -> Result<DeviceOk> {
    let n = routine.steps.len() as u64;
    for (i, step) in routine.steps.iter().enumerate() {
        let title = step.title();
        ctx.progress(Some(serial), i as u64, n, title.clone());
        let r: Result<()> = async {
            match step {
                Step::Install { path } => install(ctx, serial, Path::new(path), false).await.map(drop),
                Step::Uninstall { package } => uninstall(ctx, serial, package, false).await.map(drop),
                Step::Shell { command, ignore_failure } => {
                    ensure_adb(&ctx.inner, serial)?;
                    let out = shell_stream(ctx, serial, command).await?;
                    match out.exit_code {
                        Some(c) if c != 0 && !ignore_failure => Err(Error::Fail(format!("exited with code {c}"))),
                        _ => Ok(()),
                    }
                }
                Step::Reboot { mode } => reboot(ctx, serial, *mode, true).await.map(drop),
                Step::Push { local, remote } => push(ctx, serial, Path::new(local), remote).await.map(drop),
                Step::Pull { remote, local } => {
                    // One folder per device so several devices don't overwrite each other.
                    let dir = Path::new(local).join(serial.replace([':', '/', '\\'], "_"));
                    tokio::fs::create_dir_all(&dir).await?;
                    pull(ctx, serial, remote, &dir).await.map(drop)
                }
                Step::WaitFor { target, timeout_s } => {
                    wait_for(ctx, serial, *target, Duration::from_secs(*timeout_s)).await
                }
                Step::FastbootBoot { image } => fastboot_boot(ctx, serial, Path::new(image)).await.map(drop),
                Step::Delay { ms } => {
                    tokio::select! {
                        _ = ctx.cancel.cancelled() => Err(Error::Cancelled),
                        _ = tokio::time::sleep(Duration::from_millis(*ms)) => Ok(()),
                    }
                }
            }
        }
        .await;
        if let Err(e) = r {
            if matches!(e, Error::Cancelled) {
                return Err(e);
            }
            return Err(Error::Fail(format!("Step {} ({title}): {e}", i + 1)));
        }
    }
    ctx.progress(Some(serial), n, n, "Done");
    Ok(DeviceOk::msg(format!("All {} completed", count(n as usize, "step", "steps"))))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn install_failure_messages() {
        let m = install_failure(
            "Performing Streamed Install\nFailure [INSTALL_FAILED_VERSION_DOWNGRADE: Downgrade detected]\n",
        );
        assert!(m.starts_with("Failure [INSTALL_FAILED_VERSION_DOWNGRADE"));
        assert!(m.ends_with("allow downgrade."));
        assert_eq!(install_failure("weird\n"), "weird");
    }

    #[test]
    fn walks_local_tree() {
        let d = tempfile::tempdir().unwrap();
        std::fs::create_dir_all(d.path().join("a/b")).unwrap();
        std::fs::create_dir_all(d.path().join("empty")).unwrap();
        std::fs::write(d.path().join("a/b/f.txt"), b"hi").unwrap();
        std::fs::write(d.path().join("top.bin"), b"1234").unwrap();
        let t = walk(d.path()).unwrap();
        assert_eq!(t.dirs, ["a", "a/b", "empty"]);
        let files: Vec<_> = t.files.iter().map(|f| (f.1.as_str(), f.2)).collect();
        assert_eq!(files, [("a/b/f.txt", 2), ("top.bin", 4)]);
    }
}
