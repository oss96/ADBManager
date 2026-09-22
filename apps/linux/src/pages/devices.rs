//! Devices: the fleet table, empty / adb-missing states and the inspector.

use std::cell::Cell;
use std::rc::Rc;

use adbm_core::api::{Device, DeviceState, RebootMode, ServerStatus};
use adbm_core::{Command, Event};
use adw::prelude::*;
use gtk::{gio, glib, pango};

use crate::app::{chip, set_chip, App};

pub fn build(app: &Rc<App>) -> gtk::Widget {
    let store = gio::ListStore::new::<glib::BoxedAnyObject>();
    let selection = gtk::MultiSelection::new(Some(store.clone()));
    let view = gtk::ColumnView::new(Some(selection.clone()));
    view.set_show_row_separators(true);
    view.add_css_class("fleet");
    view.set_reorderable(false);

    add_column(&view, "Device", true, |b| {
        let name = gtk::Label::builder().xalign(0.0).ellipsize(pango::EllipsizeMode::End).build();
        name.add_css_class("heading");
        b.append(&name);
        Box::new(move |d: &Device| name.set_label(&d.display_name))
    });
    add_column(&view, "Serial", false, |b| {
        let l = gtk::Label::builder().xalign(0.0).selectable(false).build();
        l.add_css_class("monospace");
        l.add_css_class("dim-label");
        b.append(&l);
        Box::new(move |d: &Device| l.set_label(&d.serial))
    });
    add_column(&view, "State", false, |b| {
        let (c, i, l) = chip();
        b.append(&c);
        Box::new(move |d: &Device| set_chip(&c, &i, &l, d))
    });
    add_column(&view, "Android", false, |b| {
        let l = gtk::Label::builder().xalign(0.0).build();
        l.add_css_class("numeric");
        b.append(&l);
        Box::new(move |d: &Device| {
            l.set_label(&match (&d.info.android_version, d.info.sdk) {
                (Some(v), Some(s)) => format!("{v} · API {s}"),
                (Some(v), None) => v.clone(),
                _ => "—".into(),
            })
        })
    });
    add_column(&view, "Battery", false, |b| {
        let l = gtk::Label::builder().xalign(0.0).build();
        l.add_css_class("numeric");
        b.append(&l);
        Box::new(move |d: &Device| {
            l.set_label(&match (d.info.battery_level, d.state == DeviceState::Online) {
                (Some(p), true) => format!("{p}%"),
                _ => "—".into(),
            })
        })
    });

    let scrolled = gtk::ScrolledWindow::builder().child(&view).vexpand(true).build();
    let hint = gtk::Label::builder().xalign(0.0).wrap(true).margin_start(12).margin_end(12).margin_bottom(8).build();
    hint.add_css_class("dim-label");
    hint.set_visible(false);
    let list_box = gtk::Box::new(gtk::Orientation::Vertical, 0);
    list_box.append(&scrolled);
    list_box.append(&hint);

    let empty = adw::StatusPage::builder()
        .icon_name("phone-symbolic")
        .title("No devices")
        .description("Connect a device with USB debugging enabled, or connect over the network.")
        .build();
    let empty_btn = gtk::Button::builder().label("Connect over network…").halign(gtk::Align::Center).build();
    empty_btn.add_css_class("pill");
    empty_btn.set_action_name(Some("win.connect"));
    empty.set_child(Some(&empty_btn));

    let noadb = adw::StatusPage::builder()
        .icon_name("dialog-warning-symbolic")
        .title("adb was not found")
        .description("Install Android platform-tools, or choose the adb program.")
        .build();
    let choose = gtk::Button::builder().label("Choose adb…").halign(gtk::Align::Center).build();
    choose.add_css_class("pill");
    choose.add_css_class("suggested-action");
    noadb.set_child(Some(&choose));
    {
        let app = app.clone();
        choose.connect_clicked(move |_| {
            app.choose_file("Choose adb", None, |app, p| {
                let mut c = app.state.borrow().config.clone();
                c.adb_path = Some(p.display().to_string());
                app.save_config(c);
            });
        });
    }

    let starting = adw::StatusPage::builder().title("Starting adb…").build();
    let spinner = gtk::Spinner::builder().spinning(true).width_request(32).height_request(32).build();
    starting.set_child(Some(&spinner));

    let error = adw::StatusPage::builder().icon_name("network-error-symbolic").title("adb is not responding").build();
    let restart = gtk::Button::builder().label("Restart adb").halign(gtk::Align::Center).build();
    restart.add_css_class("pill");
    restart.set_action_name(Some("win.restart-adb"));
    error.set_child(Some(&restart));

    let stack = gtk::Stack::new();
    stack.add_named(&list_box, Some("list"));
    stack.add_named(&empty, Some("empty"));
    stack.add_named(&noadb, Some("noadb"));
    stack.add_named(&starting, Some("starting"));
    stack.add_named(&error, Some("error"));
    stack.set_visible_child_name("starting");

    // Inspector
    let inspector = Inspector::new(app);
    let split = adw::OverlaySplitView::builder()
        .content(&stack)
        .sidebar(&inspector.root)
        .sidebar_position(gtk::PackType::End)
        .min_sidebar_width(260.0)
        .max_sidebar_width(320.0)
        .build();

    // Selection → targets. `updating` guards programmatic re-selection.
    let updating = Rc::new(Cell::new(false));
    {
        let app = app.clone();
        let updating = updating.clone();
        selection.connect_selection_changed(move |sel, _, _| {
            if updating.get() {
                return;
            }
            let mut t = Vec::new();
            for i in 0..sel.n_items() {
                if sel.is_selected(i) {
                    if let Some(o) = sel.item(i).and_downcast::<glib::BoxedAnyObject>() {
                        t.push(o.borrow::<Device>().serial.clone());
                    }
                }
            }
            app.set_targets(t);
        });
    }

    // Device list and server state updates.
    {
        let (store, selection, stack, hint) = (store.clone(), selection.clone(), stack.clone(), hint.clone());
        let updating = updating.clone();
        app.on_event(move |app, ev| match ev {
            Event::DevicesChanged { devices } => {
                updating.set(true);
                let objs: Vec<glib::BoxedAnyObject> = devices.iter().cloned().map(glib::BoxedAnyObject::new).collect();
                store.splice(0, store.n_items(), &objs);
                let targets = app.targets();
                for (i, d) in devices.iter().enumerate() {
                    if targets.contains(&d.serial) {
                        selection.select_item(i as u32, false);
                    }
                }
                updating.set(false);
                let hints: Vec<String> = devices
                    .iter()
                    .filter_map(|d| d.state_hint.as_ref().map(|h| format!("{}: {h}", d.display_name)))
                    .collect();
                hint.set_label(&hints.join("\n"));
                hint.set_visible(!hints.is_empty());
                show(&stack, app);
            }
            Event::Server { .. } => show(&stack, app),
            _ => {}
        });
    }
    {
        let inspector2 = inspector.clone();
        app.on_targets(move |app| inspector.update(app));
        app.on_event(move |app, ev| {
            if matches!(ev, Event::DevicesChanged { .. }) {
                inspector2.update(app);
            }
        });
    }
    split.upcast()
}

