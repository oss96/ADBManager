//! ADB Manager for Linux: GTK 4 + libadwaita 1.5, linking adbm-core directly.

mod app;
mod pages;

use std::rc::Rc;

use adbm_core::api::RebootMode;
use adbm_core::{Command, Core, Event};
use adw::prelude::*;
use gtk::{gdk, gio, glib};

use app::{count, App};

type Action = Box<dyn Fn(&Rc<App>)>;

const APP_ID: &str = "io.github.oss96.ADBManager";

/// Sections in the sidebar: (id, title, icon). Matches design/DESIGN.md.
const SECTIONS: [(&str, &str, &str); 6] = [
    ("devices", "Devices", "phone-symbolic"),
    ("apps", "Apps", "view-app-grid-symbolic"),
    ("files", "Files", "folder-symbolic"),
    ("shell", "Shell", "utilities-terminal-symbolic"),
    ("routines", "Routines", "view-list-bullet-symbolic"),
    ("activity", "Activity", "document-open-recent-symbolic"),
];

fn main() -> glib::ExitCode {
    let app = adw::Application::builder().application_id(APP_ID).build();
    app.connect_startup(|_| style::install());
    app.connect_activate(build);
    app.run()
}

fn build(gapp: &adw::Application) {
    if let Some(w) = gapp.active_window() {
        w.present();
        return;
    }
    let config = app::load_config();
    let core = match Core::new(config.clone()) {
        Ok(c) => c,
        Err(e) => {
            eprintln!("could not start: {e}");
            return;
        }
    };
    let window = adw::ApplicationWindow::builder()
        .application(gapp)
        .title("ADB Manager")
        .default_width(1180)
        .default_height(720)
        .width_request(360)
        .height_request(420)
        .build();
    let toasts = adw::ToastOverlay::new();
    let app = App::new(core, config, window.clone(), toasts.clone());

    // ---- content pages
    let stack = gtk::Stack::builder().transition_type(gtk::StackTransitionType::Crossfade).build();
    let activity_badge = gtk::Label::new(None);
    activity_badge.add_css_class("badge");
    let apps_page = pages::apps::AppsPage::new(&app);
    let files_page = pages::files::FilesPage::new(&app);
    let routines_page = pages::routines::RoutinesPage::new(&app);
    stack.add_named(&pages::devices::build(&app), Some("devices"));
    stack.add_named(&apps_page.root, Some("apps"));
    stack.add_named(&files_page.root, Some("files"));
    stack.add_named(&pages::shell::build(&app), Some("shell"));
    stack.add_named(&routines_page.root, Some("routines"));
    stack.add_named(&pages::activity::build(&app, activity_badge.clone()), Some("activity"));

    // ---- sidebar
    let nav = gtk::ListBox::new();
    nav.add_css_class("navigation-sidebar");
    let device_badge = gtk::Label::new(None);
    device_badge.add_css_class("badge");
    for (id, title, icon) in SECTIONS {
        let b = gtk::Box::new(gtk::Orientation::Horizontal, 10);
        b.append(&gtk::Image::from_icon_name(icon));
        b.append(&gtk::Label::builder().label(title).xalign(0.0).hexpand(true).build());
        match id {
            "devices" => b.append(&device_badge),
            "activity" => b.append(&activity_badge),
            _ => {}
        }
        let row = gtk::ListBoxRow::builder().child(&b).build();
        row.set_widget_name(id);
        nav.append(&row);
    }
    let menu = gio::Menu::new();
    menu.append(Some("Connect over Network…"), Some("win.connect"));
    menu.append(Some("Restart adb Server"), Some("win.restart-adb"));
    let section = gio::Menu::new();
    section.append(Some("Preferences"), Some("win.preferences"));
    section.append(Some("About ADB Manager"), Some("win.about"));
    menu.append_section(None, &section);
    let menu_btn =
        gtk::MenuButton::builder().icon_name("open-menu-symbolic").menu_model(&menu).tooltip_text("Main menu").build();
    let side_header = adw::HeaderBar::new();
    side_header.pack_end(&menu_btn);
    let side_view = adw::ToolbarView::new();
    side_view.add_top_bar(&side_header);
    side_view.set_content(Some(&gtk::ScrolledWindow::builder().child(&nav).build()));
    let sidebar = adw::NavigationPage::builder().title("ADB Manager").child(&side_view).build();

    // ---- content header: targets pill + fleet actions
    let targets = gtk::Label::new(Some("No targets"));
    targets.add_css_class("targets-pill");
    let title = adw::WindowTitle::new("Devices", "");
    let header = adw::HeaderBar::new();
    header.set_title_widget(Some(&title));
    header.pack_start(&targets);
    let install = gtk::Button::builder().label("Install APK…").action_name("win.install").build();
    install.add_css_class("suggested-action");
    let reboot_menu = gio::Menu::new();
    for (label, mode) in [
        ("System", "system"),
        ("Recovery", "recovery"),
        ("Bootloader", "bootloader"),
        ("Fastboot", "fastboot"),
        ("Sideload", "sideload"),
    ] {
        reboot_menu.append(Some(label), Some(&format!("win.reboot::{mode}")));
    }
    let reboot = gtk::MenuButton::builder()
        .child(&adw::ButtonContent::builder().icon_name("view-refresh-symbolic").label("Reboot").build())
        .menu_model(&reboot_menu)
        .tooltip_text("Reboot targets")
        .build();
    header.pack_end(&install);
    header.pack_end(&reboot);
    let content_view = adw::ToolbarView::new();
    content_view.add_top_bar(&header);
    content_view.set_content(Some(&stack));
    let content = adw::NavigationPage::builder().title("Devices").child(&content_view).build();

    let split = adw::NavigationSplitView::builder()
        .sidebar(&sidebar)
        .content(&content)
        .min_sidebar_width(200.0)
        .max_sidebar_width(240.0)
        .build();
    toasts.set_child(Some(&split));
    window.set_content(Some(&toasts));

    let bp = adw::Breakpoint::new(adw::BreakpointCondition::parse("max-width: 720sp").unwrap());
    bp.add_setter(&split, "collapsed", Some(&true.to_value()));
    window.add_breakpoint(bp);

    // ---- navigation
    {
        let (stack, title, content, split) = (stack.clone(), title.clone(), content.clone(), split.clone());
        nav.connect_row_selected(move |_, row| {
            let Some(row) = row else { return };
            let id = row.widget_name();
            stack.set_visible_child_name(&id);
            let name = SECTIONS.iter().find(|s| s.0 == id.as_str()).map(|s| s.1).unwrap_or("");
            title.set_title(name);
            content.set_title(name);
            split.set_show_content(true);
        });
    }
    {
        let nav = nav.clone();
        *app.navigate.borrow_mut() = Some(Box::new(move |id: &str| {
            let mut child = nav.first_child();
            while let Some(c) = child {
                if c.widget_name() == id {
                    nav.select_row(c.downcast_ref::<gtk::ListBoxRow>());
                }
                child = c.next_sibling();
            }
        }));
    }
    nav.select_row(nav.row_at_index(0).as_ref());

    // ---- targets pill and badges
    reboot.set_sensitive(false);
    {
        let (targets, title, reboot) = (targets.clone(), title.clone(), reboot.clone());
        app.on_targets(move |app| {
            let n = app.targets().len();
            targets.set_label(&if n == 0 { "No targets".into() } else { count(n, "target", "targets") });
            if n == 0 {
                targets.remove_css_class("active");
            } else {
                targets.add_css_class("active");
            }
            title.set_subtitle(&match app.focused() {
                Some(d) if n == 1 => d.display_name,
                _ => String::new(),
            });
            reboot.set_sensitive(n > 0);
        });
    }
    app.on_event(move |_, ev| {
        if let Event::DevicesChanged { devices } = ev {
            device_badge.set_label(&if devices.is_empty() { String::new() } else { devices.len().to_string() });
        }
    });

    // ---- actions
    let add = |name: &str, f: Action| {
        let a = gio::SimpleAction::new(name, None);
        let app = app.clone();
        a.connect_activate(move |_, _| f(&app));
        window.add_action(&a);
    };
    {
        let apps_page = apps_page.clone();
        add(
            "install",
            Box::new(move |app| {
                let p = apps_page.clone();
                app.choose_file("Install APK", Some(("Android packages", "*.apk")), move |app, path| {
                    p.set_apk(app, path);
                    app.go("apps");
                });
            }),
        );
    }
    add("connect", Box::new(connect_dialog));
    add(
        "restart-adb",
        Box::new(|app| {
            app.run_visible(Command::RestartServer);
        }),
    );
    add("preferences", Box::new(pages::settings::show));
    add("show-activity", Box::new(|app| app.go("activity")));
    add(
        "about",
        Box::new(|app| {
            let about = adw::AboutDialog::builder()
                .application_name("ADB Manager")
                .application_icon("phone-symbolic")
                .version(adbm_core::VERSION)
                .developer_name("ADB Manager contributors")
                .license_type(gtk::License::Apache20)
                .website("https://github.com/oss96/ADBManager")
                .comments("Manage one Android device or a whole fleet: install, reboot, shell, files and routines.")
                .build();
            about.present(Some(&app.window));
        }),
    );
    {
        let reboot = gio::SimpleAction::new("reboot", Some(glib::VariantTy::STRING));
        let app = app.clone();
        reboot.connect_activate(move |_, p| {
            let mode = match p.and_then(|v| v.str()) {
                Some("recovery") => RebootMode::Recovery,
                Some("bootloader") => RebootMode::Bootloader,
                Some("fastboot") => RebootMode::Fastboot,
                Some("sideload") => RebootMode::Sideload,
                _ => RebootMode::System,
            };
            let n = app.targets().len();
            let heading = format!("Reboot {} to {}?", count(n, "device", "devices"), mode.label());
            app.confirm(
                &heading,
                "Running apps and transfers on these devices are interrupted.",
                "Reboot",
                move |app| {
                    app.run_visible(Command::Reboot { serials: app.targets(), mode });
                },
            );
        });
        window.add_action(&reboot);
    }
    gapp.set_accels_for_action("win.install", &["<Control>i"]);
    gapp.set_accels_for_action("win.preferences", &["<Control>comma"]);
    gapp.set_accels_for_action("window.close", &["<Control>w"]);
    gapp.set_accels_for_action("win.connect", &["<Control>n"]);

    // ---- drop an APK anywhere
    let drop = gtk::DropTarget::new(gdk::FileList::static_type(), gdk::DragAction::COPY);
    {
        let (app, apps_page) = (app.clone(), apps_page.clone());
        drop.connect_drop(move |_, value, _, _| {
            let Ok(list) = value.get::<gdk::FileList>() else { return false };
            let apk = list
                .files()
                .into_iter()
                .filter_map(|f| f.path())
                .find(|p| p.extension().is_some_and(|e| e.eq_ignore_ascii_case("apk")));
            match apk {
                Some(p) => {
                    apps_page.set_apk(&app, p);
                    app.go("apps");
                    true
                }
                None => {
                    app.toast("Drop an .apk file to install it.");
                    false
                }
            }
        });
    }
    window.add_controller(drop);

    app.start();
    window.present();
}

