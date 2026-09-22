# ADB Manager — Design Language

## Principle: one product, three natives

ADB Manager is a **fleet console**: you plug in one device or twenty and act
on all of them at once. All platforms share the same information
architecture, vocabulary, state colours and behaviour. Chrome, typography,
controls and icons belong to the host platform. A macOS user sees a Mac app, a
Windows user a Fluent app, and a GNOME user an Adwaita app.

| Shared (defined here) | Native (defined by the platform) |
|---|---|
| Sections, flows, copy | Window chrome, title bar, toolbar |
| Device-state colours, accent | Font (SF Pro / Segoe UI Variable / Cantarell or Adwaita Sans) |
| Spacing rhythm (4 pt grid), density | Controls, focus rings, dialogs |
| Icon *meaning* (see `icons.md`) | Icon *glyphs* (SF Symbols / Segoe Fluent Icons / Adwaita symbolic) |
| Keyboard shortcuts (⌘ ↔ Ctrl) | Materials (vibrancy / Mica / flat) |

## Core concept: Targets

Every action applies to the **targets**, which are the devices currently
selected in the device list. The target count is always visible in the toolbar
("3 targets"). If nothing is selected, actions are disabled and the empty hint
says "Select one or more devices". Selection survives navigation between
sections. The old app had separate ADB and fastboot tables with "All / None"
buttons. Here there is one list, and the whole list can be selected with
⌘A / Ctrl+A.

A device that reboots from system to bootloader to recovery **stays the same
row**. Only its state chip changes. Rows are merged by serial across ADB and
fastboot.

## Information architecture

```
Sidebar
├── Devices     fleet table, state chips, inspector for the focused device
├── Apps        drop APKs → install on targets · packages of focused device → uninstall
├── Files       browser for the focused device · push / pull / new folder
├── Shell       command field · run on targets · per-device output panes
├── Routines    saved step lists · editor · run on targets
├── Activity    jobs (running / finished) · per-device results · log
└── Settings    tools (adb/fastboot paths), concurrency, appearance
```

Actions that are always available from the toolbar: **Install APK…**,
**Reboot ▾** (System, Recovery, Bootloader, Fastboot, Sideload), **Connect…**
(adb over TCP), and the **Activity** indicator.

## Device states

State is always shown with **a chip: colour + label + glyph**. Colour is never
the only signal.

| State | Label | Token | Meaning / hint |
|---|---|---|---|
| device | Online | `state.online` | Ready |
| unauthorized | Unauthorized | `state.attention` | "Confirm the USB debugging prompt on the device" |
| offline | Offline | `state.muted` | "Reconnect the cable or restart adb" |
| recovery | Recovery | `state.recovery` | adb in recovery |
| sideload | Sideload | `state.recovery` | |
| bootloader | Bootloader | `state.fastboot` | fastboot, bootloader |
| fastbootd | Fastbootd | `state.fastboot` | fastboot, userspace |
| connecting | Connecting… | `state.muted` | transient |

## Layout and density

- 4 pt grid; spacing tokens `xs 4 · s 8 · m 12 · l 16 · xl 24 · xxl 32`.
- Device table row height: 32 (macOS) / 40 (Windows) / 44 (Linux, list rows).
  Each platform uses its own default density.
- Serials, package names, paths and shell output use a **monospace** face
  (SF Mono / Cascadia Mono / Adwaita Mono or Monospace), always with tabular
  numbers.
- Window minimum size is 900 × 560. The inspector can be hidden: the
  toolbar toggle on macOS, and an `AdwOverlaySplitView` on Linux, whose
  sidebar also collapses below 720 sp. *(Planned: automatic collapse below
  1100 px on every platform.)*

## Behaviour

- **Everything is a job.** Starting an action never blocks the UI. A toast
  (Linux), InfoBar (Windows) or banner (macOS) reports it: "Installing
  app.apk on 3 devices". It links to Activity. Failure on one device doesn't
  stop the others. The job ends as "2 succeeded, 1 failed", and the failure
  is explained on the device's row.
- **Destructive actions** (uninstall, delete file, reboot all) ask for
  confirmation with a native alert. The alert names the count and the
  target: "Uninstall com.example.app from 5 devices?" The confirm button
  repeats the verb.
- **Drag & drop:** dropping an APK anywhere opens it in Apps, ready to
  install on the targets. *(Planned: dropping files on Files pushes them to
  the current folder.)*
- **Cancellation:** every running job has a Cancel button in Activity.
- **Empty states** give the one next step: "Connect a device with USB
  debugging enabled, or connect over the network." *(Planned: a link to "How
  to enable USB debugging".)*
- **adb missing:** a full-window state with a "Choose adb…" button.

## Copy

- Use "device", not "phone". Use "Install", not "Deploy". "Reboot to
  Recovery" is written out in full.
- Counts are exact and pluralised: "1 device", "3 devices".
- Error messages give the cause, then the fix: "Install failed on Pixel 7:
  INSTALL_FAILED_VERSION_DOWNGRADE. Uninstall the newer version first."

## Colour

The accent is **Bridge teal**. It sits between Android green and the blue used
by the platforms, and is distinctive without being a brand colour.

- **macOS**: Bridge teal is the tint while the user keeps the default
  (multicolour) accent. Any accent the user has chosen wins.
- **Windows**: the system accent is always used for controls, because Windows
  has no "multicolour" default to detect. Bridge teal appears only in the
  targets pill.
- **Linux**: libadwaita 1.5 has no system accent, so Bridge teal is applied
  through `@define-color accent_bg_color`.

The values are in `tokens.json`. Each colour has light and dark values, and
every text/background pair meets WCAG AA (4.5:1).

## Platform mapping

| Element | macOS (SwiftUI) | Windows (WinUI 3) | Linux (GTK4 + Adw 1.5) |
|---|---|---|---|
| Shell | `NavigationSplitView` + `.inspector` | `NavigationView` (Left) + Mica | `AdwNavigationSplitView` + `AdwOverlaySplitView` |
| Device list | `Table` with multi-select | `ListView` of grid rows, Extended selection | `GtkColumnView` + `GtkMultiSelection` |
| Toolbar | `.toolbar` items | `CommandBar` | `AdwHeaderBar` in `AdwToolbarView` |
| Reboot menu | `Menu` | `MenuFlyout` on an `AppBarButton` | `GtkMenuButton` + `GMenu` |
| Notifications | overlay banner | `InfoBar` | `AdwToast` |
| Confirm | `.confirmationDialog` | `ContentDialog` | `AdwAlertDialog` |
| Preferences | `Settings` scene | Settings page in nav footer | `AdwPreferencesDialog` |

On Windows the UI is built in C# (no XAML markup), from the same WinUI 3
controls. That way the whole app type-checks on any OS.
