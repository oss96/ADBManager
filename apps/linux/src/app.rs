//! Shared application state and helpers used by every page.

use std::cell::RefCell;
use std::collections::HashMap;
use std::path::PathBuf;
use std::rc::Rc;

use adbm_core::api::{Config, Device, JobId, JobStatus, ServerStatus};
use adbm_core::{Command, Core, Event, Response};
use adw::prelude::*;
use gtk::{gio, glib};
use serde_json::Value;

type JobDone = Box<dyn FnOnce(&Rc<App>, JobStatus, &str, Option<&Value>)>;
type Listener = Rc<dyn Fn(&Rc<App>, &Event)>;
type TargetListener = Rc<dyn Fn(&Rc<App>)>;
type Navigate = Box<dyn Fn(&str)>;

#[derive(Default)]
pub struct State {
    pub devices: Vec<Device>,
    /// Selected devices, in list order. Every action applies to these.
    pub targets: Vec<String>,
    pub server: Option<ServerStatus>,
    pub config: Config,
}

pub struct App {
    pub core: Core,
    pub window: adw::ApplicationWindow,
    pub toasts: adw::ToastOverlay,
    pub state: RefCell<State>,
    pending: RefCell<HashMap<JobId, JobDone>>,
    titles: RefCell<HashMap<JobId, String>>,
    /// Title of the job whose completion callback is running.
    finishing: RefCell<String>,
    listeners: RefCell<Vec<Listener>>,
    target_listeners: RefCell<Vec<TargetListener>>,
    /// Switch the visible section (set by the window once built).
    pub navigate: RefCell<Option<Navigate>>,
}

impl App {
    pub fn new(core: Core, config: Config, window: adw::ApplicationWindow, toasts: adw::ToastOverlay) -> Rc<Self> {
        Rc::new(Self {
            core,
            window,
            toasts,
            state: RefCell::new(State { config, ..Default::default() }),
            pending: RefCell::default(),
            titles: RefCell::default(),
            finishing: RefCell::default(),
            listeners: RefCell::default(),
            target_listeners: RefCell::default(),
            navigate: RefCell::default(),
        })
    }

    /// Pump core events on the GTK main loop.
    pub fn start(self: &Rc<Self>) {
        let rx = self.core.events();
        let app = self.clone();
        glib::spawn_future_local(async move {
            while let Ok(ev) = rx.recv_async().await {
                app.handle(ev);
            }
        });
    }

    fn handle(self: &Rc<Self>, ev: Event) {
        match &ev {
            Event::DevicesChanged { devices } => {
                let mut st = self.state.borrow_mut();
                st.devices = devices.clone();
                let present: Vec<&str> = devices.iter().map(|d| d.serial.as_str()).collect();
                st.targets.retain(|t| present.contains(&t.as_str()));
            }
            Event::Server { status } => self.state.borrow_mut().server = Some(status.clone()),
            Event::JobStarted { job } => {
                self.titles.borrow_mut().insert(job.id, job.title.clone());
            }
            _ => {}
        }
        let listeners: Vec<Listener> = self.listeners.borrow().clone();
        for l in listeners {
            l(self, &ev);
        }
        if let Event::JobFinished { job_id, status, summary, data } = &ev {
            let done = self.pending.borrow_mut().remove(job_id);
            *self.finishing.borrow_mut() = self.titles.borrow_mut().remove(job_id).unwrap_or_default();
            if let Some(done) = done {
                done(self, *status, summary, data.as_ref());
            }
        }
    }

    pub fn on_event(&self, f: impl Fn(&Rc<App>, &Event) + 'static) {
        self.listeners.borrow_mut().push(Rc::new(f));
    }

    pub fn on_targets(&self, f: impl Fn(&Rc<App>) + 'static) {
        self.target_listeners.borrow_mut().push(Rc::new(f));
    }

    pub fn set_targets(self: &Rc<Self>, targets: Vec<String>) {
        if self.state.borrow().targets == targets {
            return;
        }
        self.state.borrow_mut().targets = targets;
        let ls: Vec<TargetListener> = self.target_listeners.borrow().clone();
        for l in ls {
            l(self);
        }
    }

