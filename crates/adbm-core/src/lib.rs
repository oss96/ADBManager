//! ADB Manager core.
//!
//! - [`adb`]: native client for the adb server's host protocol
//! - [`fastboot`]: fastboot via the platform-tools binary
//! - [`apk`]: APK manifest reader
//! - [`Core`]: runtime, device monitors, jobs; the API every UI uses
//!
//! See `docs/PLAN.md` for the architecture.

pub mod adb;
pub mod api;
pub mod apk;
mod engine;
pub mod error;
pub mod fastboot;
mod ops;
mod registry;
pub mod routines;
pub mod tools;

#[cfg(any(test, feature = "test-support"))]
pub mod testing;

pub use api::{Command, Config, Event, Response};
pub use engine::Core;
pub use error::{Error, Result};

pub const VERSION: &str = env!("CARGO_PKG_VERSION");
