//! Drives the C ABI exactly the way the Swift and C# apps do: JSON strings
//! in, JSON strings out, events polled with a timeout.

use std::ffi::{CStr, CString};
use std::time::{Duration, Instant};

use adbm_core::testing::FakeAdb;
use adbm_ffi::*;
use serde_json::{json, Value};

fn take(p: *mut std::ffi::c_char) -> Option<Value> {
    if p.is_null() {
        return None;
    }
    let s = unsafe { CStr::from_ptr(p) }.to_str().unwrap().to_string();
    unsafe { adbm_string_free(p) };
    Some(serde_json::from_str(&s).unwrap())
}

fn call(core: *mut AdbmCore, cmd: Value) -> Value {
    let c = CString::new(cmd.to_string()).unwrap();
    take(unsafe { adbm_core_call(core, c.as_ptr()) }).unwrap()
}

fn wait(core: *mut AdbmCore, pred: impl Fn(&Value) -> bool) -> Value {
    let deadline = Instant::now() + Duration::from_secs(10);
    while Instant::now() < deadline {
        if let Some(e) = take(unsafe { adbm_core_next_event(core, 100) }) {
            if pred(&e) {
                return e;
            }
        }
    }
    panic!("timed out");
}

#[test]
fn json_contract_end_to_end() {
    let fake = FakeAdb::start_blocking(vec![FakeAdb::device("SER1", "device")]);
    let cfg = CString::new(
        json!({ "adb_port": fake.port, "auto_start_server": false, "fastboot_path": "/nonexistent" }).to_string(),
    )
    .unwrap();
    let core = unsafe { adbm_core_new(cfg.as_ptr()) };
    assert!(!core.is_null());

    let ev = wait(core, |e| e["type"] == "devices_changed" && e["devices"][0]["info"]["sdk"] == 35);
    assert_eq!(ev["devices"][0]["serial"], "SER1");
    assert_eq!(ev["devices"][0]["state"], "online");
    assert_eq!(ev["devices"][0]["state_label"], "Online");
    assert_eq!(ev["devices"][0]["display_name"], "Pixel 8 Pro");

    let r = call(core, json!({ "type": "shell", "serials": ["SER1"], "command": "echo hi" }));
    assert_eq!(r["type"], "job");
    let id = r["job_id"].clone();
    let out = wait(core, |e| e["type"] == "job_output" && e["job_id"] == id);
    assert_eq!(out["text"], "hi\n");
    assert_eq!(out["stream"], "stdout");
    let fin = wait(core, |e| e["type"] == "job_finished" && e["job_id"] == id);
    assert_eq!(fin["status"], "succeeded");
    assert_eq!(fin["data"]["SER1"]["exit_code"], 0);

    let st = call(core, json!({ "type": "get_state" }));
    assert_eq!(st["type"], "state");
    assert_eq!(st["server"]["state"], "running");
    assert_eq!(st["config"]["adb_port"], fake.port);

    let t = call(core, json!({ "type": "routine_templates" }));
    assert_eq!(t["routines"][0]["steps"][1]["type"], "wait_for");

    // Malformed input never crashes and always answers with an error.
    assert_eq!(call(core, json!({ "type": "nope" }))["type"], "error");
    let garbage = CString::new("{not json").unwrap();
    assert_eq!(take(unsafe { adbm_core_call(core, garbage.as_ptr()) }).unwrap()["type"], "error");
    assert_eq!(take(unsafe { adbm_core_call(core, std::ptr::null()) }).unwrap()["type"], "error");

    unsafe { adbm_core_free(core) };
}

#[test]
fn bad_config_reports_error() {
    let cfg = CString::new("{\"adb_port\": \"x\"}").unwrap();
    let core = unsafe { adbm_core_new(cfg.as_ptr()) };
    assert!(core.is_null());
    let e = adbm_last_error();
    let msg = unsafe { CStr::from_ptr(e) }.to_str().unwrap().to_string();
    unsafe { adbm_string_free(e) };
    assert!(msg.starts_with("invalid config"), "{msg}");
    assert!(adbm_last_error().is_null(), "error is taken once");

    let v = unsafe { CStr::from_ptr(adbm_version()) }.to_str().unwrap();
    assert_eq!(v, env!("CARGO_PKG_VERSION"));
    // NULL handles are tolerated.
    unsafe { adbm_core_free(std::ptr::null_mut()) };
    assert!(unsafe { adbm_core_next_event(std::ptr::null_mut(), 0) }.is_null());
}

/// The header must declare exactly the exported functions.
#[test]
fn header_matches_exports() {
    let h = include_str!("../include/adbm.h");
    let src = include_str!("../src/lib.rs");
    let exported: Vec<&str> =
        src.lines().filter_map(|l| l.split("extern \"C\" fn ").nth(1)).map(|r| r.split('(').next().unwrap()).collect();
    assert_eq!(exported.len(), 7);
    for f in exported {
        assert!(h.contains(&format!("{f}(")), "{f} missing from adbm.h");
    }
}
