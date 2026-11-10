namespace Mashhad.Core.Media;

/// <summary>Builds the unified image + video gallery for a folder.</summary>
public static class FolderScanner
{
    /// <summary>
    /// Lists the visual media (images + videos) of a folder in gallery order. When
    /// <paramref name="includeAudio"/> is set, audio files are listed too (used when the
    /// file that was opened is itself an audio file).
    /// </summary>
    public static List<MediaItem> Scan(string directory, GallerySort sort = GallerySort.Name,
        bool descending = false, bool includeAudio = false)
    {
        var items = new List<(MediaItem Item, FileInfo? Info)>();
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<MediaItem>();
        }

        bool needInfo = sort != GallerySort.Name;
        foreach (var file in files)
        {
            var kind = MediaTypes.Classify(file);
            if (!(kind.IsVisual() || (includeAudio && kind == MediaKind.Audio))) continue;
            FileInfo? info = null;
            if (needInfo)
            {
                try { info = new FileInfo(file); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            items.Add((new MediaItem(file, kind), info));
        }

        Comparison<(MediaItem Item, FileInfo? Info)> byName =
            (a, b) => NaturalComparer.Instance.Compare(a.Item.FileName, b.Item.FileName);
        Comparison<(MediaItem Item, FileInfo? Info)> cmp = sort switch
        {
            GallerySort.DateModified => (a, b) =>
            {
                int c = (a.Info?.LastWriteTimeUtc ?? default).CompareTo(b.Info?.LastWriteTimeUtc ?? default);
                return c != 0 ? c : byName(a, b);
            },
            GallerySort.Size => (a, b) =>
            {
                int c = (a.Info?.Length ?? 0).CompareTo(b.Info?.Length ?? 0);
                return c != 0 ? c : byName(a, b);
            },
            _ => byName,
        };
        items.Sort(cmp);
        if (descending) items.Reverse();
        return items.ConvertAll(t => t.Item);
    }

    /// <summary>
    /// Gallery for a single opened file: every sibling in its folder, with the index of the
    /// opened file. The file itself is always present even if its extension is unknown.
    /// </summary>
    public static (List<MediaItem> Items, int Index) ScanAround(string filePath, GallerySort sort = GallerySort.Name,
        bool descending = false)
    {
        var full = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(full);
        var self = MediaItem.FromPath(full);
        if (string.IsNullOrEmpty(dir)) return (new List<MediaItem> { self }, 0);

        var items = Scan(dir, sort, descending, includeAudio: self.Kind == MediaKind.Audio);
        int index = items.FindIndex(i => PathEquals(i.Path, full));
        if (index < 0)
        {
            items.Add(self);
            index = items.Count - 1;
        }
        return (items, index);
    }

    public static bool PathEquals(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
