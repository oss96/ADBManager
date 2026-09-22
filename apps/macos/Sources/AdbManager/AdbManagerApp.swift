// ADB Manager for macOS (SwiftUI). Design: design/DESIGN.md.

import AppKit
import SwiftUI

@main
struct AdbManagerApp: App {
    @StateObject private var model = AppModel()

    init() {
        // Launched from `swift run` there is no bundle; make it a regular app.
        NSApplication.shared.setActivationPolicy(.regular)
    }

    var body: some Scene {
        WindowGroup("ADB Manager") {
            ContentView()
                .environmentObject(model)
                .frame(minWidth: 900, minHeight: 560)
                .tint(Tokens.useBridgeAccent ? Tokens.accent : .accentColor)
                .onAppear { NSApplication.shared.activate(ignoringOtherApps: true) }
        }
        .commands {
            CommandGroup(after: .newItem) {
                Button("Install APK…") { NotificationCenter.default.post(name: .installApk, object: nil) }
                    .keyboardShortcut("i")
            }
        }
        Settings {
            SettingsView().environmentObject(model).frame(width: 520)
        }
    }
}

extension Notification.Name {
    static let installApk = Notification.Name("installApk")
}

/// Colours from design/tokens.json, resolved for light and dark.
enum Tokens {
    static func dynamic(_ light: UInt32, _ dark: UInt32) -> Color {
        Color(nsColor: NSColor(name: nil) { appearance in
            let isDark = appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
            return NSColor(hex: isDark ? dark : light)
        })
    }

    static let accent = dynamic(0x0F7B6C, 0x3DD6B5)
    static let accentSubtle = dynamic(0xE3F4F0, 0x123A33)

    /// Bridge teal is used only while the user keeps the default
    /// (multicolour) system accent; otherwise their accent wins.
    static var useBridgeAccent: Bool { UserDefaults.standard.object(forKey: "AppleAccentColor") == nil }

    static func chip(for state: String) -> (fg: Color, bg: Color, symbol: String) {
        switch state {
        case "online": return (dynamic(0x1A7336, 0x6BD68B), dynamic(0xE2F3E6, 0x173222), "checkmark.circle.fill")
        case "unauthorized", "no_permissions", "authorizing":
            return (dynamic(0x8A5A00, 0xF2C14E), dynamic(0xFBF0D9, 0x3A2E12), "lock.fill")
        case "recovery", "rescue", "sideload":
            return (dynamic(0x7443C9, 0xC4A6FF), dynamic(0xEFE8FB, 0x2C2342), "cross.case.fill")
        case "bootloader", "fastbootd": return (dynamic(0x1F5FBF, 0x8DB8FF), dynamic(0xE4EDFB, 0x1B2A44), "bolt.fill")
        case "error": return (dynamic(0xB42318, 0xFF8A80), dynamic(0xFCE8E6, 0x3D1D1B), "xmark.octagon.fill")
        default: return (dynamic(0x5B6765, 0xA3B0AE), dynamic(0xECEFEE, 0x262E2D), "bolt.horizontal.circle")
        }
    }

    static func jobChip(_ status: String) -> (String, String) {
        switch status {
        case "succeeded": return ("Done", "online")
        case "partially_failed": return ("Partly failed", "unauthorized")
        case "failed": return ("Failed", "error")
        case "cancelled": return ("Cancelled", "offline")
        default: return ("Running", "bootloader")
        }
    }
}

extension NSColor {
    convenience init(hex: UInt32) {
        self.init(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255,
                  blue: CGFloat(hex & 0xFF) / 255, alpha: 1)
    }
}

struct Chip: View {
    let label: String
    let state: String
    var body: some View {
        let c = Tokens.chip(for: state)
        Label(label, systemImage: c.symbol)
            .labelStyle(.titleAndIcon)
            .font(.caption.weight(.semibold))
            .foregroundStyle(c.fg)
            .padding(.horizontal, 7)
            .padding(.vertical, 2)
            .background(c.bg, in: Capsule())
    }
}

