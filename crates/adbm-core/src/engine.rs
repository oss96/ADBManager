//! The `Core` handle: owns the tokio runtime, device monitors and jobs.
//!
//! UIs talk to it with [`Core::call`] (never blocks on I/O) and read results
//! from the event queue ([`Core::next_event`] or [`Core::events`]).

use std::collections::HashMap;
use std::future::Future;
use std::path::PathBuf;
use std::pin::Pin;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex, RwLock};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use serde_json::Value;
use tokio::sync::Semaphore;
use tokio::task::JoinSet;
use tokio_util::sync::CancellationToken;

use crate::adb::{self, wire};
use crate::api::*;
use crate::error::{Error, Result};
use crate::fastboot::Fastboot;
use crate::registry::Registry;
use crate::{apk, ops, routines, tools};

const KEEP_FINISHED_JOBS: usize = 100;

pub struct Core {
    rt: Option<tokio::runtime::Runtime>,
    inner: Arc<Inner>,
    events: flume::Receiver<Event>,
}

struct JobEntry {
    info: JobInfo,
    cancel: CancellationToken,
}

pub(crate) struct Inner {
    config: RwLock<Config>,
    tx: flume::Sender<Event>,
    pub(crate) registry: Mutex<Registry>,
    server: Mutex<ServerStatus>,
    jobs: Mutex<HashMap<JobId, JobEntry>>,
    next_job: AtomicU64,
    limiter: RwLock<Arc<Semaphore>>,
    shutdown: CancellationToken,
    monitors: Mutex<CancellationToken>,
    handle: tokio::runtime::Handle,
}

impl Core {
    pub fn new(config: Config) -> Result<Self> {
        let rt = tokio::runtime::Builder::new_multi_thread()
            .worker_threads(4)
            .thread_name("adbm-core")
            .enable_all()
            .build()?;
        let (tx, rx) = flume::unbounded();
        let inner = Arc::new(Inner {
            limiter: RwLock::new(Arc::new(Semaphore::new(config.max_concurrency.max(1)))),
            config: RwLock::new(config),
            tx,
            registry: Mutex::new(Registry::default()),
            server: Mutex::new(ServerStatus::Starting),
            jobs: Mutex::new(HashMap::new()),
            next_job: AtomicU64::new(1),
            shutdown: CancellationToken::new(),
            monitors: Mutex::new(CancellationToken::new()),
            handle: rt.handle().clone(),
        });
        inner.start_monitors();
        Ok(Self { rt: Some(rt), inner, events: rx })
    }

    /// A receiver for events. Every clone competes for the same events, so
    /// a UI should use exactly one.
    pub fn events(&self) -> flume::Receiver<Event> {
        self.events.clone()
    }

    pub fn next_event(&self, timeout: Duration) -> Option<Event> {
        self.events.recv_timeout(timeout).ok()
    }

    pub fn call(&self, cmd: Command) -> Response {
        let _guard = self.inner.handle.enter();
        match self.inner.dispatch(cmd) {
            Ok(r) => r,
            Err(e) => Response::error(e),
        }
    }

    /// Stop monitors and jobs. Called automatically on drop.
    pub fn shutdown(mut self) {
        self.stop();
    }

    fn stop(&mut self) {
        self.inner.shutdown.cancel();
        if let Some(rt) = self.rt.take() {
            rt.shutdown_timeout(Duration::from_millis(500));
        }
    }
}

impl Drop for Core {
    fn drop(&mut self) {
        self.stop();
    }
}

pub(crate) fn now_ms() -> u64 {
    SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_millis() as u64).unwrap_or(0)
}

pub(crate) fn count(n: usize, one: &str, many: &str) -> String {
    if n == 1 {
        format!("1 {one}")
    } else {
        format!("{n} {many}")
    }
}

// ------------------------------------------------------------------ jobs ---

pub(crate) type BoxFut<T> = Pin<Box<dyn Future<Output = T> + Send + 'static>>;

/// What a device operation returns on success.
pub(crate) struct DeviceOk {
    pub message: String,
    pub data: Option<Value>,
}

impl DeviceOk {
    pub fn msg(m: impl Into<String>) -> Self {
        Self { message: m.into(), data: None }
    }
    pub fn with(m: impl Into<String>, data: Value) -> Self {
        Self { message: m.into(), data: Some(data) }
    }
}

pub(crate) struct JobEnd {
    status: JobStatus,
    summary: String,
    data: Option<Value>,
}

