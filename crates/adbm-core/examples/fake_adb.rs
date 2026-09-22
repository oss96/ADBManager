//! A fake adb server with sample devices, for developing the UIs without
//! hardware:
//!
//!     cargo run -p adbm-core --features test-support --example fake_adb -- 5199
//!     ADBM_ADB_PORT=5199 cargo run -p adbm-gtk

use adbm_core::testing::FakeAdb;

#[tokio::main(flavor = "multi_thread", worker_threads = 2)]
async fn main() {
    let port: u16 = std::env::args().nth(1).and_then(|p| p.parse().ok()).unwrap_or(5199);
    let d = FakeAdb::device;
    let m = FakeAdb::with_model;
    let devices = vec![
        m(d("3A281FDJH00B7C", "device"), "Pixel 8 Pro", "15", 35, 82),
        m(d("R52W70ABC1D", "device"), "Galaxy Tab S9", "14", 34, 64),
        m(d("emulator-5554", "device"), "sdk gphone64 x86 64", "15", 35, 100),
        m(d("b7e41c09", "recovery"), "OnePlus 12", "14", 34, 0),
        d("ZY22J4KQ9P", "unauthorized"),
    ];
    let fake = FakeAdb::start_on(port, devices).await;
    {
        let mut st = fake.state.lock().unwrap();
        for dir in ["/sdcard/DCIM", "/sdcard/Download", "/sdcard/Android", "/sdcard/Documents"] {
            st.dirs.insert(dir.into());
        }
        st.files.insert("/sdcard/Download/field-tools-2.4.1.apk".into(), vec![0; 18_400_000]);
        st.files.insert("/sdcard/Documents/provisioning.json".into(), b"{}".to_vec());
    }
    println!("fake adb listening on 127.0.0.1:{}", fake.port);
    std::future::pending::<()>().await;
}
