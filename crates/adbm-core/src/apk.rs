//! Reads package metadata from an APK's binary `AndroidManifest.xml`
//! (Android binary XML, "AXML"), without aapt.
//!
//! Only the manifest is parsed. A label that is a resource reference
//! (`@string/app_name`, the common case) is not resolved through
//! `resources.arsc` in v1: `label` is `None` and UIs fall back to the file
//! name.

use std::io::Read;
use std::path::Path;

use serde::{Deserialize, Serialize};

use crate::error::{Error, Result};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
pub struct ApkInfo {
    pub path: String,
    pub file_name: String,
    pub size: u64,
    pub package: String,
    pub version_code: Option<u64>,
    pub version_name: Option<String>,
    pub min_sdk: Option<u32>,
    pub target_sdk: Option<u32>,
    /// Literal label, if the manifest has one.
    pub label: Option<String>,
}

pub fn inspect(path: impl AsRef<Path>) -> Result<ApkInfo> {
    let path = path.as_ref();
    let file = std::fs::File::open(path)?;
    let size = file.metadata()?.len();
    let mut zip = zip::ZipArchive::new(file).map_err(|e| Error::Apk(format!("not a zip file ({e})")))?;
    let mut entry =
        zip.by_name("AndroidManifest.xml").map_err(|_| Error::Apk("AndroidManifest.xml is missing".into()))?;
    let mut bytes = Vec::with_capacity(entry.size() as usize);
    entry.read_to_end(&mut bytes)?;
    let mut info = parse_manifest(&bytes)?;
    info.path = path.to_string_lossy().into_owned();
    info.file_name = path.file_name().map(|f| f.to_string_lossy().into_owned()).unwrap_or_default();
    info.size = size;
    Ok(info)
}

// Chunk types
const RES_STRING_POOL: u16 = 0x0001;
const RES_XML: u16 = 0x0003;
const RES_XML_RESOURCE_MAP: u16 = 0x0180;
const RES_XML_START_ELEMENT: u16 = 0x0102;

// Typed value types
const TYPE_STRING: u8 = 0x03;
const TYPE_INT_DEC: u8 = 0x10;
const TYPE_INT_HEX: u8 = 0x11;

// android: attribute resource ids (used when names are stripped)
const ATTR_LABEL: u32 = 0x0101_0001;
const ATTR_MIN_SDK: u32 = 0x0101_020c;
const ATTR_VERSION_CODE: u32 = 0x0101_021b;
const ATTR_VERSION_NAME: u32 = 0x0101_021c;
const ATTR_TARGET_SDK: u32 = 0x0101_0270;

struct Reader<'a> {
    b: &'a [u8],
}

impl<'a> Reader<'a> {
    fn u16(&self, at: usize) -> Result<u16> {
        self.b
            .get(at..at + 2)
            .map(|s| u16::from_le_bytes([s[0], s[1]]))
            .ok_or_else(|| Error::Apk("truncated manifest".into()))
    }
    fn u32(&self, at: usize) -> Result<u32> {
        self.b
            .get(at..at + 4)
            .map(|s| u32::from_le_bytes([s[0], s[1], s[2], s[3]]))
            .ok_or_else(|| Error::Apk("truncated manifest".into()))
    }
}

struct StringPool {
    strings: Vec<String>,
}

impl StringPool {
    fn parse(r: &Reader, at: usize) -> Result<Self> {
        let count = r.u32(at + 8)? as usize;
        let flags = r.u32(at + 16)?;
        let strings_start = r.u32(at + 20)? as usize;
        let utf8 = flags & 0x100 != 0;
        let mut strings = Vec::with_capacity(count.min(100_000));
        for i in 0..count {
            let off = r.u32(at + 28 + i * 4)? as usize;
            let s = at + strings_start + off;
            strings.push(if utf8 { Self::read_utf8(r, s)? } else { Self::read_utf16(r, s)? });
        }
        Ok(Self { strings })
    }

