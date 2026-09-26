//! `shell:` (v1, raw stream, no exit code) and `shell,v2,raw:` (framed
//! stdout / stderr / exit code).

use tokio::io::AsyncReadExt;
use tokio_util::sync::CancellationToken;

use super::wire::Conn;
use crate::api::Stream;
use crate::error::{Error, Result};

#[derive(Debug, Default, Clone, PartialEq)]
pub struct ShellOutput {
    pub stdout: String,
    pub stderr: String,
    /// `None` when the device only speaks shell v1.
    pub exit_code: Option<i32>,
}

impl ShellOutput {
    pub fn succeeded(&self) -> bool {
        self.exit_code.map(|c| c == 0).unwrap_or(true)
    }

    pub fn combined(&self) -> String {
        let mut s = self.stdout.clone();
        if !self.stderr.is_empty() {
            if !s.is_empty() && !s.ends_with('\n') {
                s.push('\n');
            }
            s.push_str(&self.stderr);
        }
        s
    }
}

const V2_STDOUT: u8 = 1;
const V2_STDERR: u8 = 2;
const V2_EXIT: u8 = 3;

/// Run `command` and collect its output. `on_chunk` is called as output
/// arrives so long-running commands can stream to the UI.
pub async fn run(
    addr: &str,
    serial: &str,
    command: &str,
    v2: bool,
    cancel: &CancellationToken,
    mut on_chunk: impl FnMut(Stream, &str),
) -> Result<ShellOutput> {
    let mut conn = Conn::transport(addr, serial).await?;
    let service = if v2 { format!("shell,v2,raw:{command}") } else { format!("shell:{command}") };
    conn.request(&service).await?;
    let stream = conn.stream();
    let mut out = ShellOutput::default();

    if !v2 {
        let mut buf = vec![0u8; 16 * 1024];
        let mut pending = Vec::new();
        loop {
            let n = tokio::select! {
                _ = cancel.cancelled() => return Err(Error::Cancelled),
                r = stream.read(&mut buf) => r?,
            };
            if n == 0 {
                break;
            }
            pending.extend_from_slice(&buf[..n]);
            let text = take_utf8(&mut pending);
            if !text.is_empty() {
                on_chunk(Stream::Stdout, &text);
                out.stdout.push_str(&text);
            }
        }
        out.stdout.push_str(&String::from_utf8_lossy(&pending));
        return Ok(out);
    }

    let mut pend_out = Vec::new();
    let mut pend_err = Vec::new();
    loop {
        let mut header = [0u8; 5];
        let r = tokio::select! {
            _ = cancel.cancelled() => return Err(Error::Cancelled),
            r = stream.read_exact(&mut header) => r,
        };
        match r {
            Ok(_) => {}
            Err(e) if e.kind() == std::io::ErrorKind::UnexpectedEof => break,
            Err(e) => return Err(e.into()),
        }
        let id = header[0];
        let len = u32::from_le_bytes([header[1], header[2], header[3], header[4]]) as usize;
        let mut data = vec![0u8; len];
        stream.read_exact(&mut data).await?;
        match id {
            V2_STDOUT | V2_STDERR => {
                let (pending, which, sink) = if id == V2_STDOUT {
                    (&mut pend_out, Stream::Stdout, &mut out.stdout)
                } else {
                    (&mut pend_err, Stream::Stderr, &mut out.stderr)
                };
                pending.extend_from_slice(&data);
                let text = take_utf8(pending);
                if !text.is_empty() {
                    on_chunk(which, &text);
                    sink.push_str(&text);
                }
            }
            V2_EXIT => {
                out.exit_code = Some(data.first().copied().unwrap_or(0) as i32);
                break;
            }
            _ => {}
        }
    }
    out.stdout.push_str(&String::from_utf8_lossy(&pend_out));
    out.stderr.push_str(&String::from_utf8_lossy(&pend_err));
    Ok(out)
}

/// Take the longest valid UTF-8 prefix out of `buf`, leaving an incomplete
/// trailing sequence for the next read.
fn take_utf8(buf: &mut Vec<u8>) -> String {
    match std::str::from_utf8(buf) {
        Ok(s) => {
            let s = s.to_string();
            buf.clear();
            s
        }
        Err(e) => {
            let valid = e.valid_up_to();
            // An invalid byte (not just a truncated sequence) is replaced.
            let cut = if e.error_len().is_some() { buf.len() } else { valid };
            let s = String::from_utf8_lossy(&buf[..cut]).into_owned();
            buf.drain(..cut);
            s
        }
    }
}

/// Quote an argument for the device's `sh`.
pub fn quote(arg: &str) -> String {
    if !arg.is_empty() && arg.bytes().all(|b| b.is_ascii_alphanumeric() || b"/._-+=:@%,".contains(&b)) {
        return arg.to_string();
    }
    format!("'{}'", arg.replace('\'', r"'\''"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn utf8_split_across_reads() {
        let mut buf = "héllo".as_bytes()[..2].to_vec(); // 'h' + first byte of 'é'
        assert_eq!(take_utf8(&mut buf), "h");
        assert_eq!(buf.len(), 1);
        buf.extend_from_slice(&"héllo".as_bytes()[2..]);
        assert_eq!(take_utf8(&mut buf), "éllo");
        assert!(buf.is_empty());
    }

    #[test]
    fn quoting() {
        assert_eq!(quote("/sdcard/a.txt"), "/sdcard/a.txt");
        assert_eq!(quote("my file"), "'my file'");
        assert_eq!(quote("it's"), r"'it'\''s'");
        assert_eq!(quote(""), "''");
    }
}
