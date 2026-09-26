// View models for the Windows app. UI-framework independent: INotifyPropertyChanged
// and ObservableCollection work with WinUI bindings and are testable anywhere.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdbManager.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// One row of the device table; updated in place so selection survives.
public sealed class DeviceItem : Observable
{
    private Device _d;
    public DeviceItem(Device d) => _d = d;

    public Device Model => _d;
    public string Serial => _d.Serial;
    public string DisplayName => _d.DisplayName;
    public string StateLabel => _d.StateLabel;
    public string State => _d.State;
    public string? StateHint => _d.StateHint;
    public bool IsAdbUsable => _d.State is "online" or "recovery";

    /// Colour role of the state chip: online, attention, recovery, fastboot, muted.
    public string StateKind => StateKindOf(_d.State);

    public static string StateKindOf(string state) => state switch
    {
        "online" => "online",
        "unauthorized" or "no_permissions" or "authorizing" => "attention",
        "recovery" or "rescue" or "sideload" => "recovery",
        "bootloader" or "fastbootd" => "fastboot",
        _ => "muted",
    };

    public string AndroidText => (_d.Info.AndroidVersion, _d.Info.Sdk) switch
    {
        ({ } v, { } s) => $"{v} · API {s}",
        ({ } v, null) => v,
        _ => "—",
    };

    public string AndroidLong => (_d.Info.AndroidVersion, _d.Info.Sdk) switch
    {
        ({ } v, { } s) => $"{v} (API {s})",
        ({ } v, null) => v,
        _ => "—",
    };

    public string BatteryText => _d.State == "online" && _d.Info.BatteryLevel is { } l ? $"{l}%" : "—";

    public string BatteryLong => _d.Info.BatteryLevel is { } l ? (_d.Info.Charging == true ? $"{l}% · charging" : $"{l}%") : "—";

    public string Connection => _d.Link switch { "tcp" => "Network", "emulator" => "Emulator", _ => "USB" }
        + (_d.TransportId is { } t ? $" · transport {t}" : "");

    public string Subtitle => string.Join(" · ", new[] { _d.Codename ?? _d.Product, _d.Serial }.Where(s => !string.IsNullOrEmpty(s)));

    public void Update(Device d)
    {
        _d = d;
        Raise(""); // all properties
    }
}

public sealed class JobItem : Observable
{
    private readonly Dictionary<string, double> _parts = [];
    private readonly List<string> _failures = [];
    private string _status = "running";
    private string _detail = "";
    private double _progress;

