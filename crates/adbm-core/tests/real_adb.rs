//! Smoke test against a real adb server. Opt in with `ADBM_REAL_ADB=1`.
//! Uses port 5099 so it doesn't disturb a server on the default port.

use std::time::{Duration, Instant};

use adbm_core::api::*;
use adbm_core::{adb::wire, Core};

#[test]
fn starts_and_talks_to_real_adb() {
    if std::env::var_os("ADBM_REAL_ADB").is_none() {
        eprintln!("skipped: set ADBM_REAL_ADB=1 to run");
        return;
    }
    let core = Core::new(Config { adb_port: 5099, ..Config::default() }).unwrap();
    let deadline = Instant::now() + Duration::from_secs(30);
    let mut running = None;
    while Instant::now() < deadline && running.is_none() {
        if let Some(Event::Server { status: s @ ServerStatus::Running { .. } }) =
            core.next_event(Duration::from_millis(200))
        {
            running = Some(s);
        }
    }
    let status = running.expect("adb server did not start");
    eprintln!("{status:?}");
    match core.call(Command::GetState) {
        Response::State { devices, .. } => eprintln!("{} device(s) attached", devices.len()),
        other => panic!("{other:?}"),
    }
    // Host service round trip through our own client.
    let rt = tokio::runtime::Runtime::new().unwrap();
    let list = rt.block_on(wire::host_query("127.0.0.1:5099", "host:devices-l")).unwrap();
    eprintln!("host:devices-l -> {list:?}");
    drop(core);
    rt.block_on(wire::kill_server("127.0.0.1:5099")).unwrap();
}
