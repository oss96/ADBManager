//! The `sync:` file service: LIST, STAT, SEND, RECV.
//!
//! Each request is `<4-byte id><u32 LE length><path>`. Data moves in `DATA`
//! chunks of at most 64 KiB.

use std::path::Path;

use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};
use tokio_util::sync::CancellationToken;

use super::wire::Conn;
use crate::api::{DirEntry, EntryKind};
use crate::error::{Error, Result};

pub const MAX_CHUNK: usize = 64 * 1024;

pub struct Sync {
    conn: Conn,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Stat {
    pub mode: u32,
    pub size: u32,
    pub mtime: u32,
}

impl Stat {
    pub fn exists(&self) -> bool {
        self.mode != 0
    }
    pub fn is_dir(&self) -> bool {
        EntryKind::from_mode(self.mode) == EntryKind::Dir
    }
}

impl Sync {
    pub async fn open(addr: &str, serial: &str) -> Result<Self> {
        let mut conn = Conn::transport(addr, serial).await?;
        conn.request("sync:").await?;
        Ok(Self { conn })
    }

    async fn send_req(&mut self, id: &[u8; 4], arg: &[u8]) -> Result<()> {
        let s = self.conn.stream();
        let mut msg = Vec::with_capacity(8 + arg.len());
        msg.extend_from_slice(id);
        msg.extend_from_slice(&(arg.len() as u32).to_le_bytes());
        msg.extend_from_slice(arg);
        s.write_all(&msg).await?;
        Ok(())
    }

    async fn read_id_len(&mut self) -> Result<([u8; 4], u32)> {
        let mut h = [0u8; 8];
        self.conn.stream().read_exact(&mut h).await?;
        Ok(([h[0], h[1], h[2], h[3]], u32::from_le_bytes([h[4], h[5], h[6], h[7]])))
    }

    async fn read_fail(&mut self, len: u32) -> Error {
        let mut msg = vec![0u8; len as usize];
        match self.conn.stream().read_exact(&mut msg).await {
            Ok(_) => Error::Fail(String::from_utf8_lossy(&msg).into_owned()),
            Err(e) => e.into(),
        }
    }

    pub async fn stat(&mut self, path: &str) -> Result<Stat> {
        self.send_req(b"STAT", path.as_bytes()).await?;
        let mut r = [0u8; 16];
        self.conn.stream().read_exact(&mut r).await?;
        if &r[..4] != b"STAT" {
            return Err(Error::protocol("expected STAT"));
        }
        let u = |i: usize| u32::from_le_bytes([r[i], r[i + 1], r[i + 2], r[i + 3]]);
        Ok(Stat { mode: u(4), size: u(8), mtime: u(12) })
    }

    pub async fn list(&mut self, path: &str) -> Result<Vec<DirEntry>> {
        self.send_req(b"LIST", path.as_bytes()).await?;
        let mut out = Vec::new();
        loop {
            let mut h = [0u8; 20];
            self.conn.stream().read_exact(&mut h).await?;
            let u = |i: usize| u32::from_le_bytes([h[i], h[i + 1], h[i + 2], h[i + 3]]);
            match &h[..4] {
                b"DENT" => {
                    let (mode, size, mtime, namelen) = (u(4), u(8), u(12), u(16));
                    let mut name = vec![0u8; namelen as usize];
                    self.conn.stream().read_exact(&mut name).await?;
                    let name = String::from_utf8_lossy(&name).into_owned();
                    if name == "." || name == ".." {
                        continue;
                    }
                    out.push(DirEntry {
                        name,
                        kind: EntryKind::from_mode(mode),
                        size: size as u64,
                        mode,
                        mtime: mtime as u64,
                    });
                }
                b"DONE" => break,
                b"FAIL" => return Err(self.read_fail(u(4)).await),
                other => {
                    return Err(Error::protocol(format!("unexpected LIST reply {:?}", String::from_utf8_lossy(other))))
                }
            }
        }
        out.sort_by(|a, b| {
            (b.kind == EntryKind::Dir)
                .cmp(&(a.kind == EntryKind::Dir))
                .then_with(|| a.name.to_lowercase().cmp(&b.name.to_lowercase()))
        });
        Ok(out)
    }

    /// Upload `reader` to `remote` with the given unix mode. `on_progress`
    /// receives the number of bytes sent so far.
    pub async fn send<R: AsyncRead + Unpin>(
        &mut self,
        mut reader: R,
        remote: &str,
        mode: u32,
        mtime: u32,
        cancel: &CancellationToken,
        mut on_progress: impl FnMut(u64),
    ) -> Result<()> {
        let arg = format!("{remote},{mode}");
        self.send_req(b"SEND", arg.as_bytes()).await?;
        let mut buf = vec![0u8; MAX_CHUNK];
        let mut sent = 0u64;
        loop {
            if cancel.is_cancelled() {
                return Err(Error::Cancelled);
            }
            let n = reader.read(&mut buf).await?;
            if n == 0 {
                break;
            }
            self.send_req(b"DATA", &buf[..n]).await?;
            sent += n as u64;
            on_progress(sent);
        }
        let s = self.conn.stream();
        let mut done = Vec::with_capacity(8);
        done.extend_from_slice(b"DONE");
        done.extend_from_slice(&mtime.to_le_bytes());
        s.write_all(&done).await?;
        let (id, len) = self.read_id_len().await?;
        match &id {
            b"OKAY" => Ok(()),
            b"FAIL" => Err(self.read_fail(len).await),
            _ => Err(Error::protocol("unexpected SEND reply")),
        }
    }

    pub async fn recv<W: AsyncWrite + Unpin>(
        &mut self,
        remote: &str,
        mut writer: W,
        cancel: &CancellationToken,
        mut on_progress: impl FnMut(u64),
    ) -> Result<u64> {
        self.send_req(b"RECV", remote.as_bytes()).await?;
        let mut got = 0u64;
        let mut buf = vec![0u8; MAX_CHUNK];
        loop {
            if cancel.is_cancelled() {
                return Err(Error::Cancelled);
            }
            let (id, len) = self.read_id_len().await?;
            match &id {
                b"DATA" => {
                    let len = len as usize;
                    if len > MAX_CHUNK {
                        return Err(Error::protocol("DATA chunk too large"));
                    }
                    self.conn.stream().read_exact(&mut buf[..len]).await?;
                    writer.write_all(&buf[..len]).await?;
                    got += len as u64;
                    on_progress(got);
                }
                b"DONE" => break,
                b"FAIL" => return Err(self.read_fail(len).await),
                _ => return Err(Error::protocol("unexpected RECV reply")),
            }
        }
        writer.flush().await?;
        Ok(got)
    }

    pub async fn quit(mut self) -> Result<()> {
        self.send_req(b"QUIT", b"").await
    }
}

/// Join a remote (always `/`-separated) path.
pub fn remote_join(base: &str, name: &str) -> String {
    if base.ends_with('/') {
        format!("{base}{name}")
    } else {
        format!("{base}/{name}")
    }
}

pub fn file_name(p: &Path) -> String {
    p.file_name().map(|s| s.to_string_lossy().into_owned()).unwrap_or_default()
}
