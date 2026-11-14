using System.Collections.Concurrent;
using System.Globalization;
using Mashhad.Core.Settings;

namespace Mashhad.App;

/// <summary>
/// Append-only diagnostic log in %LOCALAPPDATA%\Mashhad.
///
/// Writing never blocks the caller. This matters: the UI thread is a COM STA, and when it waits on
/// a contended lock the runtime pumps messages; a XAML timer then re-enters the dispatcher and WinUI
/// fail-fasts (0xC000027B, "OnReentrancyProtectedWindowMessage"). libmpv logs from its own thread,
/// so a shared lock here crashed the app at start-up. Lines go into a lock-free queue that a
/// background thread writes out.
/// </summary>
internal static class Log
{
    private static readonly ConcurrentQueue<string> Queue = new();
    private static readonly AutoResetEvent Signal = new(false);
    private static int _pending;
    private static Thread? _writer;

    /// <summary>Overridden by --smoke-log so CI can collect the output.</summary>
    public static string FilePath { get; set; } = AppPaths.LogFile;

    public static void Write(string message)
    {
        Queue.Enqueue(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ", CultureInfo.InvariantCulture) + message + Environment.NewLine);
        Interlocked.Increment(ref _pending);
        EnsureWriter();
        Signal.Set();
    }

    /// <summary>Waits (without pumping messages) until queued lines are on disk, up to <paramref name="timeoutMs"/>.</summary>
    public static void Flush(int timeoutMs = 2000)
    {
        var until = Environment.TickCount64 + timeoutMs;
        while (Volatile.Read(ref _pending) > 0 && Environment.TickCount64 < until)
        {
            Signal.Set();
            Thread.Sleep(5);
        }
    }

    private static void EnsureWriter()
    {
        if (_writer is not null) return;
        var t = new Thread(WriterLoop) { IsBackground = true, Name = "log writer" };
        if (Interlocked.CompareExchange(ref _writer, t, null) is null) t.Start();
    }

    private static void WriterLoop()
    {
        var batch = new System.Text.StringBuilder();
        while (true)
        {
            Signal.WaitOne(500);
            int taken = 0;
            batch.Clear();
            while (Queue.TryDequeue(out var line)) { batch.Append(line); taken++; }
            if (taken == 0) continue;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 2_000_000) info.Delete();
                File.AppendAllText(FilePath, batch.ToString());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            Interlocked.Add(ref _pending, -taken);
        }
    }
}
