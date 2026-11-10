using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Mashhad.Core.Mpv;

public sealed class MpvException : Exception
{
    public int Code { get; }
    public MpvException(string what, int code, string detail) : base($"{what}: {detail} ({code})") => Code = code;
}

public sealed record MpvTrack(long Id, string Type, string? Title, string? Language, string? Codec,
    bool Selected, bool External, bool Default, bool Image)
{
    public string DisplayName
    {
        get
        {
            var parts = new List<string> { "#" + Id.ToString(CultureInfo.InvariantCulture) };
            if (!string.IsNullOrEmpty(Language)) parts.Add(Language!);
            if (!string.IsNullOrEmpty(Title)) parts.Add(Title!);
            if (!string.IsNullOrEmpty(Codec)) parts.Add("(" + Codec + ")");
            return string.Join(" ", parts);
        }
    }
}

/// <summary>
/// A thin, safe wrapper over one libmpv handle. Events are delivered on a dedicated thread;
/// callers marshal to their UI thread. All other members may be called from any thread.
/// </summary>
public sealed unsafe class MpvPlayer : IDisposable
{
    private IntPtr _ctx;
    private readonly Thread _eventThread;
    private volatile bool _stopping;

    /// <summary>Tells the interop layer where libmpv lives (call before creating the first player).</summary>
    public static void SetLibraryDirectory(string? directory) => MpvNative.SearchDirectory = directory;

    /// <summary>True when the native library can be loaded.</summary>
    public static bool IsAvailable(out string? error)
    {
        try
        {
            MpvNative.mpv_client_api_version();
            error = null;
            return true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            error = e.Message;
            return false;
        }
    }

    public static string ApiVersion
    {
        get
        {
            uint v = MpvNative.mpv_client_api_version();
            return $"{v >> 16}.{v & 0xFFFF}";
        }
    }

    public event Action<string, object?>? PropertyChanged;
    public event Action? StartFile;
    public event Action? FileLoaded;
    public event Action<MpvEndFileReason>? EndFile;
    public event Action? VideoReconfig;
    public event Action? PlaybackRestart;
    public event Action<string[]>? ClientMessage;
    /// <summary>(level, prefix, text)</summary>
    public event Action<string, string, string>? LogMessage;

