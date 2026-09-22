//! Apps: install an APK on the targets; list and uninstall packages of the
//! focused device.

use std::cell::RefCell;
use std::path::PathBuf;
use std::rc::Rc;

use adbm_core::api::{JobStatus, Package};
use adbm_core::apk::ApkInfo;
use adbm_core::{Command, Response};
use adw::prelude::*;

use crate::app::{count, human_size, App};

pub struct AppsPage {
    pub root: gtk::Widget,
    app: Rc<App>,
    apk: RefCell<Option<ApkInfo>>,
    apk_row: adw::ActionRow,
    install: gtk::Button,
    downgrade: adw::SwitchRow,
    list: gtk::ListBox,
    list_group: adw::PreferencesGroup,
    search: gtk::SearchEntry,
    system: gtk::CheckButton,
    packages: RefCell<Vec<Package>>,
}

impl AppsPage {
    pub fn new(app: &Rc<App>) -> Rc<Self> {
        let page = adw::PreferencesPage::new();

        let install_group = adw::PreferencesGroup::builder()
            .title("Install")
            .description("Drop an APK anywhere in the window, or choose one.")
            .build();
        let apk_row = adw::ActionRow::builder().title("No APK chosen").subtitle("Android package (.apk)").build();
        apk_row.add_prefix(&gtk::Image::from_icon_name("application-x-addon-symbolic"));
        let choose = gtk::Button::builder().label("Choose…").valign(gtk::Align::Center).build();
        apk_row.add_suffix(&choose);
        let downgrade = adw::SwitchRow::builder()
            .title("Allow downgrade")
            .subtitle("Install even if a newer version is on the device")
            .build();
        let install = gtk::Button::builder().label("Install").halign(gtk::Align::End).sensitive(false).build();
        install.add_css_class("suggested-action");
        install.add_css_class("pill");
        install_group.add(&apk_row);
        install_group.add(&downgrade);
        install_group.set_header_suffix(Some(&install));

        let list_group = adw::PreferencesGroup::builder().title("Installed apps").build();
        let search = gtk::SearchEntry::builder().placeholder_text("Filter packages").hexpand(true).build();
        let system = gtk::CheckButton::with_label("System apps");
        let refresh = gtk::Button::from_icon_name("view-refresh-symbolic");
        refresh.set_tooltip_text(Some("Reload"));
        refresh.add_css_class("flat");
        let bar = gtk::Box::new(gtk::Orientation::Horizontal, 8);
        bar.set_margin_bottom(8);
        bar.append(&search);
        bar.append(&system);
        bar.append(&refresh);
        let list = gtk::ListBox::new();
        list.add_css_class("boxed-list");
        list.set_selection_mode(gtk::SelectionMode::None);
        let v = gtk::Box::new(gtk::Orientation::Vertical, 0);
        v.append(&bar);
        v.append(&list);
        list_group.add(&v);

        page.add(&install_group);
        page.add(&list_group);

        let me = Rc::new(Self {
            root: page.upcast(),
            app: app.clone(),
            apk: RefCell::default(),
            apk_row,
            install,
            downgrade,
            list,
            list_group,
            search,
            system,
            packages: RefCell::default(),
        });

        {
            let me = me.clone();
            let app2 = app.clone();
            choose.connect_clicked(move |_| {
                let me = me.clone();
                app2.choose_file("Choose an APK", Some(("Android packages", "*.apk")), move |app, p| {
                    me.set_apk(app, p)
                });
            });
        }
        {
            let me = me.clone();
            let app = app.clone();
            me.clone().install.connect_clicked(move |_| {
                let Some(apk) = me.apk.borrow().clone() else { return };
                app.run_visible(Command::Install {
                    serials: app.targets(),
                    path: apk.path,
                    allow_downgrade: me.downgrade.is_active(),
                });
                app.go("activity");
            });
        }
        {
            let me2 = me.clone();
            me.search.connect_search_changed(move |_| me2.render());
            let (me3, app3) = (me.clone(), app.clone());
            me.system.connect_toggled(move |_| me3.reload(&app3));
            let (me4, app4) = (me.clone(), app.clone());
            refresh.connect_clicked(move |_| me4.reload(&app4));
            let me5 = me.clone();
            app.on_targets(move |app| {
                me5.update_install_label(app);
                me5.reload(app);
            });
        }
        me.update_install_label(app);
        me
    }

