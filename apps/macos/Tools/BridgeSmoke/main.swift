// Drives Sources/AdbManager/Core.swift against the real core and the fake
// adb server (see scripts/check-bridges.sh). Runs on Linux and macOS.
import Foundation
// Drives the macOS app's bridge (Core.swift) exactly as AppModel does.
let port = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "5199"
let core = try CoreHandle(configJSON: #"{"adb_port":"# + port + #","auto_start_server":false,"fastboot_path":"/nonexistent"}"#)
print("core", CoreHandle.version)
var devices: [Device] = []
let deadline = Date().addingTimeInterval(10)
while Date() < deadline {
    guard let json = core.nextEvent(timeoutMs: 200), let ev = CoreEvent.parse(json) else { continue }
    if case .devicesChanged(let list) = ev, list.count == 5, list.filter({ $0.state == "online" }).allSatisfy({ $0.info.sdk != nil }) {
        devices = list; break
    }
}
precondition(devices.count == 5, "no devices")
for d in devices { print(d.displayName, d.serial, d.stateLabel, d.androidText, d.batteryText, d.stateHint ?? "") }
let reply = core.call(#"{"type":"shell","serials":["3A281FDJH00B7C","R52W70ABC1D"],"command":"echo swift"}"#)
print("reply", reply)
var outputs = 0, finished = false
while !finished, Date() < deadline.addingTimeInterval(10) {
    guard let json = core.nextEvent(timeoutMs: 200), let ev = CoreEvent.parse(json) else { continue }
    switch ev {
    case .jobOutput(_, let s, _, let t): outputs += 1; print("out", s, t.trimmingCharacters(in: .newlines))
    case .jobFinished(_, let st, let sum, _): print("finished", st, sum); finished = true
    default: break
    }
}
precondition(outputs == 2 && finished)
let t = core.call(#"{"type":"routine_templates"}"#)
let obj = try JSONSerialization.jsonObject(with: Data(t.utf8)) as! [String: Any]
let routines = decodeData([Routine].self, obj["routines"])!
print("routines", routines.map(\.name), routines[0].steps.map(\.title))
// Round trip: encode a routine the way the app sends it back.
var r = routines[2]; r.steps[1].ms = 10
let back = try JSONSerialization.jsonObject(with: encoder.encode(r))
let cmd = try JSONSerialization.data(withJSONObject: ["type": "run_routine", "serials": ["3A281FDJH00B7C"], "routine": back])
print("run_routine ->", core.call(String(data: cmd, encoding: .utf8)!))
let cfg = try String(data: encoder.encode(Config()), encoding: .utf8)!
print("config json", cfg)
core.free()
print("SWIFT SMOKE OK")
