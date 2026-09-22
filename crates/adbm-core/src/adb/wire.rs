//! ADB host protocol framing (the "smart socket" protocol spoken to the adb
//! server on TCP 5037).
//!
//! Every request is `<4 hex digits length><payload>`. The server answers
//! `OKAY` or `FAIL<4 hex length><message>`. Host services that return data
//! send a length-prefixed string after `OKAY`; device services turn the
//! socket into a raw stream.

use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::TcpStream;

use crate::error::{Error, Result};

pub struct Conn {
    stream: TcpStream,
}

impl Conn {
    pub async fn connect(addr: &str) -> Result<Self> {
        let stream = TcpStream::connect(addr)
            .await
            .map_err(|source| Error::ServerUnreachable { addr: addr.to_string(), source })?;
        stream.set_nodelay(true)?;
        Ok(Self { stream })
    }

    /// Open a connection already switched to the transport of `serial`.
    pub async fn transport(addr: &str, serial: &str) -> Result<Self> {
        let mut c = Self::connect(addr).await?;
        c.request(&format!("host:transport:{serial}")).await?;
        Ok(c)
    }

    /// Send one request and wait for `OKAY`.
    pub async fn request(&mut self, payload: &str) -> Result<()> {
        if payload.len() > 0xFFFF {
            return Err(Error::Invalid("adb request is too long".into()));
        }
        let msg = format!("{:04x}{}", payload.len(), payload);
        self.stream.write_all(msg.as_bytes()).await?;
        self.read_status().await
    }

    pub async fn read_status(&mut self) -> Result<()> {
        let mut status = [0u8; 4];
        self.stream.read_exact(&mut status).await?;
        match &status {
            b"OKAY" => Ok(()),
            b"FAIL" => {
                let msg = self.read_hex_string().await?;
                Err(Error::Fail(msg))
            }
            other => Err(Error::protocol(format!("unexpected status {:?}", String::from_utf8_lossy(other)))),
        }
    }

    /// Read a `<4 hex length><bytes>` string.
    pub async fn read_hex_string(&mut self) -> Result<String> {
        let mut len = [0u8; 4];
        self.stream.read_exact(&mut len).await?;
        let len = parse_hex_len(&len)?;
        let mut buf = vec![0u8; len];
        self.stream.read_exact(&mut buf).await?;
        Ok(String::from_utf8_lossy(&buf).into_owned())
    }

    pub async fn read_to_end(&mut self) -> Result<Vec<u8>> {
        let mut out = Vec::new();
        self.stream.read_to_end(&mut out).await?;
        Ok(out)
    }

    pub fn stream(&mut self) -> &mut TcpStream {
        &mut self.stream
    }

    pub fn into_stream(self) -> TcpStream {
        self.stream
    }
}

pub(crate) fn parse_hex_len(b: &[u8; 4]) -> Result<usize> {
    let s = std::str::from_utf8(b).map_err(|_| Error::protocol("length is not ASCII"))?;
    usize::from_str_radix(s, 16).map_err(|_| Error::protocol(format!("bad length {s:?}")))
}

/// Run a host service that answers with one length-prefixed string.
pub async fn host_query(addr: &str, service: &str) -> Result<String> {
    let mut c = Conn::connect(addr).await?;
    c.request(service).await?;
    c.read_hex_string().await
}

pub async fn server_version(addr: &str) -> Result<u32> {
    let v = host_query(addr, "host:version").await?;
    u32::from_str_radix(v.trim(), 16).map_err(|_| Error::protocol(format!("bad version {v:?}")))
}

/// Features the device (not the host) supports, e.g. `shell_v2`, `cmd`.
pub async fn device_features(addr: &str, serial: &str) -> Result<Vec<String>> {
    let s = host_query(addr, &format!("host-serial:{serial}:features")).await?;
    Ok(s.split(',').map(|f| f.trim().to_string()).filter(|f| !f.is_empty()).collect())
}

pub async fn connect_tcp(addr: &str, target: &str) -> Result<String> {
    let msg = host_query(addr, &format!("host:connect:{target}")).await?;
    // adb answers OKAY even on failure; the text says what happened.
    if msg.starts_with("failed") || msg.starts_with("cannot") || msg.contains("unable") {
        return Err(Error::Fail(msg));
    }
    Ok(msg)
}

pub async fn disconnect_tcp(addr: &str, target: &str) -> Result<String> {
    host_query(addr, &format!("host:disconnect:{target}")).await
}

pub async fn kill_server(addr: &str) -> Result<()> {
    let mut c = Conn::connect(addr).await?;
    let _ = c.request("host:kill").await;
    Ok(())
}

/// `reboot:<target>` on the device. The device drops the connection, which
/// is expected.
pub async fn reboot(addr: &str, serial: &str, target: &str) -> Result<()> {
    let mut c = Conn::transport(addr, serial).await?;
    c.request(&format!("reboot:{target}")).await?;
    let _ = c.read_to_end().await;
    Ok(())
}
