//! Shell: run one command on every target and show output per device.

use std::cell::RefCell;
use std::collections::HashSet;
use std::rc::Rc;

use adbm_core::api::{JobId, Stream};
use adbm_core::{Command, Event};
use adw::prelude::*;

use crate::app::{count, App};

pub fn build(app: &Rc<App>) -> gtk::Widget {
    let entry =
        gtk::Entry::builder().hexpand(true).placeholder_text("Command, e.g. getprop ro.build.version.release").build();
    entry.add_css_class("monospace");
    let run = gtk::Button::with_label("Run");
    run.add_css_class("suggested-action");
    let clear = gtk::Button::from_icon_name("edit-clear-all-symbolic");
    clear.set_tooltip_text(Some("Clear output"));
    let targets = gtk::Label::new(None);
    targets.add_css_class("dim-label");

    let bar = gtk::Box::new(gtk::Orientation::Horizontal, 6);
    bar.append(&entry);
    bar.append(&run);
    bar.append(&clear);

    let buffer = gtk::TextBuffer::new(None);
    let err_tag = gtk::TextTag::builder().name("stderr").foreground("#B42318").build();
    let head_tag = gtk::TextTag::builder().name("head").weight(700).build();
    buffer.tag_table().add(&err_tag);
    buffer.tag_table().add(&head_tag);
    let view = gtk::TextView::builder()
        .buffer(&buffer)
        .editable(false)
        .monospace(true)
        .wrap_mode(gtk::WrapMode::WordChar)
        .top_margin(8)
        .bottom_margin(8)
        .left_margin(10)
        .right_margin(10)
        .build();
    view.add_css_class("shell-output");
    let scrolled = gtk::ScrolledWindow::builder().child(&view).vexpand(true).build();
    scrolled.add_css_class("card");

    let v = gtk::Box::new(gtk::Orientation::Vertical, 8);
    v.set_margin_top(12);
    v.set_margin_bottom(12);
    v.set_margin_start(12);
    v.set_margin_end(12);
    v.append(&bar);
    v.append(&targets);
    v.append(&scrolled);

    let jobs: Rc<RefCell<HashSet<JobId>>> = Rc::default();
    let last_serial: Rc<RefCell<Option<(JobId, String)>>> = Rc::default();

    let append = {
        let buffer = buffer.clone();
        let view = view.clone();
        move |text: &str, tag: Option<&str>| {
            let mut end = buffer.end_iter();
            match tag {
                Some(t) => buffer.insert_with_tags_by_name(&mut end, text, &[t]),
                None => buffer.insert(&mut end, text),
            }
            let mark = buffer.create_mark(None, &buffer.end_iter(), false);
            view.scroll_mark_onscreen(&mark);
            buffer.delete_mark(&mark);
        }
    };

    let submit = {
        let (app, entry, jobs, append) = (app.clone(), entry.clone(), jobs.clone(), append.clone());
        move || {
            let command = entry.text().to_string();
            if command.trim().is_empty() {
                return;
            }
            append(&format!("$ {command}\n"), Some("head"));
            if let Some(id) = app.run(Command::Shell { serials: app.targets(), command }, |_, _, _, _| {}) {
                jobs.borrow_mut().insert(id);
            }
        }
    };
    {
        let s = submit.clone();
        run.connect_clicked(move |_| s());
        entry.connect_activate(move |_| submit());
        let b = buffer.clone();
        clear.connect_clicked(move |_| b.set_text(""));
    }
    {
        let (targets, run) = (targets.clone(), run.clone());
        let update = move |app: &Rc<App>| {
            let n = app.targets().len();
            targets.set_label(&if n == 0 {
                "Select one or more devices to run commands on.".to_string()
            } else {
                format!("Runs on {}", count(n, "device", "devices"))
            });
            run.set_sensitive(n > 0);
        };
        update(app);
        app.on_targets(update);
    }
    app.on_event(move |app, ev| match ev {
        Event::JobOutput { job_id, serial, stream, text } if jobs.borrow().contains(job_id) => {
            let key = Some((*job_id, serial.clone()));
            if *last_serial.borrow() != key {
                append(&format!("── {} ──\n", app.name_of(serial)), Some("head"));
                *last_serial.borrow_mut() = key;
            }
            append(text, (*stream == Stream::Stderr).then_some("stderr"));
        }
        Event::JobDeviceFinished { job_id, serial, ok, message } if jobs.borrow().contains(job_id) && !ok => {
            append(&format!("✗ {}: {message}\n", app.name_of(serial)), Some("stderr"));
            *last_serial.borrow_mut() = None;
        }
        Event::JobFinished { job_id, summary, .. } if jobs.borrow_mut().remove(job_id) => {
            append(&format!("{summary}\n\n"), Some("head"));
            *last_serial.borrow_mut() = None;
        }
        _ => {}
    });
    v.upcast()
}