#[derive(Clone)]
pub(crate) struct JobCtx {
    pub id: JobId,
    pub inner: Arc<Inner>,
    pub cancel: CancellationToken,
}

impl JobCtx {
    pub fn progress(&self, serial: Option<&str>, done: u64, total: u64, message: impl Into<String>) {
        self.inner.emit(Event::JobProgress {
            job_id: self.id,
            serial: serial.map(str::to_string),
            done,
            total,
            message: message.into(),
        });
    }

    pub fn output(&self, serial: &str, stream: Stream, text: &str) {
        self.inner.emit(Event::JobOutput { job_id: self.id, serial: serial.into(), stream, text: text.into() });
    }

    pub fn addr(&self) -> String {
        self.inner.config().adb_addr()
    }
}

impl Inner {
    pub(crate) fn emit(&self, e: Event) {
        let _ = self.tx.send(e);
    }

    pub(crate) fn log(&self, level: LogLevel, message: impl Into<String>) {
        let message = message.into();
        match level {
            LogLevel::Error => tracing::error!("{message}"),
            LogLevel::Warn => tracing::warn!("{message}"),
            LogLevel::Info => tracing::info!("{message}"),
            LogLevel::Debug => tracing::debug!("{message}"),
        }
        self.emit(Event::Log { level, message });
    }

    pub(crate) fn config(&self) -> Config {
        self.config.read().unwrap().clone()
    }

    fn limiter(&self) -> Arc<Semaphore> {
        self.limiter.read().unwrap().clone()
    }

    pub(crate) fn display_name(&self, serial: &str) -> String {
        self.registry
            .lock()
            .unwrap()
            .snapshot()
            .into_iter()
            .find(|d| d.serial == serial)
            .map(|d| d.display_name)
            .unwrap_or_else(|| serial.to_string())
    }

    pub(crate) fn state(&self, serial: &str) -> Option<DeviceState> {
        self.registry.lock().unwrap().state(serial)
    }

    pub(crate) fn fastboot(&self) -> Result<Fastboot> {
        let cfg = self.config();
        tools::locate("fastboot", cfg.fastboot_path.as_deref()).map(Fastboot::new).ok_or(Error::FastbootNotFound)
    }

    pub(crate) fn mark_rebooting(&self, serial: &str) {
        self.registry.lock().unwrap().mark_rebooting(serial);
    }

    fn emit_devices(&self) {
        let devices = self.registry.lock().unwrap().snapshot();
        self.emit(Event::DevicesChanged { devices });
    }

    fn set_server(&self, status: ServerStatus) {
        let mut s = self.server.lock().unwrap();
        if *s != status {
            *s = status.clone();
            drop(s);
            self.emit(Event::Server { status });
        }
    }

    fn start_job(
        self: &Arc<Self>,
        title: String,
        serials: Vec<String>,
        visible: bool,
        run: impl FnOnce(JobCtx) -> BoxFut<JobEnd>,
    ) -> JobId {
        let id = self.next_job.fetch_add(1, Ordering::Relaxed);
        let cancel = self.shutdown.child_token();
        let info = JobInfo { id, title, serials, visible, started_at_ms: now_ms(), status: JobStatus::Running };
        self.jobs.lock().unwrap().insert(id, JobEntry { info: info.clone(), cancel: cancel.clone() });
        self.emit(Event::JobStarted { job: info });
        let ctx = JobCtx { id, inner: self.clone(), cancel: cancel.clone() };
        let fut = run(ctx);
        let inner = self.clone();
        self.handle.spawn(async move {
            let mut end = fut.await;
            if cancel.is_cancelled() && end.status != JobStatus::Succeeded {
                end.status = JobStatus::Cancelled;
                end.summary = "Cancelled".into();
            }
            inner.finish_job(id, end);
        });
        id
    }

    fn finish_job(&self, id: JobId, end: JobEnd) {
        {
            let mut jobs = self.jobs.lock().unwrap();
            if let Some(j) = jobs.get_mut(&id) {
                j.info.status = end.status;
            }
            let finished: Vec<(u64, JobId)> = jobs
                .values()
                .filter(|j| j.info.status != JobStatus::Running)
                .map(|j| (j.info.started_at_ms, j.info.id))
                .collect();
            if finished.len() > KEEP_FINISHED_JOBS {
                let mut finished = finished;
                finished.sort();
                for (_, old) in &finished[..finished.len() - KEEP_FINISHED_JOBS] {
                    jobs.remove(old);
                }
            }
        }
        self.emit(Event::JobFinished { job_id: id, status: end.status, summary: end.summary, data: end.data });
    }

