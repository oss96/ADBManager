# ADB Manager — Rewrite Plan (v2, reviewed)

This is the corrected plan after an independent review of v1. The "Review
changes" section at the bottom records what changed and why.

## 1. Goal

Replace the Windows-only WinForms / .NET Framework 4 app with a desktop
companion for Android devices that runs on **macOS, Windows and Linux**:

- one shared, fast core written in **Rust**
- a **native UI per platform**: SwiftUI (macOS), WinUI 3 (Windows),
  GTK 4 + libadwaita (Linux)
- one design language, adapted to each platform's idioms (see `design/`)

## 2. Scope for v1

Feature parity with the old app, plus the gaps it had.

| Area | v1 |
|---|---|
| Devices | Live list (ADB track + fastboot polling), merged by serial so a device that reboots stays one row. States: device, unauthorized, offline, recovery, sideload, bootloader, fastbootd. Multi-select "targets". |
| Device info | Model, product, serial, transport, Android version, SDK, battery level. IMEI is lazy, best-effort and off the hot path. |
| Apps | Install APK to N devices (reads package, version from the manifest). List packages (user / all) and filter. Uninstall on N devices. |
| Power | Reboot to system / recovery / bootloader / fastboot(d) / sideload. |
| Shell | Run one command on N devices, streaming output per device, cancellable. |
| Files | Browse one device (sync LIST), pull, push file, mkdir, delete. |
| Fastboot | List, reboot, `boot <img>`, getvar. `flash` is out of scope for v1. |
| Routines | Linear list of steps saved as JSON. Steps: install, uninstall, shell, reboot, push, pull, wait-for (adb/fastboot/recovery, timeout), fastboot-boot. Built-in template: the old app's "Boot TWRP and push folder" flow. |
| Network | `adb connect host:port` / disconnect. |
| Activity | Every operation is a job with per-device status, progress and log. Cancel. |
| Settings | adb / fastboot path (auto-detect PATH, `ANDROID_HOME`, platform default), concurrency limit. |

Out of scope for v1: native USB fastboot, `flash`, split-APK install, resource
(arsc) label resolution, design-token code generation, auto-update.

## 3. Architecture

```
┌──────────── apps/macos (SwiftUI) ──┐ ┌── apps/windows (WinUI 3, C#) ─┐ ┌─ apps/linux (GTK4/Adw, Rust) ─┐
│ AdbmCore.swift  (thin wrapper)     │ │ AdbmCore.cs  (LibraryImport)  │ │ uses adbm-core directly       │
└──────────────┬─────────────────────┘ └──────────────┬────────────────┘ └──────────────┬────────────────┘
               │  C ABI, JSON in / JSON out           │                                 │ Rust API
               ▼                                      ▼                                 ▼
        ┌─────────────────────── crates/adbm-ffi (cdylib + staticlib, adbm.h) ────┐     │
        └──────────────────────────────────┬──────────────────────────────────────┘     │
                                           ▼                                            ▼
        ┌────────────────────────────── crates/adbm-core ─────────────────────────────────────┐
        │ api (Command / Response / Event, serde)   engine (tokio, jobs, cancel, semaphores)  │
        │ adb::wire (host protocol :5037)  adb::sync (LIST/STAT/SEND/RECV)  adb::track        │
        │ fastboot (spawned binary, polling)  apk (zip + binary AXML manifest)  routines      │
        │ registry (merge adb + fastboot by serial)  tools (locate/start adb)                 │
        └─────────────────────────────────────────────────────────────────────────────────────┘
```

### Core decisions

- **ADB:** speak the ADB host protocol over TCP 127.0.0.1:5037 natively
  (`host:version`, `host:track-devices-l`, `host:transport:<serial>`,
  `host:features`, `shell:` / `shell,v2:`, `sync:`, `reboot:`). The adb
  binary is spawned only for server lifecycle (`start-server`) and
  `connect` / `disconnect`.
- **Install:** push the APK with sync to `/data/local/tmp/` and run
  `pm install -r`. This works on every Android version, and it lets the
  same push be reused across devices. It avoids `cmd package`, which needs
  Android 7 or later.
- **Fastboot:** spawn the `fastboot` binary. Poll `fastboot devices` every
  1.5 s. Parse `getvar` from stderr.
- **APK:** use the `zip` crate and a small binary-AXML reader in the core,
  for package, versionCode, versionName, minSdk and the raw label. If the
  label is a resource reference, the file name is used instead.
- **Concurrency:** a tokio multi-thread runtime owned by the core. Every
  command that touches devices becomes a **job** with a `job_id`. A job
  fans out per device, bounded by a global semaphore. It has a
  `CancellationToken`.
- **Events, not callbacks:** the core pushes `Event`s into one queue. The
  UIs pull from it with `next_event(timeout)` on a background thread or
  task, then hop to their main thread. There are no foreign callbacks
  across the FFI.
