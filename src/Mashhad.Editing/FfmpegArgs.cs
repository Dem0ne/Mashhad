using System.Globalization;
using System.Text;
using Mashhad.Core.Imaging;

namespace Mashhad.Editing;

public enum FrameFormat { Png, Jpeg, Webp }

/// <summary>
/// Builds FFmpeg/ffprobe argument lists. Arguments are returned as discrete items (never one
/// shell string) so paths with spaces, quotes or Arabic characters need no escaping.
/// </summary>
public static class FfmpegArgs
{
    private static string T(double seconds) =>
        Math.Max(0, seconds).ToString("0.######", CultureInfo.InvariantCulture);

    private static List<string> Common() => new() { "-hide_banner", "-nostdin", "-y", "-nostats", "-progress", "pipe:1" };

    /// <summary>MP4-family containers benefit from a front-loaded index.</summary>
    public static bool IsMp4Family(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".m4v" or ".mov" or ".m4a" or ".3gp";

    /// <summary>
    /// Lossless cut: stream copy of [start, end). <c>-ss</c> is placed before <c>-i</c>, so FFmpeg
    /// starts at the keyframe at or before <paramref name="start"/> — pass an already snapped
    /// start (see <see cref="ExportPlanner"/>) for a predictable result.
    /// </summary>
    public static List<string> Cut(string input, string output, double start, double? end, bool keepAllStreams = true)
    {
        var a = Common();
        if (start > 0) { a.Add("-ss"); a.Add(T(start)); }
        a.Add("-i"); a.Add(input);
        if (end is double e) { a.Add("-t"); a.Add(T(e - start)); }
        AddCopyMapping(a, output, keepAllStreams);
        a.Add("-avoid_negative_ts"); a.Add("make_zero");
        a.Add(output);
        return a;
    }

    /// <summary>Lossless merge with the concat demuxer; <paramref name="listFile"/> comes from <see cref="ConcatList"/>.</summary>
    public static List<string> Concat(string listFile, string output, bool keepAllStreams = true)
    {
        var a = Common();
        a.AddRange(new[] { "-f", "concat", "-safe", "0", "-i", listFile });
        AddCopyMapping(a, output, keepAllStreams);
        a.Add(output);
        return a;
    }

    /// <summary>Change container without re-encoding.</summary>
    public static List<string> Remux(string input, string output, bool keepAllStreams = true)
    {
        var a = Common();
        a.Add("-i"); a.Add(input);
        AddCopyMapping(a, output, keepAllStreams);
        a.Add(output);
        return a;
    }

    private static void AddCopyMapping(List<string> a, string output, bool keepAllStreams)
    {
        bool mp4 = IsMp4Family(output);
        if (keepAllStreams && !mp4)
        {
            a.Add("-map"); a.Add("0");
        }
        else
        {
            // MP4 cannot hold arbitrary data/attachment streams: keep video, audio and subtitles.
            a.AddRange(new[] { "-map", "0:v?", "-map", "0:a?", "-map", "0:s?" });
        }
        a.Add("-c"); a.Add("copy");
        if (mp4)
        {
            // Text subtitles must be converted for MP4; this does not touch audio or video.
            a.Add("-c:s"); a.Add("mov_text");
            a.Add("-movflags"); a.Add("+faststart");
        }
        a.Add("-map_metadata"); a.Add("0");
        a.Add("-map_chapters"); a.Add(keepAllStreams ? "0" : "-1");
        a.Add("-ignore_unknown");
    }

    /// <summary>Contents of a concat-demuxer list file.</summary>
    public static string ConcatList(IEnumerable<string> files)
    {
        var sb = new StringBuilder("ffconcat version 1.0\n");
        foreach (var f in files)
        {
            // Forward slashes work on Windows and avoid backslash escaping; quotes close-escape-reopen.
            var p = f.Replace('\\', '/').Replace("'", "'\\''");
            sb.Append("file '").Append(p).Append("'\n");
        }
        return sb.ToString();
    }

    /// <summary>One full-quality frame at <paramref name="time"/>.</summary>
    public static List<string> ExtractFrame(string input, string output, double time, FrameFormat format)
    {
        var a = Common();
        a.Add("-ss"); a.Add(T(time));
        a.Add("-i"); a.Add(input);
        a.AddRange(new[] { "-map", "0:v:0", "-frames:v", "1" });
        AddImageEncoding(a, format);
        a.Add(output);
        return a;
    }

    /// <summary>A small JPEG of the frame at <paramref name="time"/> for the editor's thumbnail lane (fast keyframe seek).</summary>
    public static List<string> Thumbnail(string input, string output, double time, int height)
    {
        var a = new List<string> { "-hide_banner", "-nostdin", "-y", "-v", "error" };
        a.Add("-ss"); a.Add(T(time));
        a.Add("-i"); a.Add(input);
        a.AddRange(new[] { "-map", "0:v:0", "-frames:v", "1", "-an", "-sn",
            "-vf", "scale=-2:" + height.ToString(CultureInfo.InvariantCulture), "-c:v", "mjpeg", "-q:v", "5", "-update", "1", output });
        return a;
    }

    /// <summary>Every frame of [start, end) as numbered images; <paramref name="outputPattern"/> contains e.g. %05d.</summary>
    public static List<string> ExtractFrames(string input, string outputPattern, double start, double? end, FrameFormat format)
    {
        var a = Common();
        if (start > 0) { a.Add("-ss"); a.Add(T(start)); }
        a.Add("-i"); a.Add(input);
        if (end is double e) { a.Add("-t"); a.Add(T(e - start)); }
        a.AddRange(new[] { "-map", "0:v:0", "-fps_mode", "passthrough" });
        AddImageEncoding(a, format, single: false);
        a.Add(outputPattern);
        return a;
    }

    /// <summary>
    /// Rotate (clockwise, multiples of 90°) and/or crop a still image. The crop rectangle is in
    /// the rotated orientation. Pixels are re-encoded; PNG/WebP output is lossless, JPEG is not.
    /// </summary>
    public static List<string> TransformImage(string input, string output, int rotation, PixelRect? crop, FrameFormat format)
    {
        var filters = new List<string>();
        switch (CropMath.NormalizeRotation(rotation))
        {
            case 90: filters.Add("transpose=clock"); break;
            case 180: filters.Add("hflip"); filters.Add("vflip"); break;
            case 270: filters.Add("transpose=cclock"); break;
        }
        if (crop is { IsEmpty: false } c)
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"crop={c.Width}:{c.Height}:{c.X}:{c.Y}"));