    /// A job running `op` on every serial in parallel, bounded by the
    /// concurrency limit. One device failing does not stop the others.
    fn fanout<F, Fut>(self: &Arc<Self>, title: String, serials: Vec<String>, op: F) -> JobId
    where
        F: Fn(JobCtx, String) -> Fut + Send + Sync + 'static,
        Fut: Future<Output = Result<DeviceOk>> + Send + 'static,
    {
        let op = Arc::new(op);
        let list = serials.clone();
        self.start_job(title, serials, true, move |ctx| {
            Box::pin(async move {
                let sem = ctx.inner.limiter();
                let mut set = JoinSet::new();
                for serial in list {
                    let (ctx, op, sem) = (ctx.clone(), op.clone(), sem.clone());
                    set.spawn(async move {
                        let r = tokio::select! {
                            _ = ctx.cancel.cancelled() => Err(Error::Cancelled),
                            r = async {
                                let _permit = sem.acquire_owned().await;
                                op(ctx.clone(), serial.clone()).await
                            } => r,
                        };
                        (serial, r)
                    });
                }
                let (mut ok, mut failed) = (0usize, 0usize);
                let mut data = serde_json::Map::new();
                let mut first_error = None;
                while let Some(joined) = set.join_next().await {
                    let (serial, r) = joined.unwrap_or_else(|e| ("unknown".into(), Err(Error::Invalid(e.to_string()))));
                    let (is_ok, message) = match r {
                        Ok(d) => {
                            ok += 1;
                            if let Some(v) = d.data {
                                data.insert(serial.clone(), v);
                            }
                            (true, d.message)
                        }
                        Err(e) => {
                            failed += 1;
                            let m = e.to_string();
                            first_error.get_or_insert_with(|| format!("{}: {m}", ctx.inner.display_name(&serial)));
                            (false, m)
                        }
                    };
                    ctx.inner.emit(Event::JobDeviceFinished { job_id: ctx.id, serial, ok: is_ok, message });
                }
                let (status, summary) = match (ok, failed) {
                    (_, 0) => (JobStatus::Succeeded, format!("{} succeeded", count(ok, "device", "devices"))),
                    (0, 1) => (JobStatus::Failed, first_error.unwrap_or_default()),
                    (0, n) => {
                        (JobStatus::Failed, format!("Failed on all {n} devices. {}", first_error.unwrap_or_default()))
                    }
                    (o, f) => (JobStatus::PartiallyFailed, format!("{o} succeeded, {f} failed")),
                };
                JobEnd { status, summary, data: (!data.is_empty()).then_some(Value::Object(data)) }
            })
        })
    }

    /// A job with one result. `data` is delivered in `JobFinished`.
    fn single<Fut>(
        self: &Arc<Self>,
        title: String,
        serials: Vec<String>,
        visible: bool,
        fut: impl FnOnce(JobCtx) -> Fut,
    ) -> JobId
    where
        Fut: Future<Output = Result<DeviceOk>> + Send + 'static,
    {
        self.start_job(title, serials, visible, move |ctx| {
            let cancel = ctx.cancel.clone();
            let f = fut(ctx);
            Box::pin(async move {
                let r = tokio::select! {
                    _ = cancel.cancelled() => Err(Error::Cancelled),
                    r = f => r,
                };
                match r {
                    Ok(d) => JobEnd { status: JobStatus::Succeeded, summary: d.message, data: d.data },
                    Err(e) => JobEnd { status: JobStatus::Failed, summary: e.to_string(), data: None },
                }
            })
        })
    }

    // ------------------------------------------------------------ dispatch ---