    pub fn targets(&self) -> Vec<String> {
        self.state.borrow().targets.clone()
    }

    /// The device the single-device pages (Files, Apps list) work on.
    pub fn focused(&self) -> Option<Device> {
        let st = self.state.borrow();
        let first = st.targets.first()?;
        st.devices.iter().find(|d| &d.serial == first).cloned()
    }

    pub fn device(&self, serial: &str) -> Option<Device> {
        self.state.borrow().devices.iter().find(|d| d.serial == serial).cloned()
    }

    pub fn name_of(&self, serial: &str) -> String {
        self.device(serial).map(|d| d.display_name).unwrap_or_else(|| serial.to_string())
    }

    pub fn toast(&self, text: &str) {
        let t = adw::Toast::new(text);
        t.set_timeout(4);
        self.toasts.add_toast(t);
    }

    /// Send a command. Errors are shown as a toast; a started job calls
    /// `done` when it finishes.
    pub fn run(
        self: &Rc<Self>,
        cmd: Command,
        done: impl FnOnce(&Rc<App>, JobStatus, &str, Option<&Value>) + 'static,
    ) -> Option<JobId> {
        match self.core.call(cmd) {
            Response::Job { job_id } => {
                self.pending.borrow_mut().insert(job_id, Box::new(done));
                Some(job_id)
            }
            Response::Error { message } => {
                self.toast(&message);
                None
            }
            _ => None,
        }
    }

    /// Start a user-visible job; progress is shown in Activity and a toast
    /// reports the outcome.
    pub fn run_visible(self: &Rc<Self>, cmd: Command) -> Option<JobId> {
        self.run(cmd, move |app, status, summary, _| {
            let title = app.finishing.borrow().clone();
            let text = if title.is_empty() { summary.to_string() } else { format!("{title}: {summary}") };
            let t = adw::Toast::new(&text);
            if status != JobStatus::Succeeded {
                t.set_button_label(Some("Details"));
                t.set_action_name(Some("win.show-activity"));
                t.set_timeout(8);
            }
            app.toasts.add_toast(t);
        })
    }

    pub fn go(&self, section: &str) {
        if let Some(nav) = self.navigate.borrow().as_ref() {
            nav(section);
        }
    }

    /// Ask before a destructive action. `verb` labels the confirm button.
    pub fn confirm(self: &Rc<Self>, heading: &str, body: &str, verb: &str, on_yes: impl Fn(&Rc<App>) + 'static) {
        let d = adw::AlertDialog::new(Some(heading), Some(body));
        d.add_responses(&[("cancel", "Cancel"), ("ok", verb)]);
        d.set_response_appearance("ok", adw::ResponseAppearance::Destructive);
        d.set_default_response(Some("cancel"));
        d.set_close_response("cancel");
        let app = self.clone();
        d.connect_response(None, move |_, r| {
            if r == "ok" {
                on_yes(&app);
            }
        });
        d.present(Some(&self.window));
    }

    pub fn choose_file(
        self: &Rc<Self>,
        title: &str,
        pattern: Option<(&str, &str)>,
        on_path: impl FnOnce(&Rc<App>, PathBuf) + 'static,
    ) {
        let dialog = gtk::FileDialog::builder().title(title).modal(true).build();
        if let Some((name, pat)) = pattern {
            let f = gtk::FileFilter::new();
            f.set_name(Some(name));
            f.add_pattern(pat);
            let filters = gio::ListStore::new::<gtk::FileFilter>();
            filters.append(&f);
            dialog.set_filters(Some(&filters));
        }
        let app = self.clone();
        dialog.open(Some(&self.window), gio::Cancellable::NONE, move |r| {
            if let Some(p) = r.ok().and_then(|f| f.path()) {
                on_path(&app, p);
            }
        });
    }