    /// <param name="options">Options applied before initialisation; a failure throws.</param>
    /// <param name="optionalOptions">Options that may not exist in every libmpv build; failures are ignored.</param>
    public MpvPlayer(IEnumerable<KeyValuePair<string, string>> options,
        IEnumerable<KeyValuePair<string, string>>? optionalOptions = null, string? logLevel = null)
    {
        _ctx = MpvNative.mpv_create();
        if (_ctx == IntPtr.Zero) throw new MpvException("mpv_create", -1, "could not create an mpv instance");
        try
        {
            foreach (var (name, value) in options)
                Check(SetOptionCore(name, value), $"option {name}={value}");
            if (optionalOptions is not null)
                foreach (var (name, value) in optionalOptions) SetOptionCore(name, value);
            Check(MpvNative.mpv_initialize(_ctx), "mpv_initialize");
            if (logLevel is not null)
                fixed (byte* lvl = Utf8(logLevel)) MpvNative.mpv_request_log_messages(_ctx, lvl);
        }
        catch
        {
            MpvNative.mpv_terminate_destroy(_ctx);
            _ctx = IntPtr.Zero;
            throw;
        }
        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "mpv events" };
        _eventThread.Start();
    }

    private int SetOptionCore(string name, string value)
    {
        fixed (byte* n = Utf8(name))
        fixed (byte* v = Utf8(value))
            return MpvNative.mpv_set_option_string(_ctx, n, v);
    }

    // ---- commands -------------------------------------------------------------------------

    public void Command(params string[] args) => Check(CommandCore(args), "command " + string.Join(' ', args));

    /// <summary>Runs a command, returning false instead of throwing when mpv rejects it.</summary>
    public bool TryCommand(params string[] args) => CommandCore(args) >= 0;

    private int CommandCore(string[] args)
    {
        if (_ctx == IntPtr.Zero) return -1;
        var ptrs = new IntPtr[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++) ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            fixed (IntPtr* p = ptrs) return MpvNative.mpv_command(_ctx, p);
        }
        finally
        {
            foreach (var p in ptrs) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
        }
    }

    // ---- properties -----------------------------------------------------------------------

    public string? GetString(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        IntPtr p;
        fixed (byte* n = Utf8(name)) p = MpvNative.mpv_get_property_string(_ctx, n);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(p); }
        finally { MpvNative.mpv_free(p); }
    }

    public double? GetDouble(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        double v;
        fixed (byte* n = Utf8(name))
            return MpvNative.mpv_get_property(_ctx, n, MpvFormat.Double, &v) >= 0 ? v : null;
    }

    public long? GetLong(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        long v;
        fixed (byte* n = Utf8(name))
            return MpvNative.mpv_get_property(_ctx, n, MpvFormat.Int64, &v) >= 0 ? v : null;
    }

    public bool? GetFlag(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        int v;
        fixed (byte* n = Utf8(name))
            return MpvNative.mpv_get_property(_ctx, n, MpvFormat.Flag, &v) >= 0 ? v != 0 : null;
    }

    public bool Set(string name, string value)
    {
        if (_ctx == IntPtr.Zero) return false;
        fixed (byte* n = Utf8(name))
        fixed (byte* v = Utf8(value))
            return MpvNative.mpv_set_property_string(_ctx, n, v) >= 0;
    }

    public bool Set(string name, double value)
    {
        if (_ctx == IntPtr.Zero) return false;
        fixed (byte* n = Utf8(name))
            return MpvNative.mpv_set_property(_ctx, n, MpvFormat.Double, &value) >= 0;
    }

    public bool Set(string name, bool value)
    {
        if (_ctx == IntPtr.Zero) return false;
        int v = value ? 1 : 0;
        fixed (byte* n = Utf8(name))
            return MpvNative.mpv_set_property(_ctx, n, MpvFormat.Flag, &v) >= 0;
    }

    public void Observe(string name, MpvFormat format)
    {
        fixed (byte* n = Utf8(name))
            Check(MpvNative.mpv_observe_property(_ctx, 0, n, format), "observe " + name);
    }

    public IReadOnlyList<MpvTrack> GetTracks() => ParseTracks(GetString("track-list"));

    public static IReadOnlyList<MpvTrack> ParseTracks(string? json)
    {
        var list = new List<MpvTrack>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
            foreach (var t in doc.RootElement.EnumerateArray())
            {
                string? Str(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                bool Flag(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
                long id = t.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var i) ? i : 0;
                list.Add(new MpvTrack(id, Str("type") ?? "", Str("title"), Str("lang"), Str("codec"),
                    Flag("selected"), Flag("external"), Flag("default"), Flag("image")));
            }
        }
        catch (JsonException) { }
        return list;
    }

    // ---- events ---------------------------------------------------------------------------

    private void EventLoop()
    {
        while (!_stopping)
        {
            var ev = MpvNative.mpv_wait_event(_ctx, 1.0);
            if (_stopping) break;
            var id = (MpvEventId)ev->EventId;
            try
            {
                switch (id)
                {
                    case MpvEventId.None:
                        break;
                    case MpvEventId.Shutdown:
                        return;
                    case MpvEventId.PropertyChange:
                        OnProperty((MpvNative.MpvEventProperty*)ev->Data);
                        break;
                    case MpvEventId.StartFile:
                        StartFile?.Invoke();
                        break;
                    case MpvEventId.FileLoaded:
                        FileLoaded?.Invoke();
                        break;
                    case MpvEventId.EndFile:
                        EndFile?.Invoke((MpvEndFileReason)((MpvNative.MpvEventEndFile*)ev->Data)->Reason);
                        break;
                    case MpvEventId.VideoReconfig:
                        VideoReconfig?.Invoke();
                        break;
                    case MpvEventId.PlaybackRestart:
                        PlaybackRestart?.Invoke();
                        break;
                    case MpvEventId.ClientMessage:
                    {
                        var msg = (MpvNative.MpvEventClientMessage*)ev->Data;
                        var args = new string[msg->NumArgs];
                        var arr = (IntPtr*)msg->Args;
                        for (int i = 0; i < args.Length; i++) args[i] = Marshal.PtrToStringUTF8(arr[i]) ?? "";
                        ClientMessage?.Invoke(args);
                        break;
                    }
                    case MpvEventId.LogMessage:
                    {
                        var log = (MpvNative.MpvEventLogMessage*)ev->Data;
                        LogMessage?.Invoke(Marshal.PtrToStringUTF8(log->Level) ?? "", Marshal.PtrToStringUTF8(log->Prefix) ?? "",
                            (Marshal.PtrToStringUTF8(log->Text) ?? "").TrimEnd('\n'));
                        break;
                    }
                }
            }
            catch (Exception) when (!System.Diagnostics.Debugger.IsAttached)
            {
                // A faulty subscriber must not kill the event thread.
            }
        }
    }

    private void OnProperty(MpvNative.MpvEventProperty* p)
    {
        var handler = PropertyChanged;
        if (handler is null) return;
        string name = Marshal.PtrToStringUTF8(p->Name) ?? "";
        object? value = null;
        if (p->Data != IntPtr.Zero)
        {
            value = (MpvFormat)p->Format switch
            {
                MpvFormat.Double => *(double*)p->Data,
                MpvFormat.Flag => *(int*)p->Data != 0,
                MpvFormat.Int64 => *(long*)p->Data,
                MpvFormat.String or MpvFormat.OsdString => Marshal.PtrToStringUTF8(*(IntPtr*)p->Data),
                _ => null,
            };
        }
        handler(name, value);
    }

    // ---- helpers --------------------------------------------------------------------------

    private static byte[] Utf8(string s)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
        Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
        return bytes;
    }

    private static void Check(int code, string what)
    {
        if (code >= 0) return;
        throw new MpvException(what, code, Marshal.PtrToStringUTF8(MpvNative.mpv_error_string(code)) ?? "error");
    }

    public void Dispose()
    {
        if (_ctx == IntPtr.Zero) return;
        _stopping = true;
        MpvNative.mpv_wakeup(_ctx);
        if (Thread.CurrentThread != _eventThread) _eventThread.Join(TimeSpan.FromSeconds(3));
        var ctx = _ctx;
        _ctx = IntPtr.Zero;
        MpvNative.mpv_terminate_destroy(ctx);
    }
}