    public JobItem(JobInfo info) { Info = info; }
    public JobInfo Info { get; }
    public ulong Id => Info.Id;
    public string Title => Info.Title;
    public string Status { get => _status; private set { if (Set(ref _status, value)) { Raise(nameof(StatusLabel)); Raise(nameof(StatusKind)); Raise(nameof(IsRunning)); } } }
    public string Detail { get => _detail; private set => Set(ref _detail, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool IsRunning => Status == "running";

    public string StatusLabel => Status switch
    {
        "succeeded" => "Done",
        "partially_failed" => "Partly failed",
        "failed" => "Failed",
        "cancelled" => "Cancelled",
        _ => "Running",
    };

    public string StatusKind => Status switch
    {
        "succeeded" => "online",
        "partially_failed" => "attention",
        "failed" => "error",
        "cancelled" => "muted",
        _ => "fastboot",
    };

    internal void OnProgress(string who, string? serial, ulong done, ulong total, string message)
    {
        Detail = who + message;
        if (total > 0)
        {
            _parts[serial ?? ""] = Math.Min((double)done / total, 1);
            Recalc();
        }
    }

    internal void OnDevice(string serial, string name, bool ok, string message)
    {
        _parts[serial] = 1;
        if (!ok) _failures.Add($"{name}: {message}");
        Recalc();
    }

    internal void OnFinished(string status, string summary)
    {
        Status = status;
        Progress = 1;
        Detail = _failures.Count > 1 ? summary + "\n" + string.Join("\n", _failures) : summary;
    }

    private void Recalc() => Progress = Math.Min(_parts.Values.Sum() / Math.Max(Info.Serials.Count, 1), 1);
}

public sealed record ShellLine(string Text, ShellLineKind Kind);

public enum ShellLineKind { Command, Header, Stdout, Stderr, Summary }

public delegate void JobDone(string status, string summary, JsonNode? data);

/// App-wide state fed by core events. Every method must be called on the UI thread.
public sealed class AppState : Observable, IDisposable
{
    private readonly CoreBridge _core;
    private readonly Dictionary<ulong, JobDone> _pending = [];
    private readonly Dictionary<ulong, string> _titles = [];
    private readonly HashSet<ulong> _shellJobs = [];
    private (ulong Job, string Serial)? _lastShell;
    private List<string> _targets = [];
    private ServerStatus _server = new();
    private string _finishingTitle = "";

    public AppState(Config config, Action<Action> postToUi)
    {
        Config = config;
        _core = new CoreBridge(config, postToUi);
        _core.EventReceived += Handle;
    }

    public ObservableCollection<DeviceItem> Devices { get; } = [];
    public ObservableCollection<JobItem> Jobs { get; } = [];
    public ObservableCollection<ShellLine> Shell { get; } = [];
    public ObservableCollection<string> Log { get; } = [];
    public Config Config { get; private set; }

    public ServerStatus Server { get => _server; private set => Set(ref _server, value); }

    /// Raised with a one-line outcome to show in the InfoBar.
    public event Action<string, bool>? Banner;

    /// Raised when the targets change.
    public event Action? TargetsChanged;

    /// Selected devices, in list order. Every action applies to these.
    public IReadOnlyList<string> Targets => _targets;

    public DeviceItem? Focused => Devices.FirstOrDefault(d => _targets.Contains(d.Serial));

    public int RunningJobs => Jobs.Count(j => j.IsRunning);

    public string TargetsLabel => _targets.Count switch { 0 => "No targets", 1 => "1 target", var n => $"{n} targets" };

    public void SetTargets(IEnumerable<string> serials)
    {
        var ordered = Devices.Select(d => d.Serial).Where(serials.Contains).ToList();
        if (ordered.SequenceEqual(_targets)) return;
        _targets = ordered;
        Raise(nameof(Targets));
        Raise(nameof(TargetsLabel));
        Raise(nameof(Focused));
        TargetsChanged?.Invoke();
    }

    public string NameOf(string serial) => Devices.FirstOrDefault(d => d.Serial == serial)?.DisplayName ?? serial;

    public static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";

    // ---------------------------------------------------------------- calls

    /// Send a command; a started job calls `done` when it finishes. Errors
    /// are reported through `Banner`.
    public ulong? Run(JsonObject cmd, JobDone? done = null)
    {
        var r = _core.Call(cmd);
        switch (r["type"]?.GetValue<string>())
        {
            case "job":
                var id = r["job_id"]!.GetValue<ulong>();
                if (done is not null) _pending[id] = done;
                return id;
            case "error":
                Banner?.Invoke(r["message"]?.GetValue<string>() ?? "Something went wrong.", true);
                return null;
            default:
                return null;
        }
    }

    /// A user-facing job: the banner reports the outcome with the job title.
    public ulong? RunVisible(JsonObject cmd) => Run(cmd, (status, summary, _) =>
        Banner?.Invoke(string.IsNullOrEmpty(_finishingTitle) ? summary : $"{_finishingTitle}: {summary}", status != "succeeded"));

    public JsonObject CallNow(JsonObject cmd) => _core.Call(cmd);

    public void RunShell(string command)
    {
        Shell.Add(new ShellLine($"$ {command}", ShellLineKind.Command));
        if (Run(Cmd.Of("shell", ("serials", Targets), ("command", command))) is { } id) _shellJobs.Add(id);
    }

    public ApkInfo? InspectApk(string path)
    {
        var r = _core.Call(Cmd.Of("inspect_apk", ("path", path)));
        if (r["type"]?.GetValue<string>() == "error")
        {
            Banner?.Invoke(r["message"]?.GetValue<string>() ?? "", true);
            return null;
        }
        return Json.As<ApkInfo>(r["apk"]);
    }

    public List<Routine> Templates() => Json.As<List<Routine>>(_core.Call(Cmd.Of("routine_templates"))["routines"]) ?? [];

    public ulong? RunRoutine(Routine r) =>
        RunVisible(Cmd.Of("run_routine", ("serials", Targets), ("routine", r)));

    public bool SaveConfig(Config c)
    {
        var r = _core.Call(Cmd.Of("set_config", ("config", c)));
        if (r["type"]?.GetValue<string>() == "error")
        {
            Banner?.Invoke(r["message"]?.GetValue<string>() ?? "", true);
            return false;
        }
        Config = c.Clone();
        Raise(nameof(Config));
        return true;
    }

    // --------------------------------------------------------------- events

    private void Handle(CoreEvent ev)
    {
        switch (ev)
        {
            case CoreEvent.DevicesChanged e:
                SyncDevices(e.Devices);
                break;
            case CoreEvent.Server e:
                Server = e.Status;
                break;
            case CoreEvent.JobStarted e:
                _titles[e.Job.Id] = e.Job.Title;
                if (e.Job.Visible)
                {
                    Jobs.Insert(0, new JobItem(e.Job));
                    Raise(nameof(RunningJobs));
                }
                break;
            case CoreEvent.JobProgress e:
                Job(e.JobId)?.OnProgress(e.Serial is null ? "" : $"{NameOf(e.Serial)}: ", e.Serial, e.Done, e.Total, e.Message);
                break;
            case CoreEvent.JobOutput e when _shellJobs.Contains(e.JobId):
                if (_lastShell != (e.JobId, e.Serial))
                {
                    Shell.Add(new ShellLine($"── {NameOf(e.Serial)} ──", ShellLineKind.Header));
                    _lastShell = (e.JobId, e.Serial);
                }
                Shell.Add(new ShellLine(e.Text.TrimEnd('\n'), e.Stderr ? ShellLineKind.Stderr : ShellLineKind.Stdout));
                break;
            case CoreEvent.JobDeviceFinished e:
                Job(e.JobId)?.OnDevice(e.Serial, NameOf(e.Serial), e.Ok, e.Message);
                if (_shellJobs.Contains(e.JobId) && !e.Ok)
                {
                    Shell.Add(new ShellLine($"✗ {NameOf(e.Serial)}: {e.Message}", ShellLineKind.Stderr));
                    _lastShell = null;
                }
                break;
            case CoreEvent.JobFinished e:
                Job(e.JobId)?.OnFinished(e.Status, e.Summary);
                Raise(nameof(RunningJobs));
                if (_shellJobs.Remove(e.JobId))
                {
                    Shell.Add(new ShellLine(e.Summary, ShellLineKind.Summary));
                    _lastShell = null;
                }
                _finishingTitle = _titles.Remove(e.JobId, out var t) ? t : "";
                if (_pending.Remove(e.JobId, out var done)) done(e.Status, e.Summary, e.Data);
                _finishingTitle = "";
                break;
            case CoreEvent.Log e:
                Log.Add($"[{e.Level}] {e.Message}");
                break;
        }
    }

    private JobItem? Job(ulong id) => Jobs.FirstOrDefault(j => j.Id == id);

    private void SyncDevices(List<Device> list)
    {
        // Remove gone, update existing, insert new at their position.
        for (var i = Devices.Count - 1; i >= 0; i--)
            if (!list.Any(d => d.Serial == Devices[i].Serial)) Devices.RemoveAt(i);
        for (var i = 0; i < list.Count; i++)
        {
            var d = list[i];
            var existing = Devices.FirstOrDefault(x => x.Serial == d.Serial);
            if (existing is null) Devices.Insert(Math.Min(i, Devices.Count), new DeviceItem(d));
            else
            {
                existing.Update(d);
                var at = Devices.IndexOf(existing);
                if (at != i && i < Devices.Count) Devices.Move(at, i);
            }
        }
        var before = _targets.Count;
        _targets = _targets.Where(t => list.Any(d => d.Serial == t)).ToList();
        if (_targets.Count != before) { Raise(nameof(TargetsLabel)); TargetsChanged?.Invoke(); }
        Raise(nameof(Focused));
    }

    public void Dispose() => _core.Dispose();

    /// Config persisted next to the user's other app data.
    public static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ADB Manager", "config.json");

    public static Config LoadConfig()
    {
        try { return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), Json.Options) ?? new(); }
        catch { return new(); }
    }

    public static void StoreConfig(Config c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(c, Json.Options));
    }
}
