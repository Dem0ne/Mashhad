namespace Mashhad.Core.Media;

/// <summary>
/// Makes the neighbouring gallery items open instantly by pulling them into the OS file cache
/// on a background thread. This is deliberately not a second decoder: it costs no extra RAM in
/// our process (the cache is reclaimable) and removes the disk latency that dominates the time
/// to first frame for local media.
/// </summary>
public sealed class Preloader : IDisposable
{
    /// <summary>Whole-file warm-up limit for images.</summary>
    public long ImageLimitBytes { get; init; } = 96L * 1024 * 1024;
    /// <summary>Head (and tail, where MP4 keeps its index) warm-up for videos.</summary>
    public long VideoHeadBytes { get; init; } = 8L * 1024 * 1024;
    public long VideoTailBytes { get; init; } = 1L * 1024 * 1024;

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private string? _lastKey;

    /// <summary>Raised (on a worker thread) after each file has been warmed; used by tests and diagnostics.</summary>
    public event Action<string, long>? Warmed;

    /// <summary>Starts warming <paramref name="items"/>, cancelling any previous request.</summary>
    public Task Preload(params MediaItem?[] items)
    {
        var list = items.Where(i => i is not null && !i.Path.Contains("://", StringComparison.Ordinal))
            .Select(i => i!).DistinctBy(i => i.Path).ToList();
        var key = string.Join("|", list.Select(i => i.Path));
        CancellationToken token;
        lock (_gate)
        {
            if (key == _lastKey) return Task.CompletedTask;
            _lastKey = key;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            token = _cts.Token;
        }
        if (list.Count == 0) return Task.CompletedTask;
        return Task.Run(() =>
        {
            foreach (var item in list)
            {
                if (token.IsCancellationRequested) return;
                try
                {
                    long n = Warm(item, token);
                    Warmed?.Invoke(item.Path, n);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
                {
                    // Preloading is best-effort.
                }
            }
        }, token);
    }

    internal long Warm(MediaItem item, CancellationToken token)
    {
        using var fs = new FileStream(item.Path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
        long length = fs.Length;
        var buffer = new byte[256 * 1024];
        long total = 0;

        long head = item.Kind.IsImageLike() ? Math.Min(length, ImageLimitBytes) : Math.Min(length, VideoHeadBytes);
        total += ReadRange(fs, buffer, 0, head, token);

        if (!item.Kind.IsImageLike() && length > head)
        {
            long tailStart = Math.Max(head, length - VideoTailBytes);
            total += ReadRange(fs, buffer, tailStart, length - tailStart, token);
        }
        return total;
    }

    private static long ReadRange(FileStream fs, byte[] buffer, long offset, long count, CancellationToken token)
    {
        fs.Position = offset;
        long done = 0;
        while (done < count)
        {
            token.ThrowIfCancellationRequested();
            int n = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, count - done));
            if (n <= 0) break;
            done += n;
        }
        return done;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