    fn dispatch(self: &Arc<Self>, cmd: Command) -> Result<Response> {
        fn need(serials: &[String]) -> Result<()> {
            if serials.is_empty() {
                return Err(Error::Invalid("Select one or more devices.".into()));
            }
            Ok(())
        }
        let job = |id| Ok(Response::Job { job_id: id });
        match cmd {
            Command::GetState => {
                let mut jobs: Vec<JobInfo> = self.jobs.lock().unwrap().values().map(|j| j.info.clone()).collect();
                jobs.sort_by_key(|j| j.id);
                Ok(Response::State {
                    devices: self.registry.lock().unwrap().snapshot(),
                    jobs,
                    server: self.server.lock().unwrap().clone(),
                    config: self.config(),
                    core_version: crate::VERSION.to_string(),
                })
            }
            Command::SetConfig { config } => {
                *self.limiter.write().unwrap() = Arc::new(Semaphore::new(config.max_concurrency.max(1)));
                *self.config.write().unwrap() = config;
                self.start_monitors();
                Ok(Response::Ok)
            }
            Command::RestartServer => job(self.single("Restart adb".into(), vec![], true, |ctx| async move {
                let addr = ctx.addr();
                wire::kill_server(&addr).await?;
                ctx.inner.set_server(ServerStatus::Starting);
                for _ in 0..40 {
                    tokio::time::sleep(Duration::from_millis(500)).await;
                    if let Ok(v) = wire::server_version(&addr).await {
                        return Ok(DeviceOk::msg(format!("adb server restarted (protocol {v})")));
                    }
                }
                Err(Error::Timeout("adb server did not come back within 20 s".into()))
            })),
            Command::Connect { address } => {
                let address = normalize_tcp(&address)?;
                job(self.single(format!("Connect to {address}"), vec![], true, move |ctx| async move {
                    wire::connect_tcp(&ctx.addr(), &address).await.map(DeviceOk::msg)
                }))
            }
            Command::Disconnect { address } => {
                job(self.single(format!("Disconnect {address}"), vec![address.clone()], true, move |ctx| async move {
                    wire::disconnect_tcp(&ctx.addr(), &address).await.map(DeviceOk::msg)
                }))
            }
            Command::RefreshDevice { serial } => {
                let s = serial.clone();
                job(self.single(format!("Refresh {serial}"), vec![serial], false, move |ctx| async move {
                    ops::refresh_info(&ctx.inner, &s).await.map(|_| DeviceOk::msg("Refreshed"))
                }))
            }
            Command::ReadImei { serial } => {
                let s = serial.clone();
                job(self.single(format!("Read IMEI of {serial}"), vec![serial], false, move |ctx| async move {
                    ops::read_imei(&ctx, &s).await
                }))
            }
            Command::InspectApk { path } => Ok(Response::Apk { apk: apk::inspect(&path)? }),
            Command::Install { serials, path, allow_downgrade } => {
                need(&serials)?;
                let name = sync_name(&path);
                let title = format!("Install {name} on {}", count(serials.len(), "device", "devices"));
                let path = PathBuf::from(path);
                job(self.fanout(title, serials, move |ctx, s| {
                    let path = path.clone();
                    async move { ops::install(&ctx, &s, &path, allow_downgrade).await }
                }))
            }
            Command::Uninstall { serials, package, keep_data } => {
                need(&serials)?;
                let title = format!("Uninstall {package} from {}", count(serials.len(), "device", "devices"));
                job(self.fanout(title, serials, move |ctx, s| {
                    let package = package.clone();
                    async move { ops::uninstall(&ctx, &s, &package, keep_data).await }
                }))
            }
            Command::ListPackages { serial, include_system } => {
                let s = serial.clone();
                job(self.single(format!("List apps on {serial}"), vec![serial], false, move |ctx| async move {
                    ops::list_packages(&ctx, &s, include_system).await
                }))
            }
            Command::Reboot { serials, mode } => {
                need(&serials)?;
                let title = format!("Reboot {} to {}", count(serials.len(), "device", "devices"), mode.label());
                job(self.fanout(title, serials, move |ctx, s| async move { ops::reboot(&ctx, &s, mode, false).await }))
            }
            Command::Shell { serials, command } => {
                need(&serials)?;
                if command.trim().is_empty() {
                    return Err(Error::Invalid("Enter a command.".into()));
                }
                let title = format!("Run “{command}” on {}", count(serials.len(), "device", "devices"));
                job(self.fanout(title, serials, move |ctx, s| {
                    let command = command.clone();
                    async move { ops::shell_job(&ctx, &s, &command).await }
                }))
            }
            Command::ListDir { serial, path } => {
                let s = serial.clone();
                job(self.single(format!("List {path}"), vec![serial], false, move |ctx| async move {
                    ops::list_dir(&ctx, &s, &path).await
                }))
            }
            Command::Push { serials, local, remote } => {
                need(&serials)?;
                let title = format!("Push {} to {}", sync_name(&local), count(serials.len(), "device", "devices"));
                let local = PathBuf::from(local);
                job(self.fanout(title, serials, move |ctx, s| {
                    let (local, remote) = (local.clone(), remote.clone());
                    async move { ops::push(&ctx, &s, &local, &remote).await }
                }))
            }
            Command::Pull { serial, remote, local } => {
                let title = format!("Pull {}", sync_name(&remote));
                job(self.fanout(title, vec![serial], move |ctx, s| {
                    let (remote, local) = (remote.clone(), PathBuf::from(&local));
                    async move { ops::pull(&ctx, &s, &remote, &local).await }
                }))
            }
            Command::MakeDir { serials, path } => {
                need(&serials)?;
                job(self.fanout(format!("Create folder {path}"), serials, move |ctx, s| {
                    let path = path.clone();
                    async move {
                        ops::simple_shell(&ctx, &s, &format!("mkdir -p {}", adb::shell::quote(&path)), "Folder created")
                            .await
                    }
                }))
            }
            Command::Delete { serial, path } => {
                if matches!(path.trim(), "" | "/" | "/sdcard" | "/sdcard/" | "/data" | "/system") {
                    return Err(Error::Invalid(format!("Refusing to delete {path}.")));
                }
                job(self.fanout(format!("Delete {path}"), vec![serial], move |ctx, s| {
                    let path = path.clone();
                    async move {
                        ops::simple_shell(&ctx, &s, &format!("rm -rf {}", adb::shell::quote(&path)), "Deleted").await
                    }
                }))
            }
            Command::FastbootBoot { serials, image } => {
                need(&serials)?;
                let title = format!("Boot {} on {}", sync_name(&image), count(serials.len(), "device", "devices"));
                let image = PathBuf::from(image);
                job(self.fanout(title, serials, move |ctx, s| {
                    let image = image.clone();
                    async move { ops::fastboot_boot(&ctx, &s, &image).await }
                }))
            }
            Command::FastbootGetVar { serial, name } => {
                let s = serial.clone();
                job(self.single(format!("getvar {name}"), vec![serial], false, move |ctx| async move {
                    let v = ctx.inner.fastboot()?.getvar(&s, &name).await?;
                    Ok(DeviceOk::with(v.clone().unwrap_or_default(), serde_json::json!({ "name": name, "value": v })))
                }))
            }
            Command::RoutineTemplates => Ok(Response::Routines { routines: routines::templates() }),
            Command::RunRoutine { serials, routine } => {
                need(&serials)?;
                routine.validate().map_err(Error::Invalid)?;
                let title = format!("{} on {}", routine.name, count(serials.len(), "device", "devices"));
                let routine = Arc::new(routine);
                job(self.fanout(title, serials, move |ctx, s| {
                    let routine = routine.clone();
                    async move { ops::run_routine(&ctx, &s, &routine).await }
                }))
            }
            Command::Cancel { job_id } => {
                let jobs = self.jobs.lock().unwrap();
                let j = jobs.get(&job_id).ok_or_else(|| Error::Invalid(format!("No job {job_id}")))?;
                j.cancel.cancel();
                Ok(Response::Ok)
            }
        }
    }

