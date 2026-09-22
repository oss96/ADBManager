// App state fed by core events. All mutation happens on the main actor.

import AppKit
import Foundation
import UniformTypeIdentifiers

final class StopFlag: @unchecked Sendable {
    private let lock = NSLock()
    private var stopped = false
    var isStopped: Bool { lock.lock(); defer { lock.unlock() }; return stopped }
    func stop() { lock.lock(); stopped = true; lock.unlock() }
}

struct JobRow: Identifiable, Hashable {
    var info: JobInfo
    var parts: [String: Double] = [:]
    var detail: String = ""
    var failures: [String] = []
    var id: UInt64 { info.id }
    var fraction: Double {
        let n = Double(max(info.serials.count, 1))
        return min(parts.values.reduce(0, +) / n, 1)
    }
}

struct ShellLine: Identifiable, Hashable {
    let id = UUID()
    var text: String
    var kind: Kind
    enum Kind { case command, header, stdout, stderr, summary }
}

enum AppSection: String, CaseIterable, Identifiable {
    case devices, apps, files, shell, routines, activity
    var id: String { rawValue }
    var title: String { rawValue.capitalized }
    var symbol: String {
        switch self {
        case .devices: return "iphone.gen3"
        case .apps: return "square.grid.2x2"
        case .files: return "folder"
        case .shell: return "terminal"
        case .routines: return "list.bullet.rectangle"
        case .activity: return "waveform.path.ecg"
        }
    }
}

@MainActor
final class AppModel: ObservableObject {
    @Published var devices: [Device] = []
    @Published var targets: Set<String> = []
    @Published var server: ServerStatus?
    @Published var jobs: [JobRow] = []
    @Published var log: [String] = []
    @Published var shell: [ShellLine] = []
    @Published var section: AppSection = .devices
    @Published var banner: String?
    @Published var config: Config
    @Published var startError: String?

    private var core: CoreHandle?
    private let stop = StopFlag()
    private let stopped = DispatchSemaphore(value: 0)
    private var pending: [UInt64: (String, String, Any?) -> Void] = [:]
    private var titles: [UInt64: String] = [:]
    private var shellJobs: Set<UInt64> = []
    private var lastShellSerial: (UInt64, String)?
    private var bannerTask: Task<Void, Never>?

