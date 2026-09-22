//! Files: browse the focused device, push to it and pull from it.

use std::cell::RefCell;
use std::rc::Rc;

use adbm_core::api::{DirEntry, EntryKind, JobStatus};
use adbm_core::Command;
use adw::prelude::*;

use crate::app::{human_size, App};

pub struct FilesPage {
    pub root: gtk::Widget,
    app: Rc<App>,
    path: RefCell<String>,
    entry: gtk::Entry,
    list: gtk::ListBox,
    status: gtk::Label,
    device_label: gtk::Label,
}

impl FilesPage {
    pub fn new(app: &Rc<App>) -> Rc<Self> {
        let up = gtk::Button::from_icon_name("go-up-symbolic");
        up.set_tooltip_text(Some("Parent folder"));
        let entry = gtk::Entry::builder().hexpand(true).text("/sdcard").build();
        entry.add_css_class("monospace");
        let refresh = gtk::Button::from_icon_name("view-refresh-symbolic");
        refresh.set_tooltip_text(Some("Reload"));
        let mkdir = gtk::Button::from_icon_name("folder-new-symbolic");
        mkdir.set_tooltip_text(Some("New folder"));
        let push_file = gtk::Button::with_label("Push file…");
        let push_dir = gtk::Button::with_label("Push folder…");

        let bar = gtk::Box::new(gtk::Orientation::Horizontal, 6);
        bar.append(&up);
        bar.append(&entry);
        bar.append(&refresh);
        bar.append(&mkdir);
        bar.append(&push_file);
        bar.append(&push_dir);

        let device_label = gtk::Label::builder().xalign(0.0).build();
        device_label.add_css_class("dim-label");
        let list = gtk::ListBox::new();
        list.add_css_class("boxed-list");
        list.set_selection_mode(gtk::SelectionMode::None);
        let status = gtk::Label::builder().xalign(0.0).build();
        status.add_css_class("dim-label");

        let v = gtk::Box::new(gtk::Orientation::Vertical, 10);
        v.set_margin_top(12);
        v.set_margin_bottom(12);
        v.set_margin_start(12);
        v.set_margin_end(12);
        v.append(&bar);
        v.append(&device_label);
        v.append(&list);
        v.append(&status);
        let clamp = adw::Clamp::builder().maximum_size(1000).child(&v).build();
        let scrolled = gtk::ScrolledWindow::builder().child(&clamp).vexpand(true).build();

        let me = Rc::new(Self {
            root: scrolled.upcast(),
            app: app.clone(),
            path: RefCell::new("/sdcard".into()),
            entry,
            list,
            status,
            device_label,
        });
        {
            let me2 = me.clone();
            up.connect_clicked(move |_| {
                let p = me2.path.borrow().clone();
                let parent = match p.trim_end_matches('/').rsplit_once('/') {
                    Some(("", _)) | None => "/".to_string(),
                    Some((a, _)) => a.to_string(),
                };
                me2.open(&parent);
            });
            let me3 = me.clone();
            me.entry.connect_activate(move |e| me3.open(&e.text()));
            let me4 = me.clone();
            refresh.connect_clicked(move |_| me4.reload());
            let me5 = me.clone();
            mkdir.connect_clicked(move |_| me5.new_folder());
            let me6 = me.clone();
            push_file.connect_clicked(move |_| {
                let me = me6.clone();
                me6.app.choose_file("Push a file", None, move |app, p| me.push(app, p));
            });
            let me7 = me.clone();
            push_dir.connect_clicked(move |_| {
                let me = me7.clone();
                me7.app.choose_folder("Push a folder", move |app, p| me.push(app, p));
            });
            let me8 = me.clone();
            app.on_targets(move |_| me8.reload());
        }
        me.reload();
        me
    }

    fn open(self: &Rc<Self>, path: &str) {
        let p = if path.trim().is_empty() { "/".to_string() } else { path.trim().to_string() };
        *self.path.borrow_mut() = p;
        self.reload();
    }

    pub fn reload(self: &Rc<Self>) {
        let path = self.path.borrow().clone();
        self.entry.set_text(&path);
        let Some(d) = self.app.focused() else {
            self.device_label.set_label("Select a device to browse its files.");
            self.clear();
            return;
        };
        if !d.state.is_adb_usable() {
            self.device_label.set_label(&format!("{} is {}.", d.display_name, d.state_label.to_lowercase()));
            self.clear();
            return;
        }
        let more = self.app.targets().len().saturating_sub(1);
        let note = if more > 0 { format!(" · pushes go to all {} targets", more + 1) } else { String::new() };
        self.device_label.set_label(&format!("Browsing {}{note}", d.display_name));
        self.status.set_label("Loading…");
        let me = self.clone();
        self.app.run(Command::ListDir { serial: d.serial, path }, move |_, status, summary, data| {
            if status != JobStatus::Succeeded {
                me.clear();
                me.status.set_label(summary);
                return;
            }
            let entries: Vec<DirEntry> =
                data.and_then(|v| serde_json::from_value(v["entries"].clone()).ok()).unwrap_or_default();
            me.render(&entries);
            me.status.set_label(summary);
        });
    }

