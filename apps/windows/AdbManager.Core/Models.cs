// Mirrors crates/adbm-core/src/api.rs (snake_case JSON).

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AdbManager.Core;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static T? As<T>(JsonNode? node) => node is null ? default : node.Deserialize<T>(Options);
}

public sealed class DeviceInfo
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? AndroidVersion { get; set; }
    public int? Sdk { get; set; }
    public string? BuildId { get; set; }
    public int? BatteryLevel { get; set; }
    public bool? Charging { get; set; }
}

public sealed class Device
{
    public string Serial { get; set; } = "";
    public string State { get; set; } = "";
    public string StateLabel { get; set; } = "";
    public string? StateHint { get; set; }
    public string Link { get; set; } = "usb";
    public string? Model { get; set; }
    public string? Product { get; set; }
    [JsonPropertyName("device")] public string? Codename { get; set; }
    public ulong? TransportId { get; set; }
    public DeviceInfo Info { get; set; } = new();
    public string DisplayName { get; set; } = "";
}

public sealed class JobInfo
{
    public ulong Id { get; set; }
    public string Title { get; set; } = "";
    public List<string> Serials { get; set; } = [];
    public bool Visible { get; set; }
    public string Status { get; set; } = "running";
}

public sealed class ServerStatus
{
    public string State { get; set; } = "starting";
    public int? Version { get; set; }
    public string? AdbPath { get; set; }
    public string? FastbootPath { get; set; }
    public string? Message { get; set; }
}

public sealed class DirEntry
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "file";
    public ulong Size { get; set; }
}

public sealed class Package
{
    public string Name { get; set; } = "";
    public bool System { get; set; }
}

public sealed class ApkInfo
{
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public ulong Size { get; set; }
    public string Package { get; set; } = "";
    public string? VersionName { get; set; }
    public ulong? VersionCode { get; set; }
    public string? Label { get; set; }
}

public sealed class Step
{
    public string Type { get; set; } = "";
    public string? Path { get; set; }
    public string? Package { get; set; }
    public string? Command { get; set; }
    public string? Mode { get; set; }
    public string? Local { get; set; }
    public string? Remote { get; set; }
    public string? Target { get; set; }
    public string? Image { get; set; }
    public int? TimeoutS { get; set; }
    public int? Ms { get; set; }
    public bool? IgnoreFailure { get; set; }

    public string Title
    {
        get
        {
            static string Short(string? p, string fallback) =>
                string.IsNullOrEmpty(p) ? fallback : System.IO.Path.GetFileName(p.TrimEnd('/', '\\'));
            return Type switch
            {
                "install" => $"Install {Short(Path, "an APK")}",
                "uninstall" => $"Uninstall {Package}",
                "shell" => $"Run “{Command}”",
                "reboot" => $"Reboot to {Cap(Mode ?? "system")}",
                "push" => $"Push {Short(Local, "a folder")} to {Remote}",
                "pull" => $"Pull {Remote}",
                "wait_for" => $"Wait for {(Target == "system" ? "Android" : Target)} (up to {TimeoutS ?? 120} s)",
                "fastboot_boot" => $"Boot {Short(Image, "an image")} (without flashing)",
                "delay" => $"Wait {(Ms ?? 0) / 1000.0:0.0} s",
                _ => Type,
            };
        }
    }

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

public sealed class Routine
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Step> Steps { get; set; } = [];
}

public sealed class Config
{
    public string? AdbPath { get; set; }
    public string? FastbootPath { get; set; }
    public string AdbHost { get; set; } = "127.0.0.1";
    public int AdbPort { get; set; } = 5037;
    public bool AutoStartServer { get; set; } = true;
    public int MaxConcurrency { get; set; } = 8;
    public int FastbootPollMs { get; set; } = 1500;

    public Config Clone() => (Config)MemberwiseClone();

    public bool SameAs(Config o) =>
        AdbPath == o.AdbPath && FastbootPath == o.FastbootPath && AdbHost == o.AdbHost && AdbPort == o.AdbPort
        && AutoStartServer == o.AutoStartServer && MaxConcurrency == o.MaxConcurrency && FastbootPollMs == o.FastbootPollMs;
}

/// Events from the core.
public abstract record CoreEvent
{
    public sealed record DevicesChanged(List<Device> Devices) : CoreEvent;
    public sealed record Server(ServerStatus Status) : CoreEvent;
    public sealed record JobStarted(JobInfo Job) : CoreEvent;
    public sealed record JobProgress(ulong JobId, string? Serial, ulong Done, ulong Total, string Message) : CoreEvent;
    public sealed record JobOutput(ulong JobId, string Serial, bool Stderr, string Text) : CoreEvent;
    public sealed record JobDeviceFinished(ulong JobId, string Serial, bool Ok, string Message) : CoreEvent;
    public sealed record JobFinished(ulong JobId, string Status, string Summary, JsonNode? Data) : CoreEvent;
    public sealed record Log(string Level, string Message) : CoreEvent;

    public static CoreEvent? Parse(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject o) return null;
        string S(string k) => o[k]?.GetValue<string>() ?? "";
        ulong U(string k) => o[k]?.GetValue<ulong>() ?? 0;
        return S("type") switch
        {
            "devices_changed" => new DevicesChanged(Json.As<List<Device>>(o["devices"]) ?? []),
            "server" => new Server(Json.As<ServerStatus>(o["status"]) ?? new()),
            "job_started" => new JobStarted(Json.As<JobInfo>(o["job"]) ?? new()),
            "job_progress" => new JobProgress(U("job_id"), o["serial"]?.GetValue<string>(), U("done"), U("total"), S("message")),
            "job_output" => new JobOutput(U("job_id"), S("serial"), S("stream") == "stderr", S("text")),
            "job_device_finished" => new JobDeviceFinished(U("job_id"), S("serial"), o["ok"]?.GetValue<bool>() ?? false, S("message")),
            "job_finished" => new JobFinished(U("job_id"), S("status"), S("summary"), o["data"]?.DeepClone()),
            "log" => new Log(S("level"), S("message")),
            _ => null,
        };
    }
}