    fn read_utf8(r: &Reader, mut p: usize) -> Result<String> {
        // utf16 length (1-2 bytes), then utf8 byte length (1-2 bytes)
        let skip_len = |p: &mut usize| -> Result<usize> {
            let b0 = *r.b.get(*p).ok_or_else(|| Error::Apk("truncated string".into()))? as usize;
            *p += 1;
            if b0 & 0x80 != 0 {
                let b1 = *r.b.get(*p).ok_or_else(|| Error::Apk("truncated string".into()))? as usize;
                *p += 1;
                Ok(((b0 & 0x7f) << 8) | b1)
            } else {
                Ok(b0)
            }
        };
        skip_len(&mut p)?;
        let n = skip_len(&mut p)?;
        let bytes = r.b.get(p..p + n).ok_or_else(|| Error::Apk("truncated string".into()))?;
        Ok(String::from_utf8_lossy(bytes).into_owned())
    }

    fn read_utf16(r: &Reader, mut p: usize) -> Result<String> {
        let mut n = r.u16(p)? as usize;
        p += 2;
        if n & 0x8000 != 0 {
            n = ((n & 0x7fff) << 16) | r.u16(p)? as usize;
            p += 2;
        }
        let mut units = Vec::with_capacity(n);
        for i in 0..n {
            units.push(r.u16(p + i * 2)?);
        }
        Ok(String::from_utf16_lossy(&units))
    }

    fn get(&self, idx: u32) -> Option<&str> {
        self.strings.get(idx as usize).map(String::as_str)
    }
}

pub fn parse_manifest(bytes: &[u8]) -> Result<ApkInfo> {
    let r = Reader { b: bytes };
    if r.u16(0)? != RES_XML {
        return Err(Error::Apk("manifest is not binary XML".into()));
    }
    let header_size = r.u16(2)? as usize;
    let total = (r.u32(4)? as usize).min(bytes.len());
    let mut pool: Option<StringPool> = None;
    let mut res_ids: Vec<u32> = Vec::new();
    let mut info = ApkInfo::default();
    let mut found_manifest = false;

    let mut at = header_size;
    while at + 8 <= total {
        let ty = r.u16(at)?;
        let chunk_header = r.u16(at + 2)? as usize;
        let size = r.u32(at + 4)? as usize;
        if size < 8 || at + size > total {
            break;
        }
        match ty {
            RES_STRING_POOL => pool = Some(StringPool::parse(&r, at)?),
            RES_XML_RESOURCE_MAP => {
                let n = (size - chunk_header) / 4;
                res_ids = (0..n).map(|i| r.u32(at + chunk_header + i * 4)).collect::<Result<_>>()?;
            }
            RES_XML_START_ELEMENT => {
                let pool = pool.as_ref().ok_or_else(|| Error::Apk("no string pool".into()))?;
                let ext = at + chunk_header; // ResXMLTree_attrExt
                let name = pool.get(r.u32(ext + 4)?).unwrap_or("");
                let attr_start = r.u16(ext + 8)? as usize;
                let attr_size = r.u16(ext + 10)? as usize;
                let attr_count = r.u16(ext + 12)? as usize;
                for i in 0..attr_count {
                    let a = ext + attr_start + i * attr_size;
                    let name_idx = r.u32(a + 4)?;
                    let raw = r.u32(a + 8)?;
                    let data_type = *bytes.get(a + 15).unwrap_or(&0);
                    let data = r.u32(a + 16)?;
                    let attr_name = pool.get(name_idx).unwrap_or("");
                    let res_id = res_ids.get(name_idx as usize).copied().unwrap_or(0);
                    let string_val = || -> Option<String> {
                        if raw != u32::MAX {
                            pool.get(raw).map(str::to_string)
                        } else if data_type == TYPE_STRING {
                            pool.get(data).map(str::to_string)
                        } else {
                            None
                        }
                    };
                    let int_val = || -> Option<u64> {
                        match data_type {
                            TYPE_INT_DEC | TYPE_INT_HEX => Some(data as u64),
                            _ => string_val().and_then(|s| s.parse().ok()),
                        }
                    };
                    let is = |n: &str, id: u32| attr_name == n || res_id == id;
                    match name {
                        "manifest" => {
                            found_manifest = true;
                            if attr_name == "package" {
                                info.package = string_val().unwrap_or_default();
                            } else if is("versionCode", ATTR_VERSION_CODE) {
                                info.version_code = int_val();
                            } else if is("versionName", ATTR_VERSION_NAME) {
                                info.version_name = string_val();
                            }
                        }
                        "uses-sdk" => {
                            if is("minSdkVersion", ATTR_MIN_SDK) {
                                info.min_sdk = int_val().map(|v| v as u32);
                            } else if is("targetSdkVersion", ATTR_TARGET_SDK) {
                                info.target_sdk = int_val().map(|v| v as u32);
                            }
                        }
                        "application" if is("label", ATTR_LABEL) => info.label = string_val(),
                        _ => {}
                    }
                }
            }
            _ => {}
        }
        at += size;
    }
    if !found_manifest || info.package.is_empty() {
        return Err(Error::Apk("manifest has no package name".into()));
    }
    Ok(info)
}