fn show(stack: &gtk::Stack, app: &Rc<App>) {
    let st = app.state.borrow();
    let page = match &st.server {
        Some(ServerStatus::AdbNotFound { .. }) => "noadb",
        Some(ServerStatus::Error { .. }) if st.devices.is_empty() => "error",
        Some(ServerStatus::Starting) | None if st.devices.is_empty() => "starting",
        _ if st.devices.is_empty() => "empty",
        _ => "list",
    };
    if let (Some(ServerStatus::Error { message }), "error") = (&st.server, page) {
        if let Some(p) = stack.child_by_name("error").and_downcast::<adw::StatusPage>() {
            p.set_description(Some(message));
        }
    }
    stack.set_visible_child_name(page);
}

type Binder = Box<dyn Fn(&Device)>;

/// A column whose cell widgets are built once per row and refilled whenever
/// the row shows a different device.
fn add_column(view: &gtk::ColumnView, title: &str, expand: bool, make: fn(&gtk::Box) -> Binder) {
    let factory = gtk::SignalListItemFactory::new();
    factory.connect_setup(move |_, item| {
        let item = item.downcast_ref::<gtk::ListItem>().unwrap();
        let b = gtk::Box::new(gtk::Orientation::Horizontal, 6);
        b.set_margin_start(6);
        b.set_margin_end(6);
        let binder = make(&b);
        item.set_child(Some(&b));
        item.connect_item_notify(move |item| {
            if let Some(obj) = item.item().and_downcast::<glib::BoxedAnyObject>() {
                binder(&obj.borrow::<Device>());
            }
        });
    });
    let col = gtk::ColumnViewColumn::new(Some(title), Some(factory));
    col.set_expand(expand);
    col.set_resizable(true);
    view.append_column(&col);
}

#[derive(Clone)]
struct Inspector {
    root: gtk::Widget,
    stack: gtk::Stack,
    title: gtk::Label,
    subtitle: gtk::Label,
    rows: Rc<[adw::ActionRow; 6]>,
    chip: (gtk::Box, gtk::Image, gtk::Label),
    imei: gtk::Button,
}

