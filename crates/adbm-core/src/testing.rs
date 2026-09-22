//! An in-process fake adb server speaking the real host protocol, for tests.
//!
//! It understands: `host:version`, `host:track-devices-l`, `host:devices-l`,
//! `host-serial:<s>:features`, `host:connect:`, `host:transport:<s>` followed
//! by `shell:`, `shell,v2,raw:`, `sync:` (STAT/LIST/SEND/RECV/QUIT) and
//! `reboot:`. Shell commands are answered by a small table, see
//! [`FakeAdb::shell_reply`].

use std::collections::{BTreeMap, BTreeSet};
use std::sync::{Arc, Mutex};

use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};
use tokio::sync::watch;

#[derive(Debug, Clone)]
pub struct FakeDevice {
    pub serial: String,
    /// adb state word: device, unauthorized, recovery, ...
    pub state: String,
    pub shell_v2: bool,
    pub model: String,
    pub release: String,
    pub sdk: u32,
    pub battery: u8,
}

#[derive(Default)]
pub struct FakeState {
    pub devices: Vec<FakeDevice>,
    /// Absolute path → contents.
    pub files: BTreeMap<String, Vec<u8>>,
    pub dirs: BTreeSet<String>,
    /// Every shell command received, as `serial: command`.
    pub shell_log: Vec<String>,
    pub reboots: Vec<String>,
}

#[derive(Clone)]
pub struct FakeAdb {
    pub port: u16,
    pub state: Arc<Mutex<FakeState>>,
    changed: watch::Sender<u64>,
}

impl FakeAdb {
    /// Start on an ephemeral port inside the current tokio runtime.
    pub async fn start(devices: Vec<FakeDevice>) -> Self {
        Self::start_on(0, devices).await
    }

    /// Start on a given port (0 = ephemeral).
    pub async fn start_on(port: u16, devices: Vec<FakeDevice>) -> Self {
        let listener = TcpListener::bind(("127.0.0.1", port)).await.expect("bind");
        let port = listener.local_addr().unwrap().port();
        let mut st = FakeState { devices, ..Default::default() };
        for d in ["/", "/sdcard", "/data", "/data/local", "/data/local/tmp", "/system"] {
            st.dirs.insert(d.to_string());
        }
        let (changed, _) = watch::channel(0);
        let fake = FakeAdb { port, state: Arc::new(Mutex::new(st)), changed };
        let f = fake.clone();
        tokio::spawn(async move {
            while let Ok((sock, _)) = listener.accept().await {
                let f = f.clone();
                tokio::spawn(async move {
                    let _ = f.serve(sock).await;
                });
            }
        });
        fake
    }

    /// Start on a background thread with its own runtime; for tests that
    /// drive a synchronous API (`Core`, FFI).
    pub fn start_blocking(devices: Vec<FakeDevice>) -> Self {
        let (tx, rx) = std::sync::mpsc::channel();
        std::thread::spawn(move || {
            let rt = tokio::runtime::Builder::new_multi_thread().worker_threads(2).enable_all().build().unwrap();
            rt.block_on(async move {
                tx.send(FakeAdb::start(devices).await).unwrap();
                std::future::pending::<()>().await;
            });
        });
        rx.recv().unwrap()
    }

    pub fn addr(&self) -> String {
        format!("127.0.0.1:{}", self.port)
    }

    pub fn set_devices(&self, devices: Vec<FakeDevice>) {
        self.state.lock().unwrap().devices = devices;
        self.changed.send_modify(|v| *v += 1);
    }

    pub fn device(serial: &str, state: &str) -> FakeDevice {
        FakeDevice {
            serial: serial.into(),
            state: state.into(),
            shell_v2: true,
            model: "Pixel 8 Pro".into(),
            release: "15".into(),
            sdk: 35,
            battery: 82,
        }
    }

    pub fn with_model(mut d: FakeDevice, model: &str, release: &str, sdk: u32, battery: u8) -> FakeDevice {
        d.model = model.into();
        d.release = release.into();
        d.sdk = sdk;
        d.battery = battery;
        d
    }