/// Minimal AXML writer, used by tests (here and in dependent crates) to
/// build real APKs without shipping binary fixtures.
#[cfg(any(test, feature = "test-support"))]
pub mod testing {
    pub enum Val<'a> {
        Str(&'a str),
        Int(u32),
    }

    /// Build a binary manifest. `elements` are (tag, [(attr, value)]).
    pub fn manifest(elements: &[(&str, Vec<(&str, Val)>)]) -> Vec<u8> {
        let mut strings: Vec<String> = Vec::new();
        let mut intern = |s: &str| -> u32 {
            if let Some(i) = strings.iter().position(|x| x == s) {
                return i as u32;
            }
            strings.push(s.to_string());
            (strings.len() - 1) as u32
        };
        let mut body = Vec::new();
        for (tag, attrs) in elements {
            let name = intern(tag);
            let mut chunk = Vec::new();
            // node header: type, headerSize=16, size (patched), lineNumber, comment
            chunk.extend_from_slice(&0x0102u16.to_le_bytes());
            chunk.extend_from_slice(&16u16.to_le_bytes());
            chunk.extend_from_slice(&0u32.to_le_bytes());
            chunk.extend_from_slice(&1u32.to_le_bytes());
            chunk.extend_from_slice(&u32::MAX.to_le_bytes());
            // attrExt: ns, name, attrStart=20, attrSize=20, count, id, class, style
            chunk.extend_from_slice(&u32::MAX.to_le_bytes());
            chunk.extend_from_slice(&name.to_le_bytes());
            chunk.extend_from_slice(&20u16.to_le_bytes());
            chunk.extend_from_slice(&20u16.to_le_bytes());
            chunk.extend_from_slice(&(attrs.len() as u16).to_le_bytes());
            chunk.extend_from_slice(&[0u8; 6]);
            for (an, v) in attrs {
                let an = intern(an);
                chunk.extend_from_slice(&u32::MAX.to_le_bytes());
                chunk.extend_from_slice(&an.to_le_bytes());
                match v {
                    Val::Str(s) => {
                        let si = intern(s);
                        chunk.extend_from_slice(&si.to_le_bytes());
                        chunk.extend_from_slice(&8u16.to_le_bytes());
                        chunk.push(0);
                        chunk.push(0x03);
                        chunk.extend_from_slice(&si.to_le_bytes());
                    }
                    Val::Int(n) => {
                        chunk.extend_from_slice(&u32::MAX.to_le_bytes());
                        chunk.extend_from_slice(&8u16.to_le_bytes());
                        chunk.push(0);
                        chunk.push(0x10);
                        chunk.extend_from_slice(&n.to_le_bytes());
                    }
                }
            }
            let len = chunk.len() as u32;
            chunk[4..8].copy_from_slice(&len.to_le_bytes());
            body.extend_from_slice(&chunk);
        }
        // UTF-16 string pool
        let mut data = Vec::new();
        let mut offsets = Vec::new();
        for s in &strings {
            offsets.push(data.len() as u32);
            let units: Vec<u16> = s.encode_utf16().collect();
            data.extend_from_slice(&(units.len() as u16).to_le_bytes());
            for u in units {
                data.extend_from_slice(&u.to_le_bytes());
            }
            data.extend_from_slice(&0u16.to_le_bytes());
        }
        while data.len() % 4 != 0 {
            data.push(0);
        }
        let strings_start = 28 + offsets.len() as u32 * 4;
        let mut pool = Vec::new();
        pool.extend_from_slice(&0x0001u16.to_le_bytes());
        pool.extend_from_slice(&28u16.to_le_bytes());
        pool.extend_from_slice(&(strings_start + data.len() as u32).to_le_bytes());
        pool.extend_from_slice(&(strings.len() as u32).to_le_bytes());
        pool.extend_from_slice(&0u32.to_le_bytes()); // styles
        pool.extend_from_slice(&0u32.to_le_bytes()); // flags: utf16
        pool.extend_from_slice(&strings_start.to_le_bytes());
        pool.extend_from_slice(&0u32.to_le_bytes());
        for o in offsets {
            pool.extend_from_slice(&o.to_le_bytes());
        }
        pool.extend_from_slice(&data);

        let mut out = Vec::new();
        out.extend_from_slice(&0x0003u16.to_le_bytes());
        out.extend_from_slice(&8u16.to_le_bytes());
        out.extend_from_slice(&((8 + pool.len() + body.len()) as u32).to_le_bytes());
        out.extend_from_slice(&pool);
        out.extend_from_slice(&body);
        out
    }