    // ------------------------------------------------------------ monitors ---

    fn start_monitors(self: &Arc<Self>) {
        let token = self.shutdown.child_token();
        let old = std::mem::replace(&mut *self.monitors.lock().unwrap(), token.clone());
        old.cancel();
        self.handle.spawn(adb_monitor(self.clone(), token.clone()));
        self.handle.spawn(fastboot_monitor(self.clone(), token));
    }

    pub(crate) fn spawn_refresh(self: &Arc<Self>, serial: String) {
        let inner = self.clone();
        self.handle.spawn(async move {
            if let Err(e) = ops::refresh_info(&inner, &serial).await {
                tracing::debug!("reading details of {serial} failed: {e}");
            }
        });
    }
}

async fn sleep_or_cancel(d: Duration, cancel: &CancellationToken) -> bool {
    tokio::select! {
        _ = cancel.cancelled() => false,
        _ = tokio::time::sleep(d) => true,
    }
}

async fn adb_monitor(inner: Arc<Inner>, cancel: CancellationToken) {
    let mut announced_start = false;
    while !cancel.is_cancelled() {
        let cfg = inner.config();
        let addr = cfg.adb_addr();
        match wire::server_version(&addr).await {
            Ok(version) => {
                inner.set_server(ServerStatus::Running {
                    version,
                    adb_path: tools::locate("adb", cfg.adb_path.as_deref()).map(|p| p.display().to_string()),
                    fastboot_path: tools::locate("fastboot", cfg.fastboot_path.as_deref())
                        .map(|p| p.display().to_string()),
                });
                let i2 = inner.clone();
                let r = adb::track_devices(&addr, &cancel, move |list| {
                    let changes = i2.registry.lock().unwrap().update_adb(list);
                    if changes.changed {
                        i2.emit_devices();
                    }
                    for s in changes.became_ready {
                        i2.spawn_refresh(s);
                    }
                })
                .await;
                if cancel.is_cancelled() {
                    break;
                }
                if inner.registry.lock().unwrap().update_adb(vec![]).changed {
                    inner.emit_devices();
                }
                if let Err(e) = r {
                    inner.set_server(ServerStatus::Error { message: format!("Lost connection to adb: {e}") });
                }
            }
            Err(_) => match tools::locate("adb", cfg.adb_path.as_deref()) {
                None => {
                    let searched = tools::candidates("adb", cfg.adb_path.as_deref())
                        .into_iter()
                        .map(|p| p.display().to_string())
                        .collect();
                    inner.set_server(ServerStatus::AdbNotFound { searched });
                    if !sleep_or_cancel(Duration::from_secs(3), &cancel).await {
                        break;
                    }
                    continue;
                }
                Some(adb) if cfg.auto_start_server => {
                    inner.set_server(ServerStatus::Starting);
                    if !announced_start {
                        inner.log(LogLevel::Info, format!("Starting adb server ({})", adb.display()));
                        announced_start = true;
                    }
                    if let Err(e) = tools::start_adb_server(&adb, cfg.adb_port).await {
                        inner.set_server(ServerStatus::Error { message: e.to_string() });
                    }
                }
                Some(_) => inner.set_server(ServerStatus::Error {
                    message: format!("The adb server is not running on {addr}. Start it or enable automatic start."),
                }),
            },
        }
        if !sleep_or_cancel(Duration::from_secs(1), &cancel).await {
            break;
        }
    }
}