    fn list_text(&self) -> String {
        let st = self.state.lock().unwrap();
        st.devices
            .iter()
            .map(|d| {
                // Like real adb: no product/model until the device is authorized.
                if d.state == "device" || d.state == "recovery" {
                    let model = d.model.replace(' ', "_");
                    format!(
                        "{:<22} {} usb:1-1 product:husky model:{model} device:husky transport_id:1\n",
                        d.serial, d.state
                    )
                } else {
                    format!("{:<22} {} usb:1-2 transport_id:2\n", d.serial, d.state)
                }
            })
            .collect()
    }

    /// (stdout, stderr, exit code, hang)
    fn shell_reply(&self, serial: &str, cmd: &str) -> (String, String, u8, bool) {
        let mut st = self.state.lock().unwrap();
        st.shell_log.push(format!("{serial}: {cmd}"));
        if cmd.starts_with("echo manufacturer=") {
            let d = st
                .devices
                .iter()
                .find(|d| d.serial == serial)
                .cloned()
                .unwrap_or_else(|| Self::device(serial, "device"));
            let out = format!(
                "manufacturer=Google\nmodel={}\nrelease={}\nsdk={}\nbuild=AP4A.250105.002\n  status: 2\n  level: {}\n",
                d.model, d.release, d.sdk, d.battery
            );
            return (out, String::new(), 0, false);
        }
        if let Some(rest) = cmd.strip_prefix("pm install ") {
            let path = rest.split_whitespace().last().unwrap_or("").trim_matches('\'');
            return if st.files.contains_key(path) {
                ("Performing Streamed Install\nSuccess\n".into(), String::new(), 0, false)
            } else {
                (String::new(), "Failure [INSTALL_FAILED_INVALID_URI]\n".into(), 1, false)
            };
        }
        if let Some(p) = cmd.strip_prefix("rm -f ") {
            st.files.remove(p.trim_matches('\''));
            return (String::new(), String::new(), 0, false);
        }
        if let Some(rest) = cmd.strip_prefix("mkdir -p ") {
            for d in rest.split_whitespace() {
                let d = d.trim_matches('\'').trim_end_matches('/');
                let mut acc = String::new();
                for part in d.split('/').filter(|p| !p.is_empty()) {
                    acc.push('/');
                    acc.push_str(part);
                    st.dirs.insert(acc.clone());
                }
            }
            return (String::new(), String::new(), 0, false);
        }
        match cmd {
            "pm list packages -3" => {
                ("package:com.example.fieldtools\npackage:com.acme.kiosk\n".into(), String::new(), 0, false)
            }
            "pm list packages -s" => {
                ("package:android\npackage:com.android.settings\n".into(), String::new(), 0, false)
            }
            "pm uninstall com.example.fieldtools" => ("Success\n".into(), String::new(), 0, false),
            "pm uninstall com.missing" => (String::new(), "Failure [DELETE_FAILED_INTERNAL_ERROR]\n".into(), 1, false),
            "hang" => (String::new(), String::new(), 0, true),
            _ if cmd.starts_with("echo ") => (format!("{}\n", &cmd[5..]), String::new(), 0, false),
            _ if cmd.starts_with("exit ") => {
                (String::new(), "boom\n".into(), cmd[5..].trim().parse().unwrap_or(1), false)
            }
            _ => (String::new(), String::new(), 0, false),
        }
    }

