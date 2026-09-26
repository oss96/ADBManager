// Owns the native core handle and the event thread.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdbManager.Core;

public sealed class CoreStartException(string message) : Exception(message);

public sealed class CoreBridge : IDisposable
{
    private IntPtr _handle;
    private readonly Thread _thread;
    private volatile bool _stop;
    private readonly Action<Action> _post;

    /// Raised on the thread `post` runs callbacks on (the UI thread).
    public event Action<CoreEvent>? EventReceived;

    /// <param name="post">Schedules an action on the UI thread, in order.</param>
    public CoreBridge(Config config, Action<Action> post)
    {
        _post = post;
        var json = JsonSerializer.Serialize(config, Json.Options);
        _handle = Native.adbm_core_new(json);
        if (_handle == IntPtr.Zero)
            throw new CoreStartException(Native.Take(Native.adbm_last_error()) ?? "The core could not start.");
        _thread = new Thread(Pump) { IsBackground = true, Name = "adbm-events" };
        _thread.Start();
    }

    public static string Version =>
        System.Runtime.InteropServices.Marshal.PtrToStringUTF8(Native.adbm_version()) ?? "?";

    private void Pump()
    {
        while (!_stop)
        {
            var json = Native.Take(Native.adbm_core_next_event(_handle, 250));
            if (json is null) continue;
            var ev = CoreEvent.Parse(json);
            if (ev is not null) _post(() => EventReceived?.Invoke(ev));
        }
    }

    /// Send a command (an object with a "type" field). Returns the response.
    public JsonObject Call(JsonObject command)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        var reply = Native.Take(Native.adbm_core_call(_handle, command.ToJsonString())) ?? """{"type":"error","message":"no response"}""";
        return JsonNode.Parse(reply) as JsonObject ?? new JsonObject { ["type"] = "error", ["message"] = "bad response" };
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        _stop = true;
        _thread.Join(TimeSpan.FromSeconds(2));
        Native.adbm_core_free(_handle);
        _handle = IntPtr.Zero;
    }
}

/// Builds command JSON: Cmd.Of("shell", ("serials", list), ("command", "ls")).
public static class Cmd
{
    public static JsonObject Of(string type, params (string Key, object? Value)[] fields)
    {
        var o = new JsonObject { ["type"] = type };
        foreach (var (k, v) in fields)
            o[k] = v is null ? null : JsonSerializer.SerializeToNode(v, Json.Options);
        return o;
    }
}