// MARK: - Window

struct ContentView: View {
    @EnvironmentObject var model: AppModel
    @State private var showInspector = true
    @State private var showConnect = false
    @State private var confirmReboot: String?

    var body: some View {
        NavigationSplitView {
            List(AppSection.allCases, selection: Binding<AppSection?>(get: { model.section }, set: { if let s = $0 { model.section = s } })) { s in
                Label(s.title, systemImage: s.symbol)
                    .badge(badge(for: s))
                    .tag(s)
            }
            .navigationSplitViewColumnWidth(min: 180, ideal: 200)
        } detail: {
            detail
                .navigationTitle(model.section.title)
                .navigationSubtitle(model.orderedTargets.count == 1 ? (model.focused?.displayName ?? "") : "")
                .inspector(isPresented: $showInspector) {
                    InspectorView().inspectorColumnWidth(min: 240, ideal: 280, max: 340)
                }
        }
        .toolbar {
            ToolbarItem(placement: .navigation) {
                let n = model.orderedTargets.count
                Text(n == 0 ? "No targets" : count(n, "target", "targets"))
                    .font(.callout.weight(.semibold))
                    .foregroundStyle(n == 0 ? Color.secondary : Tokens.accent)
                    .padding(.horizontal, 10).padding(.vertical, 3)
                    .background(n == 0 ? Color.secondary.opacity(0.12) : Tokens.accentSubtle, in: Capsule())
            }
            ToolbarItemGroup(placement: .primaryAction) {
                Button { showConnect = true } label: { Label("Connect…", systemImage: "network") }
                Menu {
                    ForEach(["system", "recovery", "bootloader", "fastboot", "sideload"], id: \.self) { m in
                        Button(m.capitalized) { confirmReboot = m }
                    }
                } label: { Label("Reboot", systemImage: "arrow.clockwise") }
                .disabled(model.targets.isEmpty)
                Button { installApk() } label: { Label("Install APK…", systemImage: "square.and.arrow.down") }
                Button { showInspector.toggle() } label: { Label("Inspector", systemImage: "sidebar.right") }
            }
        }
        .overlay(alignment: .bottom) {
            if let b = model.banner {
                Text(b)
                    .padding(.horizontal, 14).padding(.vertical, 9)
                    .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 10))
                    .overlay(RoundedRectangle(cornerRadius: 10).strokeBorder(.separator))
                    .padding(.bottom, 16)
                    .transition(.move(edge: .bottom).combined(with: .opacity))
                    .onTapGesture { model.section = .activity }
            }
        }
        .animation(.snappy, value: model.banner)
        .sheet(isPresented: $showConnect) { ConnectSheet() }
        .confirmationDialog(rebootTitle, isPresented: Binding(get: { confirmReboot != nil }, set: { if !$0 { confirmReboot = nil } })) {
            Button("Reboot", role: .destructive) {
                if let m = confirmReboot { model.runVisible(["type": "reboot", "serials": model.orderedTargets, "mode": m]) }
            }
        } message: {
            Text("Running apps and transfers on these devices are interrupted.")
        }
        .onReceive(NotificationCenter.default.publisher(for: .installApk)) { _ in installApk() }
        .dropDestination(for: URL.self) { urls, _ in
            guard let apk = urls.first(where: { $0.pathExtension.lowercased() == "apk" }) else { return false }
            AppsState.shared.apk = model.inspectApk(apk)
            model.section = .apps
            return true
        }
        .alert("ADB Manager could not start", isPresented: .constant(model.startError != nil)) {
            Button("Quit") { NSApplication.shared.terminate(nil) }
        } message: { Text(model.startError ?? "") }
    }

    private var rebootTitle: String {
        "Reboot \(count(model.orderedTargets.count, "device", "devices")) to \((confirmReboot ?? "").capitalized)?"
    }

    private func badge(for s: AppSection) -> Int {
        switch s {
        case .devices: return model.devices.count
        case .activity: return model.runningJobs
        default: return 0
        }
    }

    private func installApk() {
        guard let url = model.chooseFile(types: [.apk], message: "Choose an APK to install") else { return }
        AppsState.shared.apk = model.inspectApk(url)
        model.section = .apps
    }

    @ViewBuilder private var detail: some View {
        switch model.section {
        case .devices: DevicesView()
        case .apps: AppsView()
        case .files: FilesView()
        case .shell: ShellView()
        case .routines: RoutinesView()
        case .activity: ActivityView()
        }
    }
}