    pub fn set_apk(&self, app: &Rc<App>, path: PathBuf) {
        match app.core.call(Command::InspectApk { path: path.display().to_string() }) {
            Response::Apk { apk } => {
                let title = apk.label.clone().unwrap_or_else(|| apk.file_name.clone());
                self.apk_row.set_title(&title);
                let version = apk.version_name.clone().map(|v| format!(" {v}")).unwrap_or_default();
                self.apk_row.set_subtitle(&format!("{}{version} · {}", apk.package, human_size(apk.size)));
                *self.apk.borrow_mut() = Some(apk);
            }
            Response::Error { message } => app.toast(&message),
            _ => {}
        }
        self.update_install_label(app);
    }

    fn update_install_label(&self, app: &Rc<App>) {
        let n = app.targets().len();
        self.install.set_label(&if n == 0 {
            "Install".into()
        } else {
            format!("Install on {}", count(n, "device", "devices"))
        });
        self.install.set_sensitive(n > 0 && self.apk.borrow().is_some());
    }

    fn reload(self: &Rc<Self>, app: &Rc<App>) {
        let Some(d) = app.focused() else {
            self.list_group.set_description(Some("Select a device to see its apps."));
            self.packages.borrow_mut().clear();
            self.render();
            return;
        };
        if !d.state.is_adb_usable() {
            self.list_group.set_description(Some(&format!("{} is {}.", d.display_name, d.state_label.to_lowercase())));
            self.packages.borrow_mut().clear();
            self.render();
            return;
        }
        self.list_group.set_description(Some(&format!("On {}", d.display_name)));
        let me = self.clone();
        let serial = d.serial.clone();
        app.run(
            Command::ListPackages { serial, include_system: self.system.is_active() },
            move |app, status, summary, data| {
                if status != JobStatus::Succeeded {
                    app.toast(summary);
                    return;
                }
                let pkgs: Vec<Package> = data.and_then(|v| serde_json::from_value(v.clone()).ok()).unwrap_or_default();
                *me.packages.borrow_mut() = pkgs;
                me.render();
            },
        );
    }

    fn render(self: &Rc<Self>) {
        while let Some(c) = self.list.first_child() {
            self.list.remove(&c);
        }
        let q = self.search.text().to_lowercase();
        let pkgs = self.packages.borrow();
        let shown: Vec<&Package> =
            pkgs.iter().filter(|p| q.is_empty() || p.name.to_lowercase().contains(&q)).take(500).collect();
        if shown.is_empty() {
            let r = adw::ActionRow::builder()
                .title(if pkgs.is_empty() { "No apps loaded" } else { "No matching apps" })
                .build();
            r.add_css_class("dim-label");
            self.list.append(&r);
            return;
        }
        for p in shown {
            let r = adw::ActionRow::builder().title(&p.name).build();
            r.add_css_class("monospace-title");
            if p.system {
                r.set_subtitle("System app");
            }
            let del = gtk::Button::from_icon_name("user-trash-symbolic");
            del.set_tooltip_text(Some("Uninstall from targets"));
            del.add_css_class("flat");
            del.set_valign(gtk::Align::Center);
            let name = p.name.clone();
            let me = self.clone();
            let app = self.app.clone();
            del.connect_clicked(move |_| {
                let targets = app.targets();
                let heading = format!("Uninstall {name} from {}?", count(targets.len(), "device", "devices"));
                let name2 = name.clone();
                let me = me.clone();
                app.confirm(&heading, "The app and its data are removed.", "Uninstall", move |app| {
                    let me = me.clone();
                    app.run(
                        Command::Uninstall { serials: app.targets(), package: name2.clone(), keep_data: false },
                        move |app, _, summary, _| {
                            app.toast(summary);
                            me.reload(app);
                        },
                    );
                });
            });
            r.add_suffix(&del);
            self.list.append(&r);
        }
    }
}
