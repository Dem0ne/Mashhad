using System.Reflection;
using System.Runtime.InteropServices;

namespace Mashhad.Core.Mpv;

public enum MpvFormat
{
    None = 0,
    String = 1,
    OsdString = 2,
    Flag = 3,
    Int64 = 4,
    Double = 5,
    Node = 6,
}

public enum MpvEventId
{
    None = 0,
    Shutdown = 1,
    LogMessage = 2,
    GetPropertyReply = 3,
    SetPropertyReply = 4,
    CommandReply = 5,
    StartFile = 6,
    EndFile = 7,
    FileLoaded = 8,
    ClientMessage = 16,
    VideoReconfig = 17,
    AudioReconfig = 18,
    Seek = 20,
    PlaybackRestart = 21,
    PropertyChange = 22,
    QueueOverflow = 24,
    Hook = 25,
}

public enum MpvEndFileReason
{
    Eof = 0,
    Stop = 2,
    Quit = 3,
    Error = 4,
    Redirect = 5,
}

/// <summary>Raw libmpv client API (client.h). Strings are UTF-8.</summary>
internal static unsafe class MpvNative
{
    private const string Lib = "libmpv-2";

    /// <summary>Extra directory searched first for the native library (set by the app before first use).</summary>
    internal static string? SearchDirectory;

    private static readonly string[] Candidates =
    {
        "libmpv-2.dll", "mpv-2.dll", "mpv-1.dll", // Windows (shinchiro / official builds)
        "libmpv.so.2", "libmpv.so.1", "libmpv.so", // Linux (tests, CI)
        "libmpv.2.dylib", "libmpv.dylib",
    };

    static MpvNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(MpvNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != Lib) return IntPtr.Zero;
        var dirs = new List<string?> { SearchDirectory, AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "mpv") };
        foreach (var dir in dirs)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var c in Candidates)
            {
                var full = Path.Combine(dir, c);
                if (File.Exists(full) && NativeLibrary.TryLoad(full, out var h)) return h;
            }
        }
        foreach (var c in Candidates)
            if (NativeLibrary.TryLoad(c, assembly, path, out var h)) return h;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEvent
    {
        public int EventId;
        public int Error;
        public ulong ReplyUserdata;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventProperty
    {
        public IntPtr Name;
        public int Format;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventEndFile
    {
        public int Reason;
        public int Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventClientMessage
    {
        public int NumArgs;
        public IntPtr Args;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventLogMessage
    {
        public IntPtr Prefix;
        public IntPtr Level;
        public IntPtr Text;
        public int LogLevel;
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint mpv_client_api_version();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr mpv_create();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_set_option_string(IntPtr ctx, byte* name, byte* data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_command(IntPtr ctx, IntPtr* args);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_set_property_string(IntPtr ctx, byte* name, byte* data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr mpv_get_property_string(IntPtr ctx, byte* name);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_get_property(IntPtr ctx, byte* name, MpvFormat format, void* data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_set_property(IntPtr ctx, byte* name, MpvFormat format, void* data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_observe_property(IntPtr ctx, ulong replyUserdata, byte* name, MpvFormat format);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_request_log_messages(IntPtr ctx, byte* minLevel);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern MpvEvent* mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void mpv_wakeup(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void mpv_free(IntPtr data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr mpv_error_string(int error);
}