fn connect_dialog(app: &Rc<App>) {
    let d = adw::AlertDialog::new(
        Some("Connect over network"),
        Some("Enable wireless or TCP debugging on the device, then enter its address."),
    );
    let entry = gtk::Entry::builder().placeholder_text("192.168.1.42:5555").activates_default(true).build();
    entry.add_css_class("monospace");
    d.set_extra_child(Some(&entry));
    d.add_responses(&[("cancel", "Cancel"), ("connect", "Connect")]);
    d.set_response_appearance("connect", adw::ResponseAppearance::Suggested);
    d.set_default_response(Some("connect"));
    d.set_close_response("cancel");
    let app2 = app.clone();
    d.connect_response(None, move |_, r| {
        if r == "connect" {
            app2.run(Command::Connect { address: entry.text().to_string() }, |app, _, s, _| app.toast(s));
        }
    });
    d.present(Some(&app.window));
}

mod style {
    use gtk::gdk;

    /// Colours from design/tokens.json.
    const LIGHT: &str = "
        @define-color accent_bg_color #0F7B6C; @define-color accent_fg_color #FFFFFF; @define-color accent_color #0F7B6C;
        @define-color s_online_fg #1A7336; @define-color s_online_bg #E2F3E6;
        @define-color s_attn_fg #8A5A00;   @define-color s_attn_bg #FBF0D9;
        @define-color s_muted_fg #5B6765;  @define-color s_muted_bg #ECEFEE;
        @define-color s_rec_fg #7443C9;    @define-color s_rec_bg #EFE8FB;
        @define-color s_fb_fg #1F5FBF;     @define-color s_fb_bg #E4EDFB;
        @define-color s_err_fg #B42318;    @define-color s_err_bg #FCE8E6;
        @define-color accent_subtle #E3F4F0;";
    const DARK: &str = "
        @define-color accent_bg_color #3DD6B5; @define-color accent_fg_color #062B25; @define-color accent_color #3DD6B5;
        @define-color s_online_fg #6BD68B; @define-color s_online_bg #173222;
        @define-color s_attn_fg #F2C14E;   @define-color s_attn_bg #3A2E12;
        @define-color s_muted_fg #A3B0AE;  @define-color s_muted_bg #262E2D;
        @define-color s_rec_fg #C4A6FF;    @define-color s_rec_bg #2C2342;
        @define-color s_fb_fg #8DB8FF;     @define-color s_fb_bg #1B2A44;
        @define-color s_err_fg #FF8A80;    @define-color s_err_bg #3D1D1B;
        @define-color accent_subtle #123A33;";
    const RULES: &str = "
        .chip { border-radius: 999px; padding: 1px 8px 1px 6px; font-weight: 600; font-size: 0.85em; }
        .chip.online { color: @s_online_fg; background: @s_online_bg; }
        .chip.attention { color: @s_attn_fg; background: @s_attn_bg; }
        .chip.muted { color: @s_muted_fg; background: @s_muted_bg; }
        .chip.recovery { color: @s_rec_fg; background: @s_rec_bg; }
        .chip.fastboot { color: @s_fb_fg; background: @s_fb_bg; }
        .chip.error { color: @s_err_fg; background: @s_err_bg; }
        .targets-pill { border-radius: 999px; padding: 2px 10px; font-weight: 600; font-size: 0.9em;
                        color: alpha(currentColor, 0.6); background: alpha(currentColor, 0.08); }
        .targets-pill.active { color: @accent_color; background: @accent_subtle; }
        .badge { font-feature-settings: 'tnum'; font-size: 0.85em; opacity: 0.6; }
        columnview.fleet > listview > row { min-height: 44px; }
        columnview.fleet > listview > row:selected { background: alpha(@accent_bg_color, 0.16); }
        .inspector { border-left: 1px solid alpha(currentColor, 0.12); }
        .step-number { font-feature-settings: 'tnum'; font-weight: 700; min-width: 20px; opacity: 0.55; }
        .monospace-title .title, .monospace-subtitle .subtitle { font-family: monospace; }
        .shell-output { background: transparent; }";

    pub fn install() {
        let Some(display) = gdk::Display::default() else { return };
        let rules = gtk::CssProvider::new();
        let colors = gtk::CssProvider::new();
        rules.load_from_string(RULES);
        gtk::style_context_add_provider_for_display(&display, &colors, gtk::STYLE_PROVIDER_PRIORITY_APPLICATION);
        gtk::style_context_add_provider_for_display(&display, &rules, gtk::STYLE_PROVIDER_PRIORITY_APPLICATION + 1);
        let sm = adw::StyleManager::default();
        let apply = move |sm: &adw::StyleManager| colors.load_from_string(if sm.is_dark() { DARK } else { LIGHT });
        apply(&sm);
        sm.connect_dark_notify(apply);
    }
}
