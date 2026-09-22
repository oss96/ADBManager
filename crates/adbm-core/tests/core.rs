//! End-to-end tests of `Core` against the in-process fake adb server.

use std::time::{Duration, Instant};

use adbm_core::api::*;
use adbm_core::testing::{FakeAdb, FakeDevice};
use adbm_core::Core;

fn core_for(fake: &FakeAdb) -> Core {
    Core::new(Config {
        adb_port: fake.port,
        auto_start_server: false,
        // Point fastboot at nothing so tests don't depend on the host.
        fastboot_path: Some("/nonexistent/fastboot".into()),
        ..Config::default()
    })
    .unwrap()
}

/// Wait for the first event matching `f`, failing after 10 s with the last
/// events seen, so a stall says which step it was.
fn wait<T>(core: &Core, what: &str, mut f: impl FnMut(&Event) -> Option<T>) -> T {
    let deadline = Instant::now() + Duration::from_secs(10);
    let mut recent = std::collections::VecDeque::new();
    while Instant::now() < deadline {
        if let Some(e) = core.next_event(Duration::from_millis(100)) {
            if let Some(v) = f(&e) {
                return v;
            }
            recent.push_back(format!("{e:?}"));
            if recent.len() > 8 {
                recent.pop_front();
            }
        }
    }
    panic!("timed out waiting for {what}; last events:\n{}", Vec::from(recent).join("\n"));
}

fn finished(core: &Core, id: JobId) -> (JobStatus, String, Option<serde_json::Value>) {
    wait(core, "job to finish", |e| match e {
        Event::JobFinished { job_id, status, summary, data } if *job_id == id => {
            Some((*status, summary.clone(), data.clone()))
        }
        _ => None,
    })
}

fn job(r: Response) -> JobId {
    match r {
        Response::Job { job_id } => job_id,
        other => panic!("expected a job, got {other:?}"),
    }
}

fn online(core: &Core, n: usize) -> Vec<Device> {
    wait(core, "devices with details", |e| match e {
        Event::DevicesChanged { devices }
            if devices.len() == n
                && devices.iter().filter(|d| d.state == DeviceState::Online).all(|d| d.info.sdk.is_some()) =>
        {
            Some(devices.clone())
        }
        _ => None,
    })
}

fn devices() -> Vec<FakeDevice> {
    vec![
        FakeAdb::device("SER1", "device"),
        FakeAdb::device("SER2", "device"),
        FakeAdb::device("LOCKED", "unauthorized"),
    ]
}

#[test]
fn tracks_devices_and_reads_details() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    let list = online(&core, 3);
    assert_eq!(list[0].display_name, "Pixel 8 Pro");
    assert_eq!(list[0].info.android_version.as_deref(), Some("15"));
    assert_eq!(list[0].info.battery_level, Some(82));
    let locked = list.iter().find(|d| d.serial == "LOCKED").unwrap();
    assert_eq!(locked.state, DeviceState::Unauthorized);
    assert!(locked.state_hint.as_deref().unwrap().contains("USB debugging"));

    fake.set_devices(vec![FakeAdb::device("SER1", "device")]);
    wait(&core, "unplug", |e| matches!(e, Event::DevicesChanged { devices } if devices.len() == 1).then_some(()));

    match core.call(Command::GetState) {
        Response::State { devices, server, .. } => {
            assert_eq!(devices.len(), 1);
            assert!(matches!(server, ServerStatus::Running { version: 0x29, .. }));
        }
        other => panic!("{other:?}"),
    }
}

#[test]
fn shell_on_many_devices_reports_partial_failure() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let id = job(core.call(Command::Shell {
        serials: vec!["SER1".into(), "SER2".into(), "LOCKED".into()],
        command: "echo hello".into(),
    }));
    let mut outputs = 0;
    let mut device_results = Vec::new();
    let (status, summary, data) = wait(&core, "shell job", |e| match e {
        Event::JobOutput { job_id, text, .. } if *job_id == id => {
            assert_eq!(text, "hello\n");
            outputs += 1;
            None
        }
        Event::JobDeviceFinished { job_id, serial, ok, message } if *job_id == id => {
            device_results.push((serial.clone(), *ok, message.clone()));
            None
        }
        Event::JobFinished { job_id, status, summary, data } if *job_id == id => {
            Some((*status, summary.clone(), data.clone()))
        }
        _ => None,
    });
    assert_eq!(outputs, 2);
    assert_eq!(status, JobStatus::PartiallyFailed);
    assert_eq!(summary, "2 succeeded, 1 failed");
    let locked = device_results.iter().find(|r| r.0 == "LOCKED").unwrap();
    assert!(!locked.1 && locked.2.contains("unauthorized"), "{locked:?}");
    assert_eq!(data.unwrap()["SER1"]["exit_code"], 0);
}