    /// Write a minimal APK (zip with only a manifest) to `path`.
    pub fn write_apk(path: &std::path::Path, package: &str, version_code: u32, version_name: &str) {
        use std::io::Write;
        let m = manifest(&[
            (
                "manifest",
                vec![
                    ("package", Val::Str(package)),
                    ("versionCode", Val::Int(version_code)),
                    ("versionName", Val::Str(version_name)),
                ],
            ),
            ("uses-sdk", vec![("minSdkVersion", Val::Int(24)), ("targetSdkVersion", Val::Int(35))]),
            ("application", vec![("label", Val::Str("Field Tools"))]),
        ]);
        let f = std::fs::File::create(path).unwrap();
        let mut z = zip::ZipWriter::new(f);
        z.start_file("AndroidManifest.xml", zip::write::SimpleFileOptions::default()).unwrap();
        z.write_all(&m).unwrap();
        z.finish().unwrap();
    }
}

#[cfg(test)]
mod tests {
    use super::testing::*;
    use super::*;

    #[test]
    fn reads_generated_apk() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("field-tools-2.4.1.apk");
        write_apk(&p, "com.example.fieldtools", 241, "2.4.1");
        let info = inspect(&p).unwrap();
        assert_eq!(info.package, "com.example.fieldtools");
        assert_eq!(info.version_code, Some(241));
        assert_eq!(info.version_name.as_deref(), Some("2.4.1"));
        assert_eq!(info.min_sdk, Some(24));
        assert_eq!(info.target_sdk, Some(35));
        assert_eq!(info.label.as_deref(), Some("Field Tools"));
        assert_eq!(info.file_name, "field-tools-2.4.1.apk");
    }

    #[test]
    fn rejects_non_apk() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("x.apk");
        std::fs::write(&p, b"not a zip").unwrap();
        assert!(matches!(inspect(&p), Err(Error::Apk(_))));
        assert!(parse_manifest(&[0, 0, 0, 0]).is_err());
    }
}