    async fn serve(&self, mut s: TcpStream) -> std::io::Result<()> {
        let mut transport: Option<FakeDevice> = None;
        loop {
            let req = match read_req(&mut s).await {
                Ok(r) => r,
                Err(_) => return Ok(()),
            };
            if let Some(dev) = transport.clone() {
                return self.serve_device(s, &dev, &req).await;
            }
            match req.as_str() {
                "host:version" => {
                    s.write_all(b"OKAY").await?;
                    write_str(&mut s, "0029").await?;
                    return Ok(());
                }
                "host:devices-l" => {
                    s.write_all(b"OKAY").await?;
                    write_str(&mut s, &self.list_text()).await?;
                    return Ok(());
                }
                "host:track-devices-l" => {
                    s.write_all(b"OKAY").await?;
                    let mut rx = self.changed.subscribe();
                    loop {
                        write_str(&mut s, &self.list_text()).await?;
                        if rx.changed().await.is_err() {
                            return Ok(());
                        }
                    }
                }
                "host:kill" => {
                    s.write_all(b"OKAY").await?;
                    return Ok(());
                }
                r if r.starts_with("host:connect:") => {
                    s.write_all(b"OKAY").await?;
                    write_str(&mut s, &format!("connected to {}", &r[13..])).await?;
                    return Ok(());
                }
                r if r.starts_with("host-serial:") && r.ends_with(":features") => {
                    let serial = &r[12..r.len() - 9];
                    let v2 = self.find(serial).map(|d| d.shell_v2).unwrap_or(false);
                    s.write_all(b"OKAY").await?;
                    write_str(&mut s, if v2 { "shell_v2,cmd,stat_v2" } else { "cmd" }).await?;
                    return Ok(());
                }
                r if r.starts_with("host:transport:") => match self.find(&r[15..]) {
                    Some(d) if d.state == "device" || d.state == "recovery" => {
                        s.write_all(b"OKAY").await?;
                        transport = Some(d);
                    }
                    Some(d) => return fail(&mut s, &format!("device {}", d.state)).await,
                    None => return fail(&mut s, &format!("device '{}' not found", &r[15..])).await,
                },
                other => return fail(&mut s, &format!("unknown host service {other}")).await,
            }
        }
    }

    fn find(&self, serial: &str) -> Option<FakeDevice> {
        self.state.lock().unwrap().devices.iter().find(|d| d.serial == serial).cloned()
    }

    async fn serve_device(&self, mut s: TcpStream, dev: &FakeDevice, req: &str) -> std::io::Result<()> {
        if let Some(cmd) = req.strip_prefix("shell,v2,raw:") {
            s.write_all(b"OKAY").await?;
            let (out, err, code, hang) = self.shell_reply(&dev.serial, cmd);
            if hang {
                std::future::pending::<()>().await;
            }
            for (id, data) in [(1u8, out.as_bytes()), (2u8, err.as_bytes())] {
                if !data.is_empty() {
                    let mut p = vec![id];
                    p.extend_from_slice(&(data.len() as u32).to_le_bytes());
                    p.extend_from_slice(data);
                    s.write_all(&p).await?;
                }
            }
            s.write_all(&[3, 1, 0, 0, 0, code]).await?;
            return Ok(());
        }
        if let Some(cmd) = req.strip_prefix("shell:") {
            s.write_all(b"OKAY").await?;
            let (out, err, _, hang) = self.shell_reply(&dev.serial, cmd);
            if hang {
                std::future::pending::<()>().await;
            }
            s.write_all(out.as_bytes()).await?;
            s.write_all(err.as_bytes()).await?;
            return Ok(());
        }
        if let Some(target) = req.strip_prefix("reboot:") {
            s.write_all(b"OKAY").await?;
            self.state.lock().unwrap().reboots.push(format!("{}:{target}", dev.serial));
            return Ok(());
        }
        if req == "sync:" {
            s.write_all(b"OKAY").await?;
            return self.serve_sync(s).await;
        }
        fail(&mut s, &format!("unknown device service {req}")).await
    }

