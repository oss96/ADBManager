//! Routines: pick a template, fill in its files, run it on the targets.

use std::cell::RefCell;
use std::rc::Rc;

use adbm_core::routines::{Routine, Step};
use adbm_core::{Command, Response};
use adw::prelude::*;

use crate::app::{count, App};

pub struct RoutinesPage {
    pub root: gtk::Widget,
    app: Rc<App>,
    current: RefCell<Option<Routine>>,
    steps: gtk::ListBox,
    group: adw::PreferencesGroup,
    run: gtk::Button,
}

impl RoutinesPage {
    pub fn new(app: &Rc<App>) -> Rc<Self> {
        let page = adw::PreferencesPage::new();
        let pick = adw::PreferencesGroup::builder()
            .title("Routines")
            .description("A routine runs its steps in order on every target. Devices run in parallel; a device stops at its first failed step.")
            .build();
        let templates = match app.core.call(Command::RoutineTemplates) {
            Response::Routines { routines } => routines,
            _ => vec![],
        };
        let list = gtk::ListBox::new();
        list.add_css_class("boxed-list");
        list.set_selection_mode(gtk::SelectionMode::None);
        pick.add(&list);

        let group = adw::PreferencesGroup::builder().title("Steps").build();
        let run = gtk::Button::builder().label("Run").sensitive(false).build();
        run.add_css_class("suggested-action");
        run.add_css_class("pill");
        group.set_header_suffix(Some(&run));
        let steps = gtk::ListBox::new();
        steps.add_css_class("boxed-list");
        steps.set_selection_mode(gtk::SelectionMode::None);
        group.add(&steps);
        group.set_visible(false);

        page.add(&pick);
        page.add(&group);

        let me =
            Rc::new(Self { root: page.upcast(), app: app.clone(), current: RefCell::default(), steps, group, run });
        for t in templates {
            let row = adw::ActionRow::builder()
                .title(&t.name)
                .subtitle(&t.description)
                .activatable(true)
                .subtitle_lines(2)
                .build();
            row.add_prefix(&gtk::Image::from_icon_name("view-list-bullet-symbolic"));
            row.add_suffix(&gtk::Image::from_icon_name("go-next-symbolic"));
            let me2 = me.clone();
            row.connect_activated(move |_| {
                *me2.current.borrow_mut() = Some(t.clone());
                me2.render();
            });
            list.append(&row);
        }
        {
            let me2 = me.clone();
            me.run.connect_clicked(move |_| {
                let Some(r) = me2.current.borrow().clone() else { return };
                if me2.app.run_visible(Command::RunRoutine { serials: me2.app.targets(), routine: r }).is_some() {
                    me2.app.go("activity");
                }
            });
            let me3 = me.clone();
            app.on_targets(move |_| me3.update_run());
        }
        me
    }

    fn update_run(&self) {
        let n = self.app.targets().len();
        let has = self.current.borrow().is_some();
        self.run.set_label(&if n == 0 { "Run".into() } else { format!("Run on {}", count(n, "device", "devices")) });
        self.run.set_sensitive(n > 0 && has);
    }

    fn render(self: &Rc<Self>) {
        while let Some(c) = self.steps.first_child() {
            self.steps.remove(&c);
        }
        let Some(r) = self.current.borrow().clone() else {
            self.group.set_visible(false);
            return;
        };
        self.group.set_visible(true);
        self.group.set_title(&r.name);
        for (i, step) in r.steps.iter().enumerate() {
            let row = adw::ActionRow::builder().title(glib_escape(&step.title())).build();
            let n = gtk::Label::new(Some(&format!("{}", i + 1)));
            n.add_css_class("step-number");
            row.add_prefix(&n);
            match step {
                Step::Install { path } => {
                    self.file_button(&row, i, path, "Choose APK…", Some(("Android packages", "*.apk")))
                }
                Step::FastbootBoot { image } => {
                    self.file_button(&row, i, image, "Choose image…", Some(("Boot images", "*.img")))
                }
                Step::Push { local, .. } => self.folder_button(&row, i, local),
                Step::Shell { command, .. } => {
                    row.set_subtitle(&glib_escape(command));
                    row.add_css_class("monospace-subtitle");
                }
                _ => {}
            }
            self.steps.append(&row);
        }
        self.update_run();
    }

    fn file_button(
        self: &Rc<Self>,
        row: &adw::ActionRow,
        i: usize,
        value: &str,
        label: &str,
        filter: Option<(&'static str, &'static str)>,
    ) {
        row.set_subtitle(&if value.is_empty() { "Not chosen yet".into() } else { glib_escape(value) });
        let b = gtk::Button::builder().label(label).valign(gtk::Align::Center).build();
        if value.is_empty() {
            b.add_css_class("suggested-action");
        }
        let me = self.clone();
        b.connect_clicked(move |_| {
            let me2 = me.clone();
            me.app.choose_file("Choose file", filter, move |_, p| me2.set_path(i, p.display().to_string()));
        });
        row.add_suffix(&b);
    }

    fn folder_button(self: &Rc<Self>, row: &adw::ActionRow, i: usize, value: &str) {
        row.set_subtitle(&if value.is_empty() { "Not chosen yet".into() } else { glib_escape(value) });
        let b = gtk::Button::builder().label("Choose folder…").valign(gtk::Align::Center).build();
        if value.is_empty() {
            b.add_css_class("suggested-action");
        }
        let me = self.clone();
        b.connect_clicked(move |_| {
            let me2 = me.clone();
            me.app.choose_folder("Choose folder to push", move |_, p| me2.set_path(i, p.display().to_string()));
        });
        row.add_suffix(&b);
    }

    fn set_path(self: &Rc<Self>, i: usize, value: String) {
        if let Some(r) = self.current.borrow_mut().as_mut() {
            match &mut r.steps[i] {
                Step::Install { path } => *path = value,
                Step::FastbootBoot { image } => *image = value,
                Step::Push { local, .. } => *local = value,
                _ => {}
            }
        }
        self.render();
    }
}

fn glib_escape(s: &str) -> String {
    gtk::glib::markup_escape_text(s).to_string()
}