#[test]
fn shell_exit_code_fails_the_device() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let (status, summary, _) =
        finished(&core, job(core.call(Command::Shell { serials: vec!["SER1".into()], command: "exit 3".into() })));
    assert_eq!(status, JobStatus::Failed);
    assert!(summary.contains("exited with code 3"), "{summary}");
}

#[test]
fn shell_v1_fallback() {
    let mut d = FakeAdb::device("OLD", "device");
    d.shell_v2 = false;
    let fake = FakeAdb::start_blocking(vec![d]);
    let core = core_for(&fake);
    online(&core, 1);
    let (status, _, data) =
        finished(&core, job(core.call(Command::Shell { serials: vec!["OLD".into()], command: "echo v1".into() })));
    assert_eq!(status, JobStatus::Succeeded);
    let d = data.unwrap();
    assert_eq!(d["OLD"]["stdout"], "v1\n");
    assert!(d["OLD"]["exit_code"].is_null());
}

#[test]
fn install_pushes_then_runs_pm_and_cleans_up() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let dir = tempfile::tempdir().unwrap();
    let apk = dir.path().join("field-tools-2.4.1.apk");
    adbm_core::apk::testing::write_apk(&apk, "com.example.fieldtools", 241, "2.4.1");

    match core.call(Command::InspectApk { path: apk.display().to_string() }) {
        Response::Apk { apk } => assert_eq!(apk.package, "com.example.fieldtools"),
        other => panic!("{other:?}"),
    }

    let id = job(core.call(Command::Install {
        serials: vec!["SER1".into(), "SER2".into()],
        path: apk.display().to_string(),
        allow_downgrade: false,
    }));
    let (status, summary, _) = finished(&core, id);
    assert_eq!(status, JobStatus::Succeeded, "{summary}");
    assert_eq!(summary, "2 devices succeeded");
    let st = fake.state.lock().unwrap();
    assert_eq!(st.shell_log.iter().filter(|c| c.contains("pm install -r")).count(), 2);
    assert!(st.files.keys().all(|k| !k.starts_with("/data/local/tmp/adbm-")), "temp APK removed");
}

#[test]
fn uninstall_and_packages() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let (s, _, data) =
        finished(&core, job(core.call(Command::ListPackages { serial: "SER1".into(), include_system: true })));
    assert_eq!(s, JobStatus::Succeeded);
    let pkgs: Vec<Package> = serde_json::from_value(data.unwrap()).unwrap();
    assert_eq!(pkgs.len(), 4);
    assert!(pkgs.iter().any(|p| p.name == "android" && p.system));

    let (s, _, _) = finished(
        &core,
        job(core.call(Command::Uninstall {
            serials: vec!["SER1".into()],
            package: "com.example.fieldtools".into(),
            keep_data: false,
        })),
    );
    assert_eq!(s, JobStatus::Succeeded);
    let (s, summary, _) = finished(
        &core,
        job(core.call(Command::Uninstall {
            serials: vec!["SER1".into()],
            package: "com.missing".into(),
            keep_data: false,
        })),
    );
    assert_eq!(s, JobStatus::Failed);
    assert!(summary.contains("com.missing is not installed"), "{summary}");
}

#[test]
fn push_list_pull_round_trip() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let src = tempfile::tempdir().unwrap();
    let folder = src.path().join("payload");
    std::fs::create_dir_all(folder.join("nested/deeper")).unwrap();
    std::fs::create_dir_all(folder.join("empty")).unwrap();
    std::fs::write(folder.join("a.txt"), b"alpha").unwrap();
    let big: Vec<u8> = (0..200_000u32).map(|i| (i % 251) as u8).collect();
    std::fs::write(folder.join("nested/deeper/big.bin"), &big).unwrap();

    let (s, summary, _) = finished(
        &core,
        job(core.call(Command::Push {
            serials: vec!["SER1".into()],
            local: folder.display().to_string(),
            remote: "/sdcard/".into(),
        })),
    );
    assert_eq!(s, JobStatus::Succeeded, "{summary}");
    {
        let st = fake.state.lock().unwrap();
        assert_eq!(st.files["/sdcard/payload/a.txt"], b"alpha");
        assert_eq!(st.files["/sdcard/payload/nested/deeper/big.bin"], big);
        assert!(st.dirs.contains("/sdcard/payload/empty"));
    }

    let (s, _, data) =
        finished(&core, job(core.call(Command::ListDir { serial: "SER1".into(), path: "/sdcard/payload".into() })));
    assert_eq!(s, JobStatus::Succeeded);
    let entries: Vec<DirEntry> = serde_json::from_value(data.unwrap()["entries"].clone()).unwrap();
    let names: Vec<_> = entries.iter().map(|e| (e.name.as_str(), e.kind)).collect();
    assert_eq!(names, [("empty", EntryKind::Dir), ("nested", EntryKind::Dir), ("a.txt", EntryKind::File)]);

    let dst = tempfile::tempdir().unwrap();
    let (s, summary, _) = finished(
        &core,
        job(core.call(Command::Pull {
            serial: "SER1".into(),
            remote: "/sdcard/payload".into(),
            local: dst.path().display().to_string(),
        })),
    );
    assert_eq!(s, JobStatus::Succeeded, "{summary}");
    assert_eq!(std::fs::read(dst.path().join("payload/nested/deeper/big.bin")).unwrap(), big);
    assert!(dst.path().join("payload/empty").is_dir());

    let (s, summary, _) = finished(
        &core,
        job(core.call(Command::Pull {
            serial: "SER1".into(),
            remote: "/sdcard/nope".into(),
            local: dst.path().display().to_string(),
        })),
    );
    assert_eq!(s, JobStatus::Failed);
    assert!(summary.contains("does not exist"), "{summary}");
}