    async fn serve_sync(&self, mut s: TcpStream) -> std::io::Result<()> {
        loop {
            let mut h = [0u8; 8];
            if s.read_exact(&mut h).await.is_err() {
                return Ok(());
            }
            let len = u32::from_le_bytes([h[4], h[5], h[6], h[7]]) as usize;
            let mut arg = vec![0u8; len];
            s.read_exact(&mut arg).await?;
            let arg = String::from_utf8_lossy(&arg).into_owned();
            match &h[..4] {
                b"STAT" => {
                    let (mode, size) = {
                        let st = self.state.lock().unwrap();
                        let p = norm(&arg);
                        if st.dirs.contains(&p) {
                            (0o040755u32, 0u32)
                        } else if let Some(f) = st.files.get(&p) {
                            (0o100644, f.len() as u32)
                        } else {
                            (0, 0)
                        }
                    };
                    let mut r = b"STAT".to_vec();
                    for v in [mode, size, 1_700_000_000] {
                        r.extend_from_slice(&v.to_le_bytes());
                    }
                    s.write_all(&r).await?;
                }
                b"LIST" => {
                    let entries: Vec<(String, u32, u32)> = {
                        let st = self.state.lock().unwrap();
                        let dir = norm(&arg);
                        let child = |p: &str| -> Option<String> {
                            let parent = p.rsplit_once('/').map(|(a, _)| if a.is_empty() { "/" } else { a })?;
                            (parent == dir && p != dir).then(|| p.rsplit('/').next().unwrap().to_string())
                        };
                        let mut v: Vec<_> = st.dirs.iter().filter_map(|d| child(d).map(|n| (n, 0o040755, 0))).collect();
                        v.extend(st.files.iter().filter_map(|(p, c)| child(p).map(|n| (n, 0o100644, c.len() as u32))));
                        v
                    };
                    for (name, mode, size) in entries {
                        let mut r = b"DENT".to_vec();
                        for v in [mode, size, 1_700_000_000, name.len() as u32] {
                            r.extend_from_slice(&v.to_le_bytes());
                        }
                        r.extend_from_slice(name.as_bytes());
                        s.write_all(&r).await?;
                    }
                    s.write_all(b"DONE\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0").await?;
                }
                b"SEND" => {
                    let path = norm(arg.rsplit_once(',').map(|(p, _)| p).unwrap_or(&arg));
                    let mut data = Vec::new();
                    loop {
                        let mut h = [0u8; 8];
                        s.read_exact(&mut h).await?;
                        let n = u32::from_le_bytes([h[4], h[5], h[6], h[7]]) as usize;
                        match &h[..4] {
                            b"DATA" => {
                                let mut chunk = vec![0u8; n];
                                s.read_exact(&mut chunk).await?;
                                data.extend_from_slice(&chunk);
                            }
                            b"DONE" => break,
                            _ => return Ok(()),
                        }
                    }
                    {
                        let mut st = self.state.lock().unwrap();
                        // adbd creates missing parents.
                        let mut acc = String::new();
                        let parts: Vec<&str> = path.split('/').filter(|p| !p.is_empty()).collect();
                        for part in &parts[..parts.len().saturating_sub(1)] {
                            acc.push('/');
                            acc.push_str(part);
                            st.dirs.insert(acc.clone());
                        }
                        st.files.insert(path, data);
                    }
                    s.write_all(b"OKAY\0\0\0\0").await?;
                }
                b"RECV" => {
                    let data = self.state.lock().unwrap().files.get(&norm(&arg)).cloned();
                    match data {
                        Some(d) => {
                            for c in d.chunks(64 * 1024) {
                                let mut r = b"DATA".to_vec();
                                r.extend_from_slice(&(c.len() as u32).to_le_bytes());
                                r.extend_from_slice(c);
                                s.write_all(&r).await?;
                            }
                            s.write_all(b"DONE\0\0\0\0").await?;
                        }
                        None => {
                            let m = b"No such file or directory";
                            let mut r = b"FAIL".to_vec();
                            r.extend_from_slice(&(m.len() as u32).to_le_bytes());
                            r.extend_from_slice(m);
                            s.write_all(&r).await?;
                        }
                    }
                }
                _ => return Ok(()), // QUIT or unknown
            }
        }
    }
}

fn norm(p: &str) -> String {
    let t = p.trim_end_matches('/');
    if t.is_empty() {
        "/".into()
    } else {
        t.to_string()
    }
}

async fn read_req(s: &mut TcpStream) -> std::io::Result<String> {
    let mut len = [0u8; 4];
    s.read_exact(&mut len).await?;
    let n = usize::from_str_radix(std::str::from_utf8(&len).unwrap_or("0"), 16).unwrap_or(0);
    let mut buf = vec![0u8; n];
    s.read_exact(&mut buf).await?;
    Ok(String::from_utf8_lossy(&buf).into_owned())
}

async fn write_str(s: &mut TcpStream, text: &str) -> std::io::Result<()> {
    s.write_all(format!("{:04x}{text}", text.len()).as_bytes()).await
}

async fn fail(s: &mut TcpStream, msg: &str) -> std::io::Result<()> {
    s.write_all(b"FAIL").await?;
    write_str(s, msg).await
}