        var a = Common();
        a.Add("-i"); a.Add(input);
        a.AddRange(new[] { "-map", "0:v:0", "-frames:v", "1" });
        if (filters.Count > 0) { a.Add("-vf"); a.Add(string.Join(",", filters)); }
        AddImageEncoding(a, format);
        a.Add(output);
        return a;
    }

    private static void AddImageEncoding(List<string> a, FrameFormat format, bool single = true)
    {
        switch (format)
        {
            case FrameFormat.Jpeg:
                a.AddRange(new[] { "-c:v", "mjpeg", "-q:v", "2" });
                break;
            case FrameFormat.Webp:
                a.AddRange(new[] { "-c:v", "libwebp", "-lossless", "1" });
                break;
            default:
                a.AddRange(new[] { "-c:v", "png" });
                break;
        }
        if (single) { a.Add("-update"); a.Add("1"); }
    }

    public static FrameFormat FormatForExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" => FrameFormat.Jpeg,
        ".webp" => FrameFormat.Webp,
        _ => FrameFormat.Png,
    };

    public static string ExtensionFor(FrameFormat format) => format switch
    {
        FrameFormat.Jpeg => ".jpg",
        FrameFormat.Webp => ".webp",
        _ => ".png",
    };

    // ---- ffprobe --------------------------------------------------------------------------

    public static List<string> Probe(string input) => new()
    {
        "-v", "error", "-show_format", "-show_streams", "-of", "json", input,
    };

    /// <summary>Lists packets of the main video stream with their flags (no decoding, so it is fast).</summary>
    public static List<string> ProbeKeyframes(string input, int videoStreamIndex = -1) => new()
    {
        "-v", "error", "-select_streams", videoStreamIndex >= 0 ? videoStreamIndex.ToString(CultureInfo.InvariantCulture) : "v:0",
        "-show_entries", "packet=pts_time,flags", "-of", "csv=p=0", input,
    };

    /// <summary>Human-readable command line for logs and the "show command" UI.</summary>
    public static string Display(string exe, IEnumerable<string> args)
    {
        static string Q(string s) => s.Length > 0 && s.All(ch => !char.IsWhiteSpace(ch) && ch != '"') ? s : "\"" + s.Replace("\"", "\\\"") + "\"";
        return string.Join(' ', new[] { Q(exe) }.Concat(args.Select(Q)));
    }
}
