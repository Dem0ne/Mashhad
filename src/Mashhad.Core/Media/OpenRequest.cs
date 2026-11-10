namespace Mashhad.Core.Media;

/// <summary>Turns "the user opened these paths" into a queue (gallery or playlist) plus subtitles to attach.</summary>
public sealed record OpenRequest(List<MediaItem> Items, int Index, QueueSource Source, List<string> Subtitles)
{
    public static OpenRequest Resolve(IReadOnlyList<string> paths, GallerySort sort = GallerySort.Name, bool descending = false)
    {
        var subtitles = paths.Where(p => MediaTypes.IsSubtitle(p) && File.Exists(p)).ToList();
        var inputs = paths.Where(p => !MediaTypes.IsSubtitle(p)).ToList();
        var empty = new List<MediaItem>();

        if (inputs.Count == 0) return new OpenRequest(empty, -1, QueueSource.Folder, subtitles);

        if (inputs.Count == 1)
        {
            var single = inputs[0];
            if (Directory.Exists(single))
            {
                var items = FolderScanner.Scan(single, sort, descending);
                return new OpenRequest(items, items.Count > 0 ? 0 : -1, QueueSource.Folder, subtitles);
            }
            if (MediaTypes.IsPlaylist(single) && File.Exists(single))
            {
                var items = PlaylistFile.Read(single);
                return new OpenRequest(items, items.Count > 0 ? 0 : -1, QueueSource.Playlist, subtitles);
            }
            if (single.Contains("://", StringComparison.Ordinal))
                return new OpenRequest(new List<MediaItem> { new(single, MediaKind.Video) }, 0, QueueSource.Playlist, subtitles);
            if (!File.Exists(single)) return new OpenRequest(empty, -1, QueueSource.Folder, subtitles);

            var (siblings, index) = FolderScanner.ScanAround(single, sort, descending);
            return new OpenRequest(siblings, index, QueueSource.Folder, subtitles);
        }

        // Several inputs: an explicit playlist in the given order (folders and playlists expanded).
        var list = new List<MediaItem>();
        foreach (var p in inputs)
        {
            if (Directory.Exists(p)) list.AddRange(FolderScanner.Scan(p, sort, descending));
            else if (MediaTypes.IsPlaylist(p) && File.Exists(p)) list.AddRange(PlaylistFile.Read(p));
            else if (File.Exists(p) && MediaTypes.IsMedia(p)) list.Add(MediaItem.FromPath(Path.GetFullPath(p)));
        }
        return new OpenRequest(list, list.Count > 0 ? 0 : -1, QueueSource.Playlist, subtitles);
    }
}
