namespace Mashhad.Core.Media;

public enum MediaKind
{
    Unknown,
    Image,
    AnimatedImage,
    Video,
    Audio,
}

/// <summary>Classifies files by extension. Kept deliberately cheap: no file I/O.</summary>
public static class MediaTypes
{
    public static readonly IReadOnlySet<string> ImageExtensions = Set(
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".webp", ".bmp", ".tif", ".tiff",
        ".heic", ".heif", ".avif", ".jxl", ".tga", ".ico", ".dds", ".qoi");

    public static readonly IReadOnlySet<string> AnimatedImageExtensions = Set(".gif", ".apng");

    public static readonly IReadOnlySet<string> VideoExtensions = Set(
        ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".wmv", ".flv", ".f4v", ".ts", ".mts", ".m2ts",
        ".mpg", ".mpeg", ".mpe", ".m2v", ".vob", ".3gp", ".3g2", ".ogv", ".ogm", ".rm", ".rmvb", ".asf",
        ".divx", ".mxf", ".y4m", ".nut", ".dv");

    public static readonly IReadOnlySet<string> AudioExtensions = Set(
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wav", ".wma", ".ac3", ".eac3",
        ".dts", ".mka", ".aiff", ".aif", ".ape", ".wv", ".tta", ".amr");

    public static readonly IReadOnlySet<string> PlaylistExtensions = Set(".m3u", ".m3u8");

    public static readonly IReadOnlySet<string> SubtitleExtensions = Set(
        ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup", ".smi", ".lrc");

    public static MediaKind Classify(string? path)
    {
        if (string.IsNullOrEmpty(path)) return MediaKind.Unknown;
        var ext = Path.GetExtension(path);
        if (ext.Length == 0) return MediaKind.Unknown;
        if (ImageExtensions.Contains(ext)) return MediaKind.Image;
        if (AnimatedImageExtensions.Contains(ext)) return MediaKind.AnimatedImage;
        if (VideoExtensions.Contains(ext)) return MediaKind.Video;
        if (AudioExtensions.Contains(ext)) return MediaKind.Audio;
        return MediaKind.Unknown;
    }

    public static bool IsMedia(string? path) => Classify(path) != MediaKind.Unknown;
    public static bool IsPlaylist(string? path) => path is not null && PlaylistExtensions.Contains(Path.GetExtension(path));
    public static bool IsSubtitle(string? path) => path is not null && SubtitleExtensions.Contains(Path.GetExtension(path));

    /// <summary>Still or animated image (anything shown without a seek bar by default).</summary>
    public static bool IsImageLike(this MediaKind kind) => kind is MediaKind.Image or MediaKind.AnimatedImage;

    /// <summary>Has a timeline: video or audio.</summary>
    public static bool IsTimed(this MediaKind kind) => kind is MediaKind.Video or MediaKind.Audio;

    /// <summary>Kinds shown in the unified folder gallery (audio is playable but not part of the gallery).</summary>
    public static bool IsVisual(this MediaKind kind) => kind is MediaKind.Image or MediaKind.AnimatedImage or MediaKind.Video;

    private static HashSet<string> Set(params string[] items) => new(items, StringComparer.OrdinalIgnoreCase);
}