    pub fn choose_folder(self: &Rc<Self>, title: &str, on_path: impl FnOnce(&Rc<App>, PathBuf) + 'static) {
        let dialog = gtk::FileDialog::builder().title(title).modal(true).build();
        let app = self.clone();
        dialog.select_folder(Some(&self.window), gio::Cancellable::NONE, move |r| {
            if let Some(p) = r.ok().and_then(|f| f.path()) {
                on_path(&app, p);
            }
        });
    }

    pub fn save_config(&self, config: Config) {
        if let Response::Error { message } = self.core.call(Command::SetConfig { config: config.clone() }) {
            self.toast(&message);
            return;
        }
        if let Some(path) = config_path() {
            let _ = std::fs::create_dir_all(path.parent().unwrap());
            if let Ok(json) = serde_json::to_string_pretty(&config) {
                let _ = std::fs::write(path, json);
            }
        }
        self.state.borrow_mut().config = config;
    }
}

pub fn config_path() -> Option<PathBuf> {
    let base = std::env::var_os("XDG_CONFIG_HOME")
        .map(PathBuf::from)
        .or_else(|| std::env::var_os("HOME").map(|h| PathBuf::from(h).join(".config")))?;
    Some(base.join("adb-manager").join("config.json"))
}

pub fn load_config() -> Config {
    let mut c: Config = config_path()
        .and_then(|p| std::fs::read_to_string(p).ok())
        .and_then(|s| serde_json::from_str(&s).ok())
        .unwrap_or_default();
    // Developer override, e.g. to point the app at a fake server.
    if let Some(port) = std::env::var("ADBM_ADB_PORT").ok().and_then(|p| p.parse().ok()) {
        c.adb_port = port;
    }
    c
}

pub fn count(n: usize, one: &str, many: &str) -> String {
    if n == 1 {
        format!("1 {one}")
    } else {
        format!("{n} {many}")
    }
}

pub fn human_size(n: u64) -> String {
    const U: [&str; 5] = ["B", "KB", "MB", "GB", "TB"];
    let mut v = n as f64;
    let mut i = 0;
    while v >= 1000.0 && i < U.len() - 1 {
        v /= 1000.0;
        i += 1;
    }
    if i == 0 {
        format!("{n} B")
    } else {
        format!("{v:.1} {}", U[i])
    }
}

/// CSS class for a device state chip (matches design/tokens.json roles).
pub fn state_class(s: adbm_core::api::DeviceState) -> &'static str {
    use adbm_core::api::DeviceState::*;
    match s {
        Online => "online",
        Unauthorized | NoPermissions | Authorizing => "attention",
        Recovery | Rescue | Sideload => "recovery",
        Bootloader | Fastbootd => "fastboot",
        Offline | Connecting | Rebooting | Unknown => "muted",
    }
}

pub fn state_icon(s: adbm_core::api::DeviceState) -> &'static str {
    use adbm_core::api::DeviceState::*;
    match s {
        Online => "emblem-ok-symbolic",
        Unauthorized | NoPermissions | Authorizing => "system-lock-screen-symbolic",
        Recovery | Rescue | Sideload => "applications-engineering-symbolic",
        Bootloader | Fastbootd => "system-run-symbolic",
        Offline | Connecting | Rebooting | Unknown => "network-offline-symbolic",
    }
}

/// A state chip: glyph + label + colour (never colour alone).
pub fn chip() -> (gtk::Box, gtk::Image, gtk::Label) {
    let b = gtk::Box::new(gtk::Orientation::Horizontal, 4);
    b.add_css_class("chip");
    b.set_halign(gtk::Align::Start);
    b.set_valign(gtk::Align::Center);
    let i = gtk::Image::new();
    i.set_pixel_size(12);
    let l = gtk::Label::new(None);
    b.append(&i);
    b.append(&l);
    (b, i, l)
}

pub fn set_chip(b: &gtk::Box, i: &gtk::Image, l: &gtk::Label, d: &Device) {
    b.set_css_classes(&["chip", state_class(d.state)]);
    i.set_icon_name(Some(state_icon(d.state)));
    l.set_label(&d.state_label);
}
