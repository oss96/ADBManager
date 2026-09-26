// Bridge to the Rust core through its C ABI (crates/adbm-ffi/include/adbm.h).
// Messages are JSON; the types below mirror crates/adbm-core/src/api.rs.

import CAdbm
import Foundation

enum CoreError: Error, LocalizedError {
    case start(String)
    var errorDescription: String? {
        switch self { case .start(let m): return m }
    }
}

/// Owns the native handle. `call` and `nextEvent` are safe from any thread.
final class CoreHandle: @unchecked Sendable {
    private let ptr: OpaquePointer

    init(configJSON: String) throws {
        let p: OpaquePointer? = configJSON.withCString { adbm_core_new($0) }
        guard let p else {
            var message = "The core could not start."
            if let e = adbm_last_error() {
                message = String(cString: e)
                adbm_string_free(e)
            }
            throw CoreError.start(message)
        }
        ptr = p
    }

    func call(_ json: String) -> String {
        guard let out = json.withCString({ adbm_core_call(ptr, $0) }) else {
            return #"{"type":"error","message":"no response"}"#
        }
        defer { adbm_string_free(out) }
        return String(cString: out)
    }

    func nextEvent(timeoutMs: UInt32) -> String? {
        guard let e = adbm_core_next_event(ptr, timeoutMs) else { return nil }
        defer { adbm_string_free(e) }
        return String(cString: e)
    }

    /// Call once, after the event thread has stopped.
    func free() { adbm_core_free(ptr) }

    static var version: String { String(cString: adbm_version()) }
}

// MARK: - Models (snake_case JSON)

let decoder: JSONDecoder = {
    let d = JSONDecoder()
    d.keyDecodingStrategy = .convertFromSnakeCase
    return d
}()

let encoder: JSONEncoder = {
    let e = JSONEncoder()
    e.keyEncodingStrategy = .convertToSnakeCase
    return e
}()

struct DeviceInfo: Decodable, Hashable {
    var manufacturer: String?
    var model: String?
    var androidVersion: String?
    var sdk: Int?
    var buildId: String?
    var batteryLevel: Int?
    var charging: Bool?
}

struct Device: Decodable, Identifiable, Hashable {
    var serial: String
    var state: String
    var stateLabel: String
    var stateHint: String?
    var link: String
    var model: String?
    var product: String?
    var device: String?
    var transportId: UInt64?
    var info: DeviceInfo
    var displayName: String
    var id: String { serial }

    var isAdbUsable: Bool { state == "online" || state == "recovery" }

    var androidText: String {
        switch (info.androidVersion, info.sdk) {
        case let (v?, s?): return "\(v) · API \(s)"
        case let (v?, nil): return v
        default: return "—"
        }
    }

    var androidLong: String {
        guard let v = info.androidVersion else { return "—" }
        guard let s = info.sdk else { return v }
        return "\(v) (API \(s))"
    }

    var batteryLong: String {
        guard let l = info.batteryLevel else { return "—" }
        return info.charging == true ? "\(l)% · charging" : "\(l)%"
    }

    var batteryText: String {
        guard state == "online", let l = info.batteryLevel else { return "—" }
        return "\(l)%"
    }
}

struct JobInfo: Decodable, Hashable {
    var id: UInt64
    var title: String
    var serials: [String]
    var visible: Bool
    var status: String
}

struct ServerStatus: Decodable, Hashable {
    var state: String
    var version: Int?
    var adbPath: String?
    var fastbootPath: String?
    var message: String?
    var searched: [String]?
}

struct DirEntry: Decodable, Identifiable, Hashable {
    var name: String
    var kind: String
    var size: UInt64
    var id: String { name }
}

struct Package: Decodable, Identifiable, Hashable {
    var name: String
    var system: Bool
    var id: String { name }
}

struct ApkInfo: Decodable, Hashable {
    var path: String
    var fileName: String
    var size: UInt64
    var package: String
    var versionName: String?
    var versionCode: UInt64?
    var label: String?
}

struct Step: Codable, Hashable {
    var type: String
    var path: String?
    var package: String?
    var command: String?
    var mode: String?
    var local: String?
    var remote: String?
    var target: String?
    var image: String?
    var timeoutS: Int?
    var ms: Int?
    var ignoreFailure: Bool?

