//! Activity: every visible job with per-device progress, results and cancel;
//! plus the core log.

use std::cell::RefCell;
use std::collections::HashMap;
use std::rc::Rc;

use adbm_core::api::{JobId, JobInfo, JobStatus, LogLevel};
use adbm_core::{Command, Event};
use adw::prelude::*;

use crate::app::App;

struct JobRow {
    info: JobInfo,
    progress: gtk::ProgressBar,
    status: gtk::Label,
    detail: gtk::Label,
    cancel: gtk::Button,
    /// Per-device fraction done.
    parts: HashMap<String, f64>,
    failures: Vec<String>,
}

pub fn build(app: &Rc<App>, badge: gtk::Label) -> gtk::Widget {
    let list = gtk::ListBox::new();
    list.add_css_class("boxed-list");
    list.set_selection_mode(gtk::SelectionMode::None);
    let empty = adw::ActionRow::builder().title("Nothing has run yet").build();
    empty.add_css_class("dim-label");
    list.append(&empty);

    let log = gtk::TextBuffer::new(None);
    let log_view = gtk::TextView::builder().buffer(&log).editable(false).monospace(true).height_request(160).build();
    log_view.set_left_margin(8);
    log_view.set_top_margin(6);
    let log_scroll = gtk::ScrolledWindow::builder().child(&log_view).min_content_height(160).build();
    log_scroll.add_css_class("card");
    let expander = gtk::Expander::builder().label("Log").child(&log_scroll).build();

    let v = gtk::Box::new(gtk::Orientation::Vertical, 12);
    v.set_margin_top(12);
    v.set_margin_bottom(12);
    v.set_margin_start(12);
    v.set_margin_end(12);
    let title = gtk::Label::builder().label("Jobs").xalign(0.0).build();
    title.add_css_class("heading");
    v.append(&title);
    v.append(&list);
    v.append(&expander);
    let clamp = adw::Clamp::builder().maximum_size(900).child(&v).build();
    let scrolled = gtk::ScrolledWindow::builder().child(&clamp).vexpand(true).build();

    let rows: Rc<RefCell<HashMap<JobId, JobRow>>> = Rc::default();
    let running = move |rows: &HashMap<JobId, JobRow>| {
        let n = rows.values().filter(|r| r.info.status == JobStatus::Running).count();
        badge.set_label(&if n > 0 { n.to_string() } else { String::new() });
    };

    app.on_event(move |app, ev| {
        let mut rows_mut = rows.borrow_mut();
        match ev {
            Event::JobStarted { job } if job.visible => {
                if empty.parent().is_some() {
                    list.remove(&empty);
                }
                let row = gtk::Box::new(gtk::Orientation::Vertical, 6);
                row.set_margin_top(10);
                row.set_margin_bottom(10);
                row.set_margin_start(12);
                row.set_margin_end(12);
                let top = gtk::Box::new(gtk::Orientation::Horizontal, 8);
                let t = gtk::Label::builder().label(&job.title).xalign(0.0).hexpand(true).wrap(true).build();
                t.add_css_class("heading");
                let status = gtk::Label::new(Some("Running"));
                status.set_css_classes(&["chip", "fastboot"]);
                let cancel = gtk::Button::from_icon_name("process-stop-symbolic");
                cancel.set_tooltip_text(Some("Cancel"));
                cancel.add_css_class("flat");
                let (app2, id) = (app.clone(), job.id);
                cancel.connect_clicked(move |_| {
                    app2.core.call(Command::Cancel { job_id: id });
                });
                top.append(&t);
                top.append(&status);
                top.append(&cancel);
                let progress = gtk::ProgressBar::new();
                let detail = gtk::Label::builder().xalign(0.0).wrap(true).build();
                detail.add_css_class("dim-label");
                detail.add_css_class("caption");
                row.append(&top);
                row.append(&progress);
                row.append(&detail);
                list.prepend(&row);
                rows_mut.insert(
                    job.id,
                    JobRow {
                        info: job.clone(),
                        progress,
                        status,
                        detail,
                        cancel,
                        parts: HashMap::new(),
                        failures: vec![],
                    },
                );
                running(&rows_mut);
            }
            Event::JobProgress { job_id, serial, done, total, message } => {
                if let Some(r) = rows_mut.get_mut(job_id) {
                    let who = serial.as_deref().map(|s| format!("{}: ", app.name_of(s))).unwrap_or_default();
                    r.detail.set_label(&format!("{who}{message}"));
                    if *total > 0 {
                        let key = serial.clone().unwrap_or_default();
                        r.parts.insert(key, (*done as f64 / *total as f64).min(1.0));
                        set_fraction(r);
                    } else {
                        r.progress.pulse();
                    }
                }
            }
            Event::JobDeviceFinished { job_id, serial, ok, message } => {
                if let Some(r) = rows_mut.get_mut(job_id) {
                    r.parts.insert(serial.clone(), 1.0);
                    if !ok {
                        r.failures.push(format!("{}: {message}", app.name_of(serial)));
                    }
                    set_fraction(r);
                }
            }
            Event::JobFinished { job_id, status, summary, .. } => {
                if let Some(r) = rows_mut.get_mut(job_id) {
                    r.info.status = *status;
                    r.cancel.set_visible(false);
                    r.progress.set_fraction(1.0);
                    let (label, class) = match status {
                        JobStatus::Succeeded => ("Done", "online"),
                        JobStatus::PartiallyFailed => ("Partly failed", "attention"),
                        JobStatus::Failed => ("Failed", "error"),
                        JobStatus::Cancelled => ("Cancelled", "muted"),
                        JobStatus::Running => ("Running", "fastboot"),
                    };
                    r.status.set_label(label);
                    r.status.set_css_classes(&["chip", class]);
                    let mut text = summary.clone();
                    if r.failures.len() > 1 || (r.failures.len() == 1 && *status == JobStatus::PartiallyFailed) {
                        text = format!("{summary}\n{}", r.failures.join("\n"));
                    }
                    r.detail.set_label(&text);
                }
                running(&rows_mut);
            }
            Event::Log { level, message } => {
                let tag = match level {
                    LogLevel::Error => "error",
                    LogLevel::Warn => "warn",
                    LogLevel::Info => "info",
                    LogLevel::Debug => "debug",
                };
                let mut end = log.end_iter();
                log.insert(&mut end, &format!("[{tag}] {message}\n"));
            }
            _ => {}
        }
    });
    scrolled.upcast()
}

fn set_fraction(r: &JobRow) {
    let n = r.info.serials.len().max(1) as f64;
    let sum: f64 = r.parts.values().sum();
    r.progress.set_fraction((sum / n).min(1.0));
}