struct ConnectSheet: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) private var dismiss
    @State private var address = ""
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Connect over network").font(.headline)
            Text("Enable wireless or TCP debugging on the device, then enter its address.")
                .foregroundStyle(.secondary)
            TextField("192.168.1.42:5555", text: $address)
                .font(.body.monospaced())
                .onSubmit(connect)
            HStack {
                Spacer()
                Button("Cancel", role: .cancel) { dismiss() }.keyboardShortcut(.cancelAction)
                Button("Connect", action: connect).keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(width: 380)
    }

    private func connect() {
        model.run(["type": "connect", "address": address]) { _, summary, _ in model.show(summary) }
        dismiss()
    }
}

// MARK: - Devices

struct DevicesView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        Group {
            if model.devices.isEmpty {
                emptyState
            } else {
                VStack(spacing: 0) {
                    Table(model.devices, selection: $model.targets) {
                        TableColumn("Device") { d in Text(d.displayName).fontWeight(.semibold) }
                            .width(min: 160, ideal: 220)
                        TableColumn("Serial") { d in
                            Text(d.serial).font(.callout.monospaced()).foregroundStyle(.secondary)
                        }
                        TableColumn("State") { d in Chip(label: d.stateLabel, state: d.state) }
                            .width(min: 110, ideal: 120)
                        TableColumn("Android") { d in Text(d.androidText).monospacedDigit() }
                            .width(min: 90, ideal: 110)
                        TableColumn("Battery") { d in Text(d.batteryText).monospacedDigit() }
                            .width(min: 60, ideal: 70)
                    }
                    let hints = model.devices.compactMap { d in d.stateHint.map { "\(d.displayName): \($0)" } }
                    if !hints.isEmpty {
                        Label(hints.joined(separator: "\n"), systemImage: "exclamationmark.triangle")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(10)
                    }
                }
            }
        }
    }

    @ViewBuilder private var emptyState: some View {
        switch model.server?.state {
        case "adb_not_found":
            ContentUnavailableView {
                Label("adb was not found", systemImage: "exclamationmark.triangle")
            } description: {
                Text("Install Android platform-tools (for example with Homebrew: brew install android-platform-tools), or choose the adb program.")
            } actions: {
                Button("Choose adb…") {
                    if let url = model.chooseFile(message: "Choose the adb program") {
                        var c = model.config
                        c.adbPath = url.path
                        model.saveConfig(c)
                    }
                }
                .buttonStyle(.borderedProminent)
            }
        case "error":
            ContentUnavailableView {
                Label("adb is not responding", systemImage: "network.slash")
            } description: {
                Text(model.server?.message ?? "")
            } actions: {
                Button("Restart adb") { model.runVisible(["type": "restart_server"]) }
            }
        case "running":
            ContentUnavailableView {
                Label("No devices", systemImage: "iphone.gen3.slash")
            } description: {
                Text("Connect a device with USB debugging enabled, or connect over the network.")
            }
        default:
            ProgressView("Starting adb…")
        }
    }
}

struct InspectorView: View {
    @EnvironmentObject var model: AppModel
    @State private var imei: String?