#[test]
fn cancel_stops_a_hanging_command() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let id = job(core.call(Command::Shell { serials: vec!["SER1".into()], command: "hang".into() }));
    std::thread::sleep(Duration::from_millis(200));
    assert_eq!(core.call(Command::Cancel { job_id: id }), Response::Ok);
    let (s, _, _) = finished(&core, id);
    assert_eq!(s, JobStatus::Cancelled);
}

#[test]
fn reboot_marks_device_rebooting() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let (s, _, _) =
        finished(&core, job(core.call(Command::Reboot { serials: vec!["SER2".into()], mode: RebootMode::Bootloader })));
    assert_eq!(s, JobStatus::Succeeded);
    assert_eq!(fake.state.lock().unwrap().reboots, ["SER2:bootloader"]);
    // The device leaves adb; its row stays as "Rebooting…".
    fake.set_devices(vec![FakeAdb::device("SER1", "device")]);
    let d = wait(&core, "rebooting row", |e| match e {
        Event::DevicesChanged { devices } => {
            devices.iter().find(|d| d.serial == "SER2" && d.state == DeviceState::Rebooting).cloned()
        }
        _ => None,
    });
    assert_eq!(d.display_name, "Pixel 8 Pro");
}

#[test]
fn routine_runs_steps_in_order_and_stops_on_failure() {
    use adbm_core::routines::{Routine, Step, WaitTarget};
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    online(&core, 3);
    let routine = Routine {
        id: "t".into(),
        name: "Test".into(),
        description: String::new(),
        steps: vec![
            Step::WaitFor { target: WaitTarget::Adb, timeout_s: 5 },
            Step::Shell { command: "echo one".into(), ignore_failure: false },
            Step::Shell { command: "exit 1".into(), ignore_failure: true },
            Step::Delay { ms: 10 },
            Step::Shell { command: "exit 2".into(), ignore_failure: false },
            Step::Shell { command: "echo never".into(), ignore_failure: false },
        ],
    };
    let (s, summary, _) =
        finished(&core, job(core.call(Command::RunRoutine { serials: vec!["SER1".into()], routine: routine.clone() })));
    assert_eq!(s, JobStatus::Failed);
    assert!(summary.contains("Step 5"), "{summary}");
    let log = fake.state.lock().unwrap().shell_log.clone();
    assert!(log.iter().any(|c| c == "SER1: echo one"));
    assert!(!log.iter().any(|c| c.contains("never")));

    // Validation happens before a job is started.
    let bad = adbm_core::routines::templates().remove(0);
    match core.call(Command::RunRoutine { serials: vec!["SER1".into()], routine: bad }) {
        Response::Error { message } => assert!(message.contains("Step 3"), "{message}"),
        other => panic!("{other:?}"),
    }
}

#[test]
fn connect_and_errors() {
    let fake = FakeAdb::start_blocking(devices());
    let core = core_for(&fake);
    let (s, summary, _) = finished(&core, job(core.call(Command::Connect { address: "192.168.1.42".into() })));
    assert_eq!(s, JobStatus::Succeeded);
    assert_eq!(summary, "connected to 192.168.1.42:5555");
    assert!(matches!(core.call(Command::Shell { serials: vec![], command: "x".into() }), Response::Error { .. }));
    assert!(matches!(
        core.call(Command::Delete { serial: "SER1".into(), path: "/sdcard".into() }),
        Response::Error { .. }
    ));
}

#[test]
fn reports_unreachable_server() {
    // Nothing listens on this port; auto start is off.
    let core = Core::new(Config {
        adb_port: 1,
        auto_start_server: false,
        adb_path: Some("/bin/true".into()),
        ..Config::default()
    })
    .unwrap();
    wait(&core, "server error", |e| matches!(e, Event::Server { status: ServerStatus::Error { .. } }).then_some(()));
}
