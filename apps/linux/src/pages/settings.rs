//! Preferences dialog: tool paths, server and concurrency.

use std::rc::Rc;

use adbm_core::api::ServerStatus;
use adw::prelude::*;

use crate::app::App;

pub fn show(app: &Rc<App>) {
    let cfg = app.state.borrow().config.clone();
    let server = app.state.borrow().server.clone();
    let dialog = adw::PreferencesDialog::new();
    dialog.set_title("Preferences");
    let page = adw::PreferencesPage::new();

    let tools = adw::PreferencesGroup::builder()
        .title("Android platform-tools")
        .description("Leave empty to find adb and fastboot automatically (PATH, ANDROID_HOME, the SDK folder).")
        .build();
    let adb = adw::EntryRow::builder().title("adb").text(cfg.adb_path.clone().unwrap_or_default()).build();
    let fastboot =
        adw::EntryRow::builder().title("fastboot").text(cfg.fastboot_path.clone().unwrap_or_default()).build();
    if let Some(ServerStatus::Running { version, adb_path, fastboot_path }) = &server {
        tools.set_description(Some(&format!(
            "Using {} (protocol {version}) and {}. Leave empty for automatic detection.",
            adb_path.as_deref().unwrap_or("adb"),
            fastboot_path.as_deref().unwrap_or("no fastboot")
        )));
    }
    tools.add(&adb);
    tools.add(&fastboot);

    let server_group = adw::PreferencesGroup::builder().title("adb server").build();
    let autostart = adw::SwitchRow::builder()
        .title("Start the server automatically")
        .subtitle("Runs “adb start-server” when no server is listening")
        .active(cfg.auto_start_server)
        .build();
    let port = adw::SpinRow::with_range(1.0, 65535.0, 1.0);
    port.set_title("Port");
    port.set_value(cfg.adb_port as f64);
    let restart = adw::ActionRow::builder().title("Restart adb server").activatable(true).build();
    restart.add_suffix(&gtk::Image::from_icon_name("view-refresh-symbolic"));
    restart.set_action_name(Some("win.restart-adb"));
    server_group.add(&autostart);
    server_group.add(&port);
    server_group.add(&restart);

    let perf = adw::PreferencesGroup::builder().title("Performance").build();
    let conc = adw::SpinRow::with_range(1.0, 64.0, 1.0);
    conc.set_title("Devices at once");
    conc.set_subtitle("How many devices a job works on at the same time");
    conc.set_value(cfg.max_concurrency as f64);
    perf.add(&conc);

    page.add(&tools);
    page.add(&server_group);
    page.add(&perf);
    dialog.add(&page);

    let app2 = app.clone();
    dialog.connect_closed(move |_| {
        let mut c = app2.state.borrow().config.clone();
        let opt = |s: String| (!s.trim().is_empty()).then(|| s.trim().to_string());
        c.adb_path = opt(adb.text().to_string());
        c.fastboot_path = opt(fastboot.text().to_string());
        c.auto_start_server = autostart.is_active();
        c.adb_port = port.value() as u16;
        c.max_concurrency = conc.value() as usize;
        if c != app2.state.borrow().config {
            app2.save_config(c);
            app2.toast("Preferences saved");
        }
    });
    dialog.present(Some(&app.window));
}