    var body: some View {
        if let d = model.focused {
            Form {
                Section {
                    VStack(alignment: .leading, spacing: 2) {
                        let more = model.orderedTargets.count - 1
                        Text(d.displayName + (more > 0 ? "  +\(more) more" : "")).font(.title3.weight(.semibold))
                        Text([d.device ?? d.product, d.serial].compactMap { $0 }.joined(separator: " · "))
                            .font(.caption.monospaced()).foregroundStyle(.secondary).textSelection(.enabled)
                    }
                }
                Section {
                    LabeledContent("State") { Chip(label: d.stateLabel, state: d.state) }
                    LabeledContent("Android", value: d.androidLong)
                    LabeledContent("Build", value: d.info.buildId ?? "—")
                    LabeledContent("Battery", value: d.batteryLong)
                    LabeledContent("Connection", value: d.link == "tcp" ? "Network" : d.link == "emulator" ? "Emulator" : "USB")
                    LabeledContent("IMEI") {
                        if let imei { Text(imei).textSelection(.enabled) } else {
                            Button("Read") {
                                model.run(["type": "read_imei", "serial": d.serial]) { _, summary, _ in imei = summary }
                            }
                            .disabled(!d.isAdbUsable)
                        }
                    }
                }
                Section("Quick actions") {
                    Button { model.section = .shell } label: { Label("Shell", systemImage: "terminal") }
                    Button { model.section = .files } label: { Label("Files", systemImage: "folder") }
                    Button { model.runVisible(["type": "reboot", "serials": model.orderedTargets, "mode": "recovery"]) } label: {
                        Label("Reboot to Recovery", systemImage: "cross.case")
                    }
                    Button { model.runVisible(["type": "reboot", "serials": model.orderedTargets, "mode": "bootloader"]) } label: {
                        Label("Reboot to Bootloader", systemImage: "bolt")
                    }
                }
            }
            .formStyle(.grouped)
            .onChange(of: d.serial) { imei = nil }
        } else {
            ContentUnavailableView("No device selected", systemImage: "iphone.gen3",
                                   description: Text("Select one or more devices. Actions apply to every selected device."))
        }
    }
}

// MARK: - Apps

@MainActor
final class AppsState: ObservableObject {
    static let shared = AppsState()
    @Published var apk: ApkInfo?
}

struct AppsView: View {
    @EnvironmentObject var model: AppModel
    @ObservedObject private var state = AppsState.shared
    @State private var downgrade = false
    @State private var packages: [Package] = []
    @State private var system = false
    @State private var query = ""
    @State private var uninstall: String?

    var body: some View {
        Form {
            Section("Install") {
                LabeledContent(state.apk.map { $0.label ?? $0.fileName } ?? "No APK chosen") {
                    Button("Choose…") {
                        if let url = model.chooseFile(types: [.apk], message: "Choose an APK") { state.apk = model.inspectApk(url) }
                    }
                }
                if let apk = state.apk {
                    Text("\(apk.package)\(apk.versionName.map { " \($0)" } ?? "") · \(humanSize(apk.size))")
                        .font(.callout.monospaced()).foregroundStyle(.secondary)
                }
                Toggle("Allow downgrade", isOn: $downgrade)
                let n = model.orderedTargets.count
                Button(n == 0 ? "Install" : "Install on \(count(n, "device", "devices"))") {
                    guard let apk = state.apk else { return }
                    model.runVisible(["type": "install", "serials": model.orderedTargets, "path": apk.path, "allow_downgrade": downgrade])
                    model.section = .activity
                }
                .buttonStyle(.borderedProminent)
                .disabled(n == 0 || state.apk == nil)
            }
            Section {
                Toggle("Show system apps", isOn: $system)
                ForEach(packages.filter { query.isEmpty || $0.name.localizedCaseInsensitiveContains(query) }.prefix(500)) { p in
                    HStack {
                        Text(p.name).font(.body.monospaced())
                        if p.system { Text("System").font(.caption).foregroundStyle(.secondary) }
                        Spacer()
                        Button(role: .destructive) { uninstall = p.name } label: { Image(systemName: "trash") }
                            .buttonStyle(.borderless)
                            .help("Uninstall from targets")
                    }
                }
            } header: {
                Text(model.focused.map { "Installed apps on \($0.displayName)" } ?? "Select a device to see its apps")
            }
        }
        .formStyle(.grouped)
        .searchable(text: $query, prompt: "Filter packages")
        .task(id: "\(model.focused?.serial ?? "")|\(system)") { reload() }
        .confirmationDialog("Uninstall \(uninstall ?? "") from \(count(model.orderedTargets.count, "device", "devices"))?",
                            isPresented: Binding(get: { uninstall != nil }, set: { if !$0 { uninstall = nil } })) {
            Button("Uninstall", role: .destructive) {
                guard let name = uninstall else { return }
                model.run(["type": "uninstall", "serials": model.orderedTargets, "package": name]) { _, summary, _ in
                    model.show(summary)
                    reload()
                }
            }
        } message: { Text("The app and its data are removed.") }
    }