    fn clear(&self) {
        while let Some(c) = self.list.first_child() {
            self.list.remove(&c);
        }
        self.status.set_label("");
    }

    fn render(self: &Rc<Self>, entries: &[DirEntry]) {
        self.clear();
        let base = self.path.borrow().clone();
        let join = |n: &str| if base.ends_with('/') { format!("{base}{n}") } else { format!("{base}/{n}") };
        for e in entries {
            let full = join(&e.name);
            let row = adw::ActionRow::builder().title(glib_escape(&e.name)).build();
            let icon = match e.kind {
                EntryKind::Dir => "folder-symbolic",
                EntryKind::Symlink => "emblem-symbolic-link-symbolic",
                _ => "text-x-generic-symbolic",
            };
            row.add_prefix(&gtk::Image::from_icon_name(icon));
            if e.kind == EntryKind::File {
                row.set_subtitle(&human_size(e.size));
            }
            if e.kind == EntryKind::Dir || e.kind == EntryKind::Symlink {
                row.set_activatable(true);
                let (me, full) = (self.clone(), full.clone());
                row.connect_activated(move |_| me.open(&full));
            }
            let pull = gtk::Button::from_icon_name("document-save-symbolic");
            pull.set_tooltip_text(Some("Pull to this computer"));
            pull.add_css_class("flat");
            pull.set_valign(gtk::Align::Center);
            {
                let (me, full) = (self.clone(), full.clone());
                pull.connect_clicked(move |_| me.pull(&full));
            }
            let del = gtk::Button::from_icon_name("user-trash-symbolic");
            del.set_tooltip_text(Some("Delete"));
            del.add_css_class("flat");
            del.set_valign(gtk::Align::Center);
            {
                let (me, full, name) = (self.clone(), full.clone(), e.name.clone());
                del.connect_clicked(move |_| me.delete(&full, &name));
            }
            row.add_suffix(&pull);
            row.add_suffix(&del);
            self.list.append(&row);
        }
        if entries.is_empty() {
            let r = adw::ActionRow::builder().title("This folder is empty").build();
            r.add_css_class("dim-label");
            self.list.append(&r);
        }
    }

    fn pull(self: &Rc<Self>, remote: &str) {
        let Some(d) = self.app.focused() else { return };
        let remote = remote.to_string();
        self.app.choose_folder("Pull to folder", move |app, dir| {
            app.run_visible(Command::Pull { serial: d.serial, remote, local: dir.display().to_string() });
        });
    }

    fn push(self: &Rc<Self>, app: &Rc<App>, local: std::path::PathBuf) {
        let remote = format!("{}/", self.path.borrow().trim_end_matches('/'));
        let me = self.clone();
        app.run(
            Command::Push { serials: app.targets(), local: local.display().to_string(), remote },
            move |app, _, s, _| {
                app.toast(s);
                me.reload();
            },
        );
    }

    fn delete(self: &Rc<Self>, path: &str, name: &str) {
        let Some(d) = self.app.focused() else { return };
        let (me, path) = (self.clone(), path.to_string());
        self.app.confirm(
            &format!("Delete {name}?"),
            &format!("It is removed from {}. This can't be undone.", d.display_name),
            "Delete",
            move |app| {
                let me = me.clone();
                app.run(Command::Delete { serial: d.serial.clone(), path: path.clone() }, move |app, _, s, _| {
                    app.toast(s);
                    me.reload();
                });
            },
        );
    }

    fn new_folder(self: &Rc<Self>) {
        let dialog = adw::AlertDialog::new(Some("New folder"), None);
        let entry = gtk::Entry::builder().placeholder_text("Folder name").activates_default(true).build();
        dialog.set_extra_child(Some(&entry));
        dialog.add_responses(&[("cancel", "Cancel"), ("create", "Create")]);
        dialog.set_response_appearance("create", adw::ResponseAppearance::Suggested);
        dialog.set_default_response(Some("create"));
        dialog.set_close_response("cancel");
        let me = self.clone();
        dialog.connect_response(None, move |_, r| {
            let name = entry.text().trim().to_string();
            if r != "create" || name.is_empty() {
                return;
            }
            let path = format!("{}/{name}", me.path.borrow().trim_end_matches('/'));
            let me2 = me.clone();
            me.app.run(Command::MakeDir { serials: me.app.targets(), path }, move |app, _, s, _| {
                app.toast(s);
                me2.reload();
            });
        });
        dialog.present(Some(&self.app.window));
    }
}

/// ActionRow titles are Pango markup.
fn glib_escape(s: &str) -> String {
    gtk::glib::markup_escape_text(s).to_string()
}
