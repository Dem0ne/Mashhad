using Mashhad.Editing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Mashhad.App;

/// <summary>
/// Shell thumbnails for the folder view (photos and videos alike, from the Windows thumbnail
/// cache) plus video durations. Loads at most four at a time and keeps the most recent few hundred.
/// </summary>
internal sealed class ThumbnailCache
{
    public sealed record Info(ImageSource? Image, double? Aspect, TimeSpan? Duration);

    private readonly DispatcherQueue _ui;
    private readonly SemaphoreSlim _gate = new(4);
    private readonly Dictionary<string, Info> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();
    private const int Capacity = 600;

    public ThumbnailCache(DispatcherQueue ui) => _ui = ui;

    public bool TryGet(string path, out Info info) => _cache.TryGetValue(path, out info!);

    /// <summary>FFmpeg for video thumbnails when the shell has none (no thumbnail handler installed).</summary>
    public Func<ToolPaths?>? Tools { get; set; }

    /// <summary>Loads (or returns cached) thumbnail info; <paramref name="done"/> runs on the UI thread.</summary>
    public async void Request(string path, bool isVideo, uint size, Action<Info> done)
    {
        if (_cache.TryGetValue(path, out var hit)) { done(hit); return; }
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (_cache.TryGetValue(path, out hit)) { done(hit); return; }
            ImageSource? image = null;
            double? aspect = null;
            TimeSpan? duration = null;

            // 1. The Windows thumbnail cache (fast, shared with Explorer).
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using (var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, size, ThumbnailOptions.ResizeThumbnail))
                {
                    if (thumb is not null && thumb.Type == ThumbnailType.Image)
                    {
                        var bmp = new BitmapImage();
                        await bmp.SetSourceAsync(thumb);
                        image = bmp;
                        if (thumb.OriginalWidth > 0 && thumb.OriginalHeight > 0) aspect = (double)thumb.OriginalWidth / thumb.OriginalHeight;
                    }
                }
                if (isVideo)
                {
                    try { duration = (await file.Properties.GetVideoPropertiesAsync()).Duration; } catch (Exception) { }
                    if (duration == TimeSpan.Zero) duration = null;
                }
            }
            catch (Exception e) { Log.Write($"Shell thumbnail unavailable for {Path.GetFileName(path)}: {e.GetType().Name}"); }

            // 2. Our own: decode the photo at tile size, or grab a video frame with FFmpeg.
            if (image is null && !isVideo)
            {
                image = new BitmapImage { DecodePixelHeight = (int)size, UriSource = new Uri(path) };
            }
            else if ((image is null || duration is null) && isVideo && Tools?.Invoke() is { } tools)
            {
                var runner = new FfmpegRunner(tools);
                try
                {
                    var probe = await Task.Run(() => runner.ProbeAsync(path));
                    if (probe.Duration > 0) duration ??= TimeSpan.FromSeconds(probe.Duration);
                    if (probe.MainVideo is { Width: > 0, Height: > 0 } v) aspect ??= (double)v.Width / v.Height;
                    if (image is null && probe.HasVideo)
                    {
                        var jpg = Path.Combine(CacheDirectory, StableKey(path) + $"-g{size}.jpg");
                        double at = probe.Duration > 0 ? Math.Min(probe.Duration * 0.1, 5) : 0;
                        if (!File.Exists(jpg))
                            await Task.Run(() => runner.RunAsync(FfmpegArgs.Thumbnail(path, jpg, at, (int)size)));
                        if (File.Exists(jpg)) image = new BitmapImage(new Uri(jpg));
                    }
                }
                catch (Exception e) { Log.Write($"Video thumbnail failed for {Path.GetFileName(path)}: {e.Message}"); }
            }

            var info = new Info(image, aspect, duration);
            Store(path, info);
            done(info);
        }
        catch (Exception e)
        {
            Log.Write($"Thumbnail failed for {Path.GetFileName(path)}: {e.GetType().Name} {e.Message}");
            var info = new Info(null, null, null);
            Store(path, info);
            done(info);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static string CacheDirectory
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "Mashhad", "thumbs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>A key that stays the same across runs while the file is unchanged.</summary>
    public static string StableKey(string path)
    {
        var info = new FileInfo(path);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()));
        return $"{Convert.ToHexString(hash, 0, 8).ToLowerInvariant()}-{(info.Exists ? info.Length : 0):x}-{(info.Exists ? info.LastWriteTimeUtc.Ticks : 0):x}";
    }

    private void Store(string path, Info info)
    {
        if (!_cache.ContainsKey(path)) _lru.AddFirst(path);
        _cache[path] = info;
        while (_lru.Count > Capacity)
        {
            _cache.Remove(_lru.Last!.Value);
            _lru.RemoveLast();
        }
    }
}
