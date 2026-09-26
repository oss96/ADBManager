// Smoke test of the Windows app's non-UI layer. Exit code 0 = pass.
using System.Collections.Concurrent;
using AdbManager.Core;

var port = args.Length > 0 ? int.Parse(args[0]) : 5199;
var ui = new BlockingCollection<Action>();
var state = new AppState(new Config { AdbPort = port, AutoStartServer = false, FastbootPath = "/nonexistent" }, ui.Add);
var banners = new List<string>();
state.Banner += (text, _) => banners.Add(text);

// Pump the "UI thread" until `done` or timeout.
bool Pump(Func<bool> done, int seconds = 10)
{
    var until = DateTime.UtcNow.AddSeconds(seconds);
    while (DateTime.UtcNow < until)
    {
        if (done()) return true;
        if (ui.TryTake(out var a, 100)) a();
    }
    return done();
}

void Check(bool ok, string what)
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
    if (!ok) Environment.Exit(1);
}

Console.WriteLine($"core {CoreBridge.Version}");
Check(Pump(() => state.Devices.Count == 5 && state.Devices.Where(d => d.State == "online").All(d => d.AndroidText != "—")), "devices synced with details");
Check(state.Server.State == "running" && state.Server.Version == 0x29, "server running");
var locked = state.Devices.First(d => d.State == "unauthorized");
Check(locked.StateKind == "attention" && locked.StateHint!.Contains("USB debugging"), "unauthorized chip and hint");
foreach (var d in state.Devices) Console.WriteLine($"     {d.DisplayName,-22} {d.Serial,-16} {d.StateLabel,-13} {d.AndroidText,-14} {d.BatteryText}");

var first = state.Devices[0];
state.SetTargets([state.Devices[1].Serial, first.Serial]);
Check(state.Targets[0] == first.Serial && state.TargetsLabel == "2 targets", "targets kept in list order");
Check(state.Focused == first, "focused is first target");

state.RunShell("echo hello windows");
Check(Pump(() => state.Shell.Any(l => l.Kind == ShellLineKind.Summary)), "shell finished");
Check(state.Shell.Count(l => l.Kind == ShellLineKind.Stdout && l.Text == "hello windows") == 2, "shell output from both targets");
Check(state.Shell.Count(l => l.Kind == ShellLineKind.Header) == 2, "one header per device");

ulong? job = state.RunVisible(Cmd.Of("reboot", ("serials", state.Targets), ("mode", "recovery")));
Check(job is not null && Pump(() => state.Jobs.Any(j => j.Id == job && !j.IsRunning)), "reboot job finished");
var jobItem = state.Jobs.First(j => j.Id == job);
Check(jobItem.StatusLabel == "Done" && jobItem.Progress == 1, "job row done at 100%");
Check(banners.Last() == "Reboot 2 devices to Recovery: 2 devices succeeded", $"banner has title: {banners.Last()}");

List<DirEntry>? entries = null;
state.Run(Cmd.Of("list_dir", ("serial", first.Serial), ("path", "/sdcard")), (s, _, data) => entries = Json.As<List<DirEntry>>(data?["entries"]));
Check(Pump(() => entries is not null) && entries!.Any(e => e.Name == "Download" && e.Kind == "dir"), "list_dir decoded");

var templates = state.Templates();
Check(templates.Count == 3 && templates[0].Steps[2].Title == "Boot an image (without flashing)", "templates decoded");
var bad = state.RunRoutine(templates[0]);
Check(bad is null && banners.Last().StartsWith("Step 3"), "routine validation error shown");
var r = new Routine
{
    Id = "smoke", Name = "Smoke",
    Steps =
    [
        new Step { Type = "wait_for", Target = "adb", TimeoutS = 5 },
        new Step { Type = "shell", Command = "echo from routine" },
        new Step { Type = "shell", Command = "exit 1", IgnoreFailure = true },
        new Step { Type = "delay", Ms = 10 },
    ],
};
var rid = state.RunRoutine(r);
Check(rid is not null && Pump(() => state.Jobs.Any(j => j.Id == rid && !j.IsRunning)), "routine ran (serialised from C#)");
Check(state.Jobs.First(j => j.Id == rid).StatusLabel == "Done", "routine succeeded, ignore_failure honoured");

var cfg = state.Config.Clone();
cfg.MaxConcurrency = 3;
Check(state.SaveConfig(cfg) && state.Config.MaxConcurrency == 3, "config accepted by core");
Check(state.InspectApk("/nonexistent.apk") is null, "bad APK reported");

state.Dispose();
Console.WriteLine("C# SMOKE OK");