    private func reload() {
        guard let d = model.focused, d.isAdbUsable else { packages = []; return }
        model.run(["type": "list_packages", "serial": d.serial, "include_system": system]) { status, summary, data in
            if status != "succeeded" { model.show(summary) }
            packages = decodeData([Package].self, data) ?? []
        }
    }
}

// MARK: - Files

struct FilesView: View {
    @EnvironmentObject var model: AppModel
    @State private var path = "/sdcard"
    @State private var entries: [DirEntry] = []
    @State private var status = ""
    @State private var selection: DirEntry.ID?
    @State private var delete: String?

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Button { open(parent(of: path)) } label: { Image(systemName: "chevron.up") }.help("Parent folder")
                TextField("Path", text: $path).font(.body.monospaced()).onSubmit { reload() }
                Button { reload() } label: { Image(systemName: "arrow.clockwise") }.help("Reload")
                Button("Push File…") { push(directories: false) }
                Button("Push Folder…") { push(directories: true) }
            }
            .padding(10)
            Table(entries, selection: $selection) {
                TableColumn("Name") { e in
                    Label(e.name, systemImage: e.kind == "dir" ? "folder" : e.kind == "symlink" ? "arrow.turn.up.right" : "doc")
                }
                TableColumn("Size") { e in Text(e.kind == "file" ? humanSize(e.size) : "—").monospacedDigit() }
                    .width(90)
            }
            .contextMenu(forSelectionType: DirEntry.ID.self) { ids in
                if let id = ids.first {
                    Button("Pull to This Mac…") { pull(join(path, id)) }
                    Button("Delete…", role: .destructive) { delete = id }
                }
            } primaryAction: { ids in
                if let id = ids.first, let e = entries.first(where: { $0.id == id }), e.kind != "file" { open(join(path, id)) }
            }
            Text(status).font(.caption).foregroundStyle(.secondary).frame(maxWidth: .infinity, alignment: .leading).padding(8)
        }
        .task(id: model.focused?.serial) { reload() }
        .confirmationDialog("Delete \(delete ?? "")?", isPresented: Binding(get: { delete != nil }, set: { if !$0 { delete = nil } })) {
            Button("Delete", role: .destructive) {
                guard let d = model.focused, let name = delete else { return }
                model.run(["type": "delete", "serial": d.serial, "path": join(path, name)]) { _, s, _ in
                    model.show(s)
                    reload()
                }
            }
        } message: { Text("It is removed from the device. This can't be undone.") }
    }

    private func join(_ base: String, _ name: String) -> String { base.hasSuffix("/") ? base + name : base + "/" + name }
    private func parent(of p: String) -> String {
        let t = p.hasSuffix("/") && p.count > 1 ? String(p.dropLast()) : p
        let up = (t as NSString).deletingLastPathComponent
        return up.isEmpty ? "/" : up
    }
    private func open(_ p: String) { path = p; reload() }

    private func reload() {
        guard let d = model.focused else { entries = []; status = "Select a device to browse its files."; return }
        guard d.isAdbUsable else { entries = []; status = "\(d.displayName) is \(d.stateLabel.lowercased())."; return }
        status = "Loading…"
        model.run(["type": "list_dir", "serial": d.serial, "path": path]) { st, summary, data in
            struct Listing: Decodable { var entries: [DirEntry] }
            entries = st == "succeeded" ? (decodeData(Listing.self, data)?.entries ?? []) : []
            status = summary
        }
    }

    private func push(directories: Bool) {
        guard let url = model.chooseFile(directories: directories, message: "Choose what to push to \(path)") else { return }
        let remote = path.hasSuffix("/") ? path : path + "/"
        model.run(["type": "push", "serials": model.orderedTargets, "local": url.path, "remote": remote]) { _, s, _ in
            model.show(s)
            reload()
        }
    }

    private func pull(_ remote: String) {
        guard let d = model.focused,
              let dir = model.chooseFile(directories: true, message: "Choose where to save \(remote)") else { return }
        model.runVisible(["type": "pull", "serial": d.serial, "remote": remote, "local": dir.path])
    }
}

