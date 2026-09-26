//! Native client for the adb server's host protocol.

pub mod parse;
pub mod shell;
pub mod sync;
pub mod wire;

use tokio_util::sync::CancellationToken;

use crate::error::Result;
pub use parse::AdbDeviceLine;

/// Open `host:track-devices-l` and call `on_list` with every snapshot the
/// server sends until the connection closes or `cancel` fires.
pub async fn track_devices(
    addr: &str,
    cancel: &CancellationToken,
    mut on_list: impl FnMut(Vec<AdbDeviceLine>),
) -> Result<()> {
    let mut c = wire::Conn::connect(addr).await?;
    c.request("host:track-devices-l").await?;
    loop {
        let text = tokio::select! {
            _ = cancel.cancelled() => return Ok(()),
            t = c.read_hex_string() => t?,
        };
        on_list(parse::parse_device_list(&text));
    }
}
