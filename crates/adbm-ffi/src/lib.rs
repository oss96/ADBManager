//! C ABI over `adbm-core`. Every message is UTF-8 JSON; the schema is the
//! serde types in `adbm_core::api`. See `include/adbm.h` for the contract.
//!
//! Threading: `adbm_core_call` and `adbm_core_next_event` may be called from
//! any thread, concurrently. `adbm_core_free` must not race with either:
//! stop the event thread (use a short timeout) before freeing.

use std::cell::RefCell;
use std::ffi::{c_char, CStr, CString};
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::ptr;
use std::time::Duration;

use adbm_core::{Command, Config, Core, Response};

/// Opaque handle.
pub struct AdbmCore {
    core: Core,
}

thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

fn set_error(e: impl Into<String>) {
    LAST_ERROR.with(|l| *l.borrow_mut() = Some(e.into()));
}

fn to_c(s: String) -> *mut c_char {
    // JSON never contains NUL (serde escapes it), but be defensive.
    CString::new(s.replace('\0', "")).map(CString::into_raw).unwrap_or(ptr::null_mut())
}

/// # Safety
/// `p` must be NULL or a NUL-terminated string.
unsafe fn from_c<'a>(p: *const c_char) -> Option<&'a str> {
    if p.is_null() {
        None
    } else {
        CStr::from_ptr(p).to_str().ok()
    }
}

fn json(r: &Response) -> *mut c_char {
    to_c(serde_json::to_string(r).unwrap_or_else(|e| format!(r#"{{"type":"error","message":"{e}"}}"#)))
}

/// Create a core. `config_json` may be NULL for defaults. Returns NULL on
/// failure; see `adbm_last_error`.
///
/// # Safety
/// `config_json` must be NULL or a valid NUL-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn adbm_core_new(config_json: *const c_char) -> *mut AdbmCore {
    let r = catch_unwind(AssertUnwindSafe(|| {
        let config = match from_c(config_json) {
            None => Config::default(),
            Some(s) if s.trim().is_empty() => Config::default(),
            Some(s) => serde_json::from_str(s).map_err(|e| format!("invalid config: {e}"))?,
        };
        Core::new(config).map_err(|e| e.to_string())
    }));
    match r {
        Ok(Ok(core)) => Box::into_raw(Box::new(AdbmCore { core })),
        Ok(Err(e)) => {
            set_error(e);
            ptr::null_mut()
        }
        Err(_) => {
            set_error("panic while creating core");
            ptr::null_mut()
        }
    }
}

/// Run a command (a JSON `Command`). Returns a JSON `Response` that the
/// caller frees with `adbm_string_free`. Never returns NULL.
///
/// # Safety
/// `core` must come from `adbm_core_new` and not be freed; `cmd_json` must
/// be a valid NUL-terminated string.
#[no_mangle]
pub unsafe extern "C" fn adbm_core_call(core: *mut AdbmCore, cmd_json: *const c_char) -> *mut c_char {
    let Some(core) = core.as_ref() else {
        return json(&Response::error("core is NULL"));
    };
    let Some(text) = from_c(cmd_json) else {
        return json(&Response::error("command is NULL or not UTF-8"));
    };
    let r = catch_unwind(AssertUnwindSafe(|| match serde_json::from_str::<Command>(text) {
        Ok(cmd) => core.core.call(cmd),
        Err(e) => Response::error(format!("invalid command: {e}")),
    }));
    json(&r.unwrap_or_else(|_| Response::error("panic while handling command")))
}

/// Wait up to `timeout_ms` for the next event. Returns a JSON `Event` the
/// caller frees with `adbm_string_free`, or NULL on timeout.
///
/// # Safety
/// `core` must come from `adbm_core_new` and not be freed.
#[no_mangle]
pub unsafe extern "C" fn adbm_core_next_event(core: *mut AdbmCore, timeout_ms: u32) -> *mut c_char {
    let Some(core) = core.as_ref() else { return ptr::null_mut() };
    let r = catch_unwind(AssertUnwindSafe(|| core.core.next_event(Duration::from_millis(timeout_ms as u64))));
    match r {
        Ok(Some(ev)) => serde_json::to_string(&ev).map(to_c).unwrap_or(ptr::null_mut()),
        _ => ptr::null_mut(),
    }
}

/// Stop all work and free the core. NULL is ignored.
///
/// # Safety
/// `core` must come from `adbm_core_new`, be freed at most once, and no
/// other call may be running on it.
#[no_mangle]
pub unsafe extern "C" fn adbm_core_free(core: *mut AdbmCore) {
    if !core.is_null() {
        let _ = catch_unwind(AssertUnwindSafe(|| drop(Box::from_raw(core))));
    }
}

/// Free a string returned by this library. NULL is ignored.
///
/// # Safety
/// `s` must come from this library and be freed at most once.
#[no_mangle]
pub unsafe extern "C" fn adbm_string_free(s: *mut c_char) {
    if !s.is_null() {
        drop(CString::from_raw(s));
    }
}

/// The last error on this thread from `adbm_core_new`, or NULL. The
/// caller frees it with `adbm_string_free`.
#[no_mangle]
pub extern "C" fn adbm_last_error() -> *mut c_char {
    LAST_ERROR.with(|l| l.borrow_mut().take()).map(to_c).unwrap_or(ptr::null_mut())
}

/// Library version. Static; do not free.
#[no_mangle]
pub extern "C" fn adbm_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}