// MARK: - Shell

struct ShellView: View {
    @EnvironmentObject var model: AppModel
    @State private var command = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                TextField("Command, e.g. getprop ro.build.version.release", text: $command)
                    .font(.body.monospaced())
                    .onSubmit(run)
                Button("Run", action: run).keyboardShortcut(.defaultAction).disabled(model.targets.isEmpty)
                Button { model.shell.removeAll() } label: { Image(systemName: "clear") }.help("Clear output")
            }
            let n = model.orderedTargets.count
            Text(n == 0 ? "Select one or more devices to run commands on." : "Runs on \(count(n, "device", "devices"))")
                .font(.caption).foregroundStyle(.secondary)
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 2) {
                        ForEach(model.shell) { line in
                            Text(line.text)
                                .font(.system(.body, design: .monospaced).weight(line.kind == .stdout || line.kind == .stderr ? .regular : .semibold))
                                .foregroundStyle(line.kind == .stderr ? Tokens.dynamic(0xB42318, 0xFF8A80) : Color.primary)
                                .textSelection(.enabled)
                                .id(line.id)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(10)
                }
                .background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 8))
                .onChange(of: model.shell.count) { if let last = model.shell.last { proxy.scrollTo(last.id) } }
            }
        }
        .padding(12)
    }

    private func run() {
        let c = command.trimmingCharacters(in: .whitespaces)
        guard !c.isEmpty else { return }
        model.runShell(c)
    }
}

// MARK: - Routines

struct RoutinesView: View {
    @EnvironmentObject var model: AppModel
    @State private var templates: [Routine] = []
    @State private var current: Routine?

