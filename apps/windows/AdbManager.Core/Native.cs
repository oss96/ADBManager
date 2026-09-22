// P/Invoke bindings for crates/adbm-ffi/include/adbm.h. All strings are UTF-8 JSON.

using System.Runtime.InteropServices;

namespace AdbManager.Core;

internal static partial class Native
{
    private const string Lib = "adbm_ffi";

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr adbm_core_new(string? configJson);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr adbm_core_call(IntPtr core, string commandJson);

    [LibraryImport(Lib)]
    internal static partial IntPtr adbm_core_next_event(IntPtr core, uint timeoutMs);

    [LibraryImport(Lib)]
    internal static partial void adbm_core_free(IntPtr core);

    [LibraryImport(Lib)]
    internal static partial void adbm_string_free(IntPtr s);

    [LibraryImport(Lib)]
    internal static partial IntPtr adbm_last_error();

    [LibraryImport(Lib)]
    internal static partial IntPtr adbm_version();

    /// Copy a string returned by the library and free the original.
    internal static string? Take(IntPtr p)
    {
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(p); }
        finally { adbm_string_free(p); }
    }
}