- **Logging:** the `tracing` crate. Log lines are also sent to the UI as
  `Event::Log`.

### FFI contract (the same for Swift and C#)

```c
AdbmCore *adbm_core_new(const char *config_json);        // NULL on failure
char     *adbm_core_call(AdbmCore *, const char *cmd_json); // Response JSON, caller frees
char     *adbm_core_next_event(AdbmCore *, uint32_t timeout_ms); // NULL on timeout
void      adbm_core_free(AdbmCore *);                     // stops runtime
void      adbm_string_free(char *);
const char *adbm_version(void);                           // static, do not free
```

The JSON schemas are the serde types in `adbm-core::api`. A Rust contract
test drives the FFI exactly the way the apps do.

### UI rules

- The apps are thin. Formatting, state machines and validation live in the
  core. Each UI keeps a store (device map, job map) that is updated by
  events, and renders it.
- All three apps have the same information architecture: Devices, Apps,
  Files, Shell, Routines, Activity, and Settings.

## 4. Repository layout

```
Cargo.toml                 workspace
crates/adbm-core/          core library + tests (fake adb server)
crates/adbm-ffi/           C ABI, include/adbm.h
apps/linux/                GTK4 + libadwaita app (Rust)
apps/macos/                Swift package: CAdbm module + AdbManager app
apps/windows/              WinUI 3 unpackaged app (C#, .NET 8)
design/                    design language, tokens.json, icon map, mockup
docs/                      this plan, architecture notes
.github/workflows/ci.yml   core + linux + macOS + windows builds
```

## 5. Execution order

1. Remove the old WinForms app. It stays in git history. The TWRP flow is
   recorded in §7.
2. Design: design language, tokens, icon mapping, and a mockup of the main
   screen.
3. `adbm-core`:
   - wire, sync and track modules, tested against an in-process fake adb
     server
   - APK reader with tests
   - fastboot parser with tests
   - engine and jobs
   - routines
4. `adbm-ffi` plus a contract test and the C header.
5. CI for all three platforms: core tests, Linux app, a Swift build that
   links the static lib, and a dotnet build that bundles the dll.
6. Linux app. It is built and run here under Xvfb, with a screenshot.
7. macOS app (SwiftPM) and Windows app (WinUI 3). They are written here and
   compiled by CI. The README labels them "CI-built, not run-tested" until
   someone runs them on real hardware.

## 6. Verification

- `cargo fmt --check`, `cargo clippy -D warnings`, `cargo test` for the
  workspace.
- An integration smoke test against the real local adb server:
  `host:version` and the device list, with no devices attached.
- The Linux app launches under Xvfb and a screenshot is attached.
- The macOS and Windows apps are checked only by CI compilation. That limit
  is stated in the README.

## 7. The old "Boot TWRP & push" flow (preserved as a routine template)

1. Reboot each target to the bootloader.
2. Wait until every target shows up in fastboot.
3. `fastboot boot recovery.img`.
4. Wait until every target shows up in adb in recovery state.
5. `mount system`.
6. Push the selected folder.
7. `rm -r /data/dalvik-cache`, `rm -r /sdcard/TWRP`, `umount system`.
8. Reboot.

## 8. Review changes (v1 → v2)

- **UniFFI + uniffi-bindgen-cs dropped.** It was the biggest risk: the
  third-party C# generator lags upstream and pins the uniffi version, and
  none of it can be exercised in this environment. The replacement is one
  small C ABI with JSON messages, shared by Swift and C#.
- **Callback interfaces dropped** in favour of a polled event queue and job
  IDs. This avoids reentrancy and "callback after dispose" bugs in code that
  can't be run here.
- **Install:** `exec:cmd package install` replaced by push + `pm install`,
  which works on older devices.
- **shell v2** is detected through `host:features`, with a fallback to
  `shell:`.
- **Features added:**
  - adb-not-found and unauthorized states
  - `adb connect`
  - cancellation
  - a concurrency limit
  - `tracing`
  - a settings screen
- **Routines** gained `wait-for` steps, without which the old TWRP flow
  can't be expressed.
- **Features cut from v1:**
  - native USB fastboot
  - fastboot `flash`
  - arsc label resolution
  - token codegen
  - more than one HTML mockup
- **Order changed:** FFI and CI now come before the UIs. The old code is
  removed only after its one non-obvious flow has been written down.
- **Stack pinned:** libadwaita is pinned to the `v1_5` feature and gtk4 to
  `v4_14`. No AdwSpinner, AdwToggleGroup or AdwWrapBox.
- **Licensing:** v1 does not bundle adb or fastboot. The user installs
  Android platform-tools, and the app auto-detects them. This avoids
  redistribution questions about platform-tools.