    var body: some View {
        Form {
            Section {
                ForEach(templates) { t in
                    Button { current = t } label: {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(t.name).fontWeight(.semibold)
                            Text(t.description).font(.caption).foregroundStyle(.secondary)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                }
            } header: {
                Text("Routines")
            } footer: {
                Text("A routine runs its steps in order on every target. Devices run in parallel; a device stops at its first failed step.")
            }
            if let r = current {
                Section {
                    ForEach(Array(r.steps.enumerated()), id: \.offset) { i, step in
                        HStack(alignment: .firstTextBaseline) {
                            Text("\(i + 1)").monospacedDigit().foregroundStyle(.secondary).frame(width: 20)
                            VStack(alignment: .leading) {
                                Text(step.title)
                                if step.type == "shell" { Text(step.command ?? "").font(.caption.monospaced()).foregroundStyle(.secondary) }
                            }
                            Spacer()
                            chooser(i, step)
                        }
                    }
                } header: {
                    HStack {
                        Text(r.name)
                        Spacer()
                        let n = model.orderedTargets.count
                        Button(n == 0 ? "Run" : "Run on \(count(n, "device", "devices"))") { model.runRoutine(r) }
                            .buttonStyle(.borderedProminent)
                            .disabled(n == 0)
                    }
                }
            }
        }
        .formStyle(.grouped)
        .onAppear { if templates.isEmpty { templates = model.templates() } }
    }

    @ViewBuilder private func chooser(_ i: Int, _ step: Step) -> some View {
        switch step.type {
        case "install":
            Button((step.path ?? "").isEmpty ? "Choose APK…" : "Change…") {
                if let u = model.chooseFile(types: [.apk], message: "Choose an APK") { current?.steps[i].path = u.path }
            }
        case "fastboot_boot":
            Button((step.image ?? "").isEmpty ? "Choose Image…" : "Change…") {
                if let u = model.chooseFile(types: [.img], message: "Choose a boot image") { current?.steps[i].image = u.path }
            }
        case "push":
            Button((step.local ?? "").isEmpty ? "Choose Folder…" : "Change…") {
                if let u = model.chooseFile(directories: true, message: "Choose a folder to push") { current?.steps[i].local = u.path }
            }
        default:
            EmptyView()
        }
    }
}

// MARK: - Activity

struct ActivityView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        Form {
            Section("Jobs") {
                if model.jobs.isEmpty { Text("Nothing has run yet").foregroundStyle(.secondary) }
                ForEach(model.jobs) { job in
                    VStack(alignment: .leading, spacing: 6) {
                        HStack {
                            Text(job.info.title).fontWeight(.semibold)
                            Spacer()
                            let chip = Tokens.jobChip(job.info.status)
                            Chip(label: chip.0, state: chip.1)
                            if job.info.status == "running" {
                                Button { _ = model.callSync(["type": "cancel", "job_id": job.id]) } label: {
                                    Image(systemName: "xmark.circle")
                                }
                                .buttonStyle(.borderless)
                                .help("Cancel")
                            }
                        }
                        ProgressView(value: job.fraction)
                        if !job.detail.isEmpty { Text(job.detail).font(.caption).foregroundStyle(.secondary) }
                    }
                    .padding(.vertical, 4)
                }
            }
            Section("Log") {
                Text(model.log.suffix(200).joined(separator: "\n"))
                    .font(.caption.monospaced())
                    .textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .formStyle(.grouped)
    }
}

// MARK: - Settings

struct SettingsView: View {
    @EnvironmentObject var model: AppModel
    @State private var draft = Config()

    var body: some View {
        Form {
            Section {
                TextField("adb", text: Binding(get: { draft.adbPath ?? "" }, set: { draft.adbPath = $0.isEmpty ? nil : $0 }), prompt: Text("Automatic"))
                TextField("fastboot", text: Binding(get: { draft.fastbootPath ?? "" }, set: { draft.fastbootPath = $0.isEmpty ? nil : $0 }), prompt: Text("Automatic"))
            } header: {
                Text("Android platform-tools")
            } footer: {
                if let s = model.server, s.state == "running" {
                    Text("Using \(s.adbPath ?? "adb") (protocol \(s.version ?? 0)).")
                } else {
                    Text("Leave empty to find adb and fastboot automatically.")
                }
            }
            Section("adb server") {
                Toggle("Start the server automatically", isOn: $draft.autoStartServer)
                TextField("Port", value: $draft.adbPort, format: .number.grouping(.never))
                Button("Restart adb Server") { model.runVisible(["type": "restart_server"]) }
            }
            Section("Performance") {
                Stepper("Devices at once: \(draft.maxConcurrency)", value: $draft.maxConcurrency, in: 1...64)
            }
            HStack {
                Spacer()
                Button("Save") { model.saveConfig(draft); model.show("Preferences saved") }
                    .keyboardShortcut(.defaultAction)
                    .disabled(draft == model.config)
            }
        }
        .formStyle(.grouped)
        .onAppear { draft = model.config }
    }
}