    var title: String {
        func short(_ p: String?, _ fallback: String) -> String {
            guard let p, !p.isEmpty else { return fallback }
            return (p as NSString).lastPathComponent
        }
        switch type {
        case "install": return "Install \(short(path, "an APK"))"
        case "uninstall": return "Uninstall \(package ?? "")"
        case "shell": return "Run “\(command ?? "")”"
        case "reboot": return "Reboot to \((mode ?? "system").capitalized)"
        case "push": return "Push \(short(local, "a folder")) to \(remote ?? "")"
        case "pull": return "Pull \(remote ?? "")"
        case "wait_for":
            let what = ["system": "Android", "recovery": "recovery", "fastboot": "fastboot", "adb": "adb"][target ?? ""] ?? (target ?? "")
            return "Wait for \(what) (up to \(timeoutS ?? 120) s)"
        case "fastboot_boot": return "Boot \(short(image, "an image")) (without flashing)"
        case "delay": return String(format: "Wait %.1f s", Double(ms ?? 0) / 1000)
        default: return type
        }
    }
}

struct Routine: Codable, Identifiable, Hashable {
    var id: String
    var name: String
    var description: String
    var steps: [Step]
}

struct Config: Codable, Equatable {
    var adbPath: String?
    var fastbootPath: String?
    var adbHost: String = "127.0.0.1"
    var adbPort: Int = 5037
    var autoStartServer: Bool = true
    var maxConcurrency: Int = 8
    var fastbootPollMs: Int = 1500
}

/// Events from the core. Only the fields the UI uses are decoded.
enum CoreEvent {
    case devicesChanged([Device])
    case server(ServerStatus)
    case jobStarted(JobInfo)
    case jobProgress(job: UInt64, serial: String?, done: UInt64, total: UInt64, message: String)
    case jobOutput(job: UInt64, serial: String, stderr: Bool, text: String)
    case jobDeviceFinished(job: UInt64, serial: String, ok: Bool, message: String)
    case jobFinished(job: UInt64, status: String, summary: String, data: Any?)
    case log(level: String, message: String)

    static func parse(_ json: String) -> CoreEvent? {
        let bytes = Data(json.utf8)
        guard let obj = try? JSONSerialization.jsonObject(with: bytes) as? [String: Any],
              let type = obj["type"] as? String else { return nil }
        func dec<T: Decodable>(_: T.Type, _ key: String) -> T? {
            guard let v = obj[key], let d = try? JSONSerialization.data(withJSONObject: v) else { return nil }
            return try? decoder.decode(T.self, from: d)
        }
        let job = (obj["job_id"] as? NSNumber)?.uint64Value ?? 0
        switch type {
        case "devices_changed": return dec([Device].self, "devices").map { .devicesChanged($0) }
        case "server": return dec(ServerStatus.self, "status").map { .server($0) }
        case "job_started": return dec(JobInfo.self, "job").map { .jobStarted($0) }
        case "job_progress":
            return .jobProgress(job: job, serial: obj["serial"] as? String,
                                done: (obj["done"] as? NSNumber)?.uint64Value ?? 0,
                                total: (obj["total"] as? NSNumber)?.uint64Value ?? 0,
                                message: obj["message"] as? String ?? "")
        case "job_output":
            return .jobOutput(job: job, serial: obj["serial"] as? String ?? "",
                              stderr: (obj["stream"] as? String) == "stderr", text: obj["text"] as? String ?? "")
        case "job_device_finished":
            return .jobDeviceFinished(job: job, serial: obj["serial"] as? String ?? "",
                                      ok: obj["ok"] as? Bool ?? false, message: obj["message"] as? String ?? "")
        case "job_finished":
            return .jobFinished(job: job, status: obj["status"] as? String ?? "failed",
                                summary: obj["summary"] as? String ?? "", data: obj["data"])
        case "log": return .log(level: obj["level"] as? String ?? "info", message: obj["message"] as? String ?? "")
        default: return nil
        }
    }
}

/// Decode a `data` payload from `job_finished` into a model type.
func decodeData<T: Decodable>(_ type: T.Type, _ data: Any?) -> T? {
    guard let data, JSONSerialization.isValidJSONObject(data),
          let d = try? JSONSerialization.data(withJSONObject: data) else { return nil }
    return try? decoder.decode(T.self, from: d)
}