async fn fastboot_monitor(inner: Arc<Inner>, cancel: CancellationToken) {
    let mut userspace: HashMap<String, bool> = HashMap::new();
    while !cancel.is_cancelled() {
        let cfg = inner.config();
        if let Ok(fb) = inner.fastboot() {
            if let Ok(list) = fb.devices().await {
                let mut resolved = Vec::with_capacity(list.len());
                for (serial, printed_userspace) in list {
                    let u = match userspace.get(&serial) {
                        Some(u) => *u,
                        None => {
                            let u = printed_userspace || fb.is_userspace(&serial).await;
                            userspace.insert(serial.clone(), u);
                            u
                        }
                    };
                    resolved.push((serial, u));
                }
                userspace.retain(|s, _| resolved.iter().any(|(r, _)| r == s));
                if inner.registry.lock().unwrap().update_fastboot(&resolved) {
                    inner.emit_devices();
                }
            }
        }
        if inner.registry.lock().unwrap().tick() {
            inner.emit_devices();
        }
        if !sleep_or_cancel(Duration::from_millis(cfg.fastboot_poll_ms.max(250)), &cancel).await {
            break;
        }
    }
}

fn sync_name(p: &str) -> String {
    p.trim_end_matches(['/', '\\']).rsplit(['/', '\\']).next().unwrap_or(p).to_string()
}

/// Accept `host`, `host:port`; default port 5555.
fn normalize_tcp(a: &str) -> Result<String> {
    let a = a.trim();
    if a.is_empty() || a.contains(char::is_whitespace) {
        return Err(Error::Invalid("Enter an address like 192.168.1.42:5555.".into()));
    }
    Ok(if a.rsplit_once(':').is_some_and(|(_, p)| p.parse::<u16>().is_ok()) {
        a.to_string()
    } else {
        format!("{a}:5555")
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tcp_addresses() {
        assert_eq!(normalize_tcp("192.168.1.4").unwrap(), "192.168.1.4:5555");
        assert_eq!(normalize_tcp(" 10.0.0.2:5037 ").unwrap(), "10.0.0.2:5037");
        assert!(normalize_tcp("").is_err());
    }

    #[test]
    fn names() {
        assert_eq!(sync_name("/home/u/app.apk"), "app.apk");
        assert_eq!(sync_name("C:\\x\\folder\\"), "folder");
        assert_eq!(count(1, "device", "devices"), "1 device");
        assert_eq!(count(3, "device", "devices"), "3 devices");
    }
}