impl Inspector {
    fn new(app: &Rc<App>) -> Self {
        let b = gtk::Box::new(gtk::Orientation::Vertical, 12);
        b.set_margin_top(16);
        b.set_margin_bottom(16);
        b.set_margin_start(16);
        b.set_margin_end(16);
        let title = gtk::Label::builder().xalign(0.0).wrap(true).build();
        title.add_css_class("title-3");
        let subtitle = gtk::Label::builder().xalign(0.0).selectable(true).build();
        subtitle.add_css_class("monospace");
        subtitle.add_css_class("dim-label");
        b.append(&title);
        b.append(&subtitle);

        let list = gtk::ListBox::new();
        list.add_css_class("boxed-list");
        list.set_selection_mode(gtk::SelectionMode::None);
        let mk = |t: &str| {
            let r = adw::ActionRow::builder().title(t).build();
            r.add_css_class("property");
            list.append(&r);
            r
        };
        let state_row = mk("State");
        let chip = chip();
        state_row.add_suffix(&chip.0);
        let rows = Rc::new([state_row, mk("Android"), mk("Build"), mk("Battery"), mk("Connection"), mk("IMEI")]);
        let imei = gtk::Button::builder().label("Read").valign(gtk::Align::Center).build();
        imei.add_css_class("flat");
        rows[5].add_suffix(&imei);
        b.append(&list);

        let qa_title = gtk::Label::builder().label("Quick actions").xalign(0.0).build();
        qa_title.add_css_class("heading");
        b.append(&qa_title);
        let grid = gtk::Grid::builder().row_spacing(6).column_spacing(6).column_homogeneous(true).build();
        let actions: [(&str, &str, &str); 4] = [
            ("Shell", "utilities-terminal-symbolic", "shell"),
            ("Files", "folder-symbolic", "files"),
            ("Recovery", "view-refresh-symbolic", "recovery"),
            ("Bootloader", "system-run-symbolic", "bootloader"),
        ];
        for (i, (label, icon, what)) in actions.into_iter().enumerate() {
            let content = adw::ButtonContent::builder().label(label).icon_name(icon).build();
            let btn = gtk::Button::builder().child(&content).build();
            let app = app.clone();
            btn.connect_clicked(move |_| match what {
                "shell" | "files" => app.go(what),
                "recovery" => {
                    app.run_visible(Command::Reboot { serials: app.targets(), mode: RebootMode::Recovery });
                }
                _ => {
                    app.run_visible(Command::Reboot { serials: app.targets(), mode: RebootMode::Bootloader });
                }
            });
            grid.attach(&btn, (i % 2) as i32, (i / 2) as i32, 1, 1);
        }
        b.append(&grid);

        let placeholder = adw::StatusPage::builder()
            .icon_name("phone-symbolic")
            .title("No device selected")
            .description("Select one or more devices. Actions apply to every selected device.")
            .build();
        placeholder.add_css_class("compact");

        let stack = gtk::Stack::new();
        let scrolled = gtk::ScrolledWindow::builder().child(&b).hscrollbar_policy(gtk::PolicyType::Never).build();
        stack.add_named(&scrolled, Some("device"));
        stack.add_named(&placeholder, Some("none"));
        stack.set_visible_child_name("none");
        stack.add_css_class("inspector");

        let me = Self { root: stack.clone().upcast(), stack, title, subtitle, rows, chip, imei };
        {
            let me2 = me.clone();
            let app = app.clone();
            me.imei.connect_clicked(move |btn| {
                let Some(d) = app.focused() else { return };
                btn.set_sensitive(false);
                let me3 = me2.clone();
                app.run(Command::ReadImei { serial: d.serial }, move |_, _, summary, _| {
                    me3.rows[5].set_subtitle(summary);
                    me3.imei.set_sensitive(true);
                });
            });
        }
        me
    }

    fn update(&self, app: &Rc<App>) {
        let targets = app.targets();
        let Some(d) = app.focused() else {
            self.stack.set_visible_child_name("none");
            return;
        };
        self.stack.set_visible_child_name("device");
        let extra = if targets.len() > 1 { format!("  +{} more", targets.len() - 1) } else { String::new() };
        self.title.set_label(&format!("{}{extra}", d.display_name));
        let code = d.device.clone().or(d.product.clone()).map(|c| format!("{c} · ")).unwrap_or_default();
        self.subtitle.set_label(&format!("{code}{}", d.serial));
        set_chip(&self.chip.0, &self.chip.1, &self.chip.2, &d);
        let dash = || "—".to_string();
        let i = &d.info;
        self.rows[1].set_subtitle(&match (&i.android_version, i.sdk) {
            (Some(v), Some(s)) => format!("{v} (API {s})"),
            (Some(v), None) => v.clone(),
            _ => dash(),
        });
        self.rows[2].set_subtitle(&i.build_id.clone().unwrap_or_else(dash));
        self.rows[3].set_subtitle(&match (i.battery_level, i.charging) {
            (Some(l), Some(true)) => format!("{l}% · charging"),
            (Some(l), _) => format!("{l}%"),
            _ => dash(),
        });
        let link = match d.link {
            adbm_core::api::Link::Usb => "USB",
            adbm_core::api::Link::Tcp => "Network",
            adbm_core::api::Link::Emulator => "Emulator",
        };
        self.rows[4].set_subtitle(&match d.transport_id {
            Some(t) => format!("{link} · transport {t}"),
            None => link.to_string(),
        });
        self.imei.set_sensitive(d.state.is_adb_usable());
    }
}