    init() {
        config = AppModel.loadConfig()
        let json = (try? encoder.encode(config)).flatMap { String(data: $0, encoding: .utf8) } ?? "{}"
        do {
            let core = try CoreHandle(configJSON: json)
            self.core = core
            startEventThread(core)
        } catch {
            startError = error.localizedDescription
        }
        NotificationCenter.default.addObserver(forName: NSApplication.willTerminateNotification, object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.shutdown() }
        }
    }

    // MARK: Core plumbing

    private func startEventThread(_ core: CoreHandle) {
        let stop = self.stop
        let stopped = self.stopped
        Thread.detachNewThread { [weak self] in
            while !stop.isStopped {
                guard let json = core.nextEvent(timeoutMs: 250) else { continue }
                DispatchQueue.main.async {
                    MainActor.assumeIsolated { self?.handle(json) }
                }
            }
            stopped.signal()
        }
    }

    func shutdown() {
        guard let core else { return }
        stop.stop()
        _ = stopped.wait(timeout: .now() + 2)
        core.free()
        self.core = nil
    }

    /// Send a command. Returns the job id when a job was started; errors are
    /// shown in the banner.
    @discardableResult
    func run(_ cmd: [String: Any], done: ((String, String, Any?) -> Void)? = nil) -> UInt64? {
        guard let core,
              let data = try? JSONSerialization.data(withJSONObject: cmd),
              let json = String(data: data, encoding: .utf8) else { return nil }
        let reply = core.call(json)
        guard let obj = try? JSONSerialization.jsonObject(with: Data(reply.utf8)) as? [String: Any] else { return nil }
        switch obj["type"] as? String {
        case "job":
            let id = (obj["job_id"] as? NSNumber)?.uint64Value ?? 0
            if let done { pending[id] = done }
            return id
        case "error":
            show(obj["message"] as? String ?? "Something went wrong.")
            return nil
        default:
            return nil
        }
    }

    /// A user-facing job: the banner reports the outcome with the job's title.
    @discardableResult
    func runVisible(_ cmd: [String: Any]) -> UInt64? {
        run(cmd) { [weak self] _, summary, _ in
            guard let self else { return }
            let title = self.finishingTitle
            self.show(title.isEmpty ? summary : "\(title): \(summary)")
        }
    }

    func callSync(_ cmd: [String: Any]) -> [String: Any]? {
        guard let core, let data = try? JSONSerialization.data(withJSONObject: cmd),
              let json = String(data: data, encoding: .utf8) else { return nil }
        return try? JSONSerialization.jsonObject(with: Data(core.call(json).utf8)) as? [String: Any]
    }

    func show(_ text: String) {
        banner = text
        bannerTask?.cancel()
        bannerTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 5_000_000_000)
            if !Task.isCancelled { self?.banner = nil }
        }
    }

    /// Title of the job whose completion callback is running.
    private var finishingTitle = ""

    private func handle(_ json: String) {
        guard let ev = CoreEvent.parse(json) else { return }
        switch ev {
        case .devicesChanged(let list):
            devices = list
            let present = Set(list.map(\.serial))
            targets = targets.intersection(present)
        case .server(let s):
            server = s
        case .jobStarted(let job):
            titles[job.id] = job.title
            if job.visible { jobs.insert(JobRow(info: job), at: 0) }
        case let .jobProgress(id, serial, done, total, message):
            guard let i = jobs.firstIndex(where: { $0.id == id }) else { return }
            let who = serial.map { "\(name(of: $0)): " } ?? ""
            jobs[i].detail = who + message
            if total > 0 { jobs[i].parts[serial ?? ""] = min(Double(done) / Double(total), 1) }
        case let .jobOutput(id, serial, isErr, text):
            guard shellJobs.contains(id) else { return }
            if lastShellSerial?.0 != id || lastShellSerial?.1 != serial {
                shell.append(ShellLine(text: "── \(name(of: serial)) ──", kind: .header))
                lastShellSerial = (id, serial)
            }
            shell.append(ShellLine(text: text.hasSuffix("\n") ? String(text.dropLast()) : text, kind: isErr ? .stderr : .stdout))
        case let .jobDeviceFinished(id, serial, ok, message):
            if let i = jobs.firstIndex(where: { $0.id == id }) {
                jobs[i].parts[serial] = 1
                if !ok { jobs[i].failures.append("\(name(of: serial)): \(message)") }
            }
            if shellJobs.contains(id), !ok {
                shell.append(ShellLine(text: "✗ \(name(of: serial)): \(message)", kind: .stderr))
                lastShellSerial = nil
            }
        case let .jobFinished(id, status, summary, data):
            if let i = jobs.firstIndex(where: { $0.id == id }) {
                jobs[i].info.status = status
                jobs[i].parts = Dictionary(uniqueKeysWithValues: jobs[i].info.serials.map { ($0, 1.0) })
                jobs[i].detail = ([summary] + (jobs[i].failures.count > 1 ? jobs[i].failures : [])).joined(separator: "\n")
            }
            if shellJobs.remove(id) != nil {
                shell.append(ShellLine(text: summary, kind: .summary))
                lastShellSerial = nil
            }
            finishingTitle = titles.removeValue(forKey: id) ?? ""
            if let done = pending.removeValue(forKey: id) {
                done(status, summary, data)
            }
        case let .log(level, message):
            log.append("[\(level)] \(message)")
        }
    }

    // MARK: Helpers

    /// Targets in list order.
    var orderedTargets: [String] { devices.map(\.serial).filter { targets.contains($0) } }
    var focused: Device? { devices.first { targets.contains($0.serial) } }
    func name(of serial: String) -> String { devices.first { $0.serial == serial }?.displayName ?? serial }
    var runningJobs: Int { jobs.filter { $0.info.status == "running" }.count }

    func runShell(_ command: String) {
        shell.append(ShellLine(text: "$ \(command)", kind: .command))
        if let id = run(["type": "shell", "serials": orderedTargets, "command": command]) {
            shellJobs.insert(id)
        }
    }

    func inspectApk(_ url: URL) -> ApkInfo? {
        guard let r = callSync(["type": "inspect_apk", "path": url.path]) else { return nil }
        if r["type"] as? String == "error" { show(r["message"] as? String ?? ""); return nil }
        return decodeData(ApkInfo.self, r["apk"])
    }

    func templates() -> [Routine] {
        guard let r = callSync(["type": "routine_templates"]) else { return [] }
        return decodeData([Routine].self, r["routines"]) ?? []
    }

    func runRoutine(_ routine: Routine) {
        guard let d = try? encoder.encode(routine), let obj = try? JSONSerialization.jsonObject(with: d) else { return }
        if runVisible(["type": "run_routine", "serials": orderedTargets, "routine": obj]) != nil { section = .activity }
    }

    func saveConfig(_ c: Config) {
        guard let d = try? encoder.encode(c), let obj = try? JSONSerialization.jsonObject(with: d) else { return }
        if let r = callSync(["type": "set_config", "config": obj]), r["type"] as? String == "error" {
            show(r["message"] as? String ?? "")
            return
        }
        config = c
        UserDefaults.standard.set(d, forKey: "config")
    }

    private static func loadConfig() -> Config {
        guard let d = UserDefaults.standard.data(forKey: "config"),
              let c = try? decoder.decode(Config.self, from: d) else { return Config() }
        return c
    }

    // MARK: Panels

    func chooseFile(types: [UTType] = [], directories: Bool = false, message: String) -> URL? {
        let p = NSOpenPanel()
        p.message = message
        p.canChooseFiles = !directories
        p.canChooseDirectories = directories
        p.allowsMultipleSelection = false
        if !types.isEmpty { p.allowedContentTypes = types }
        return p.runModal() == .OK ? p.url : nil
    }
}

extension UTType {
    static let apk = UTType(filenameExtension: "apk") ?? .data
    static let img = UTType(filenameExtension: "img") ?? .data
}

func count(_ n: Int, _ one: String, _ many: String) -> String { n == 1 ? "1 \(one)" : "\(n) \(many)" }

func humanSize(_ n: UInt64) -> String {
    ByteCountFormatter.string(fromByteCount: Int64(n), countStyle: .file)
}
