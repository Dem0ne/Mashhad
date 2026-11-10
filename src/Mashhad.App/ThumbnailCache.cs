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

    /// <summary>Loads (or returns cached) thumbnail info; <paramref name="done"/> runs on the UI thread.</summary>
    public async void Request(string path, bool isVideo, uint size, Action<Info> done)
    {
        if (_cache.TryGetValue(path, out var hit)) { done(hit); return; }
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (_cache.TryGetValue(path, out hit)) { done(hit); return; }
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, size, ThumbnailOptions.ResizeThumbnail);
            ImageSource? image = null;
            double? aspect = null;
            if (thumb is not null && thumb.Type == ThumbnailType.Image)
            {
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(thumb);
                image = bmp;
                if (thumb.OriginalWidth > 0 && thumb.OriginalHeight > 0) aspect = (double)thumb.OriginalWidth / thumb.OriginalHeight;
            }
            TimeSpan? duration = null;
            if (isVideo)
            {
                try { duration = (await file.Properties.GetVideoPropertiesAsync()).Duration; }
                catch (Exception) { }
                if (duration == TimeSpan.Zero) duration = null;
            }
            var info = new Info(image, aspect, duration);
            Store(path, info);
            done(info);
        }
        catch (Exception e)
        {
            Log.Write($"Thumbnail failed for {Path.GetFileName(path)}: {e.Message}");
            var info = new Info(null, null, null);
            Store(path, info);
            done(info);
        }
        finally
        {
            _gate.Release();
        }
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
