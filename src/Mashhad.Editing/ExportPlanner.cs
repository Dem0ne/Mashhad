using Mashhad.Core.Playback;

namespace Mashhad.Editing;

public sealed record ExportOptions
{
    /// <summary>Null/empty = next to the source file.</summary>
    public string? OutputDirectory { get; init; }
    /// <summary>Concatenate the cut segments into one file.</summary>
    public bool Merge { get; init; }
    /// <summary>Move every in point back to the previous keyframe so the cut is exact and glitch-free.</summary>
    public bool SnapToKeyframes { get; init; } = true;
    /// <summary>Output extension including the dot; null = same as the source.</summary>
    public string? Extension { get; init; }
    public bool KeepAllStreams { get; init; } = true;
    /// <summary>Keep the individual segment files after a merge.</summary>
    public bool KeepSegmentFiles { get; init; }
    /// <summary>ffprobe's format_name for the source (e.g. "mpegts"); decides how FFmpeg seeks. Null = guess from the extension.</summary>
    public string? SourceFormat { get; init; }
}

public sealed record ExportStep(string Description, List<string> Arguments, string Output, double Duration);

public sealed record ExportPlan(string Source, List<ExportStep> Steps, List<Segment> EffectiveSegments,
    string? ConcatListPath, string? ConcatListContent, List<string> TemporaryFiles, List<string> Outputs);

/// <summary>Turns a segment list into the FFmpeg invocations that realise it, without running anything.</summary>
public static class ExportPlanner
{
    /// <summary>
    /// How far past a keyframe we ask FFmpeg to seek. For index-based containers (MP4, MOV,
    /// Matroska, WebM, …) a stream copy starts at the keyframe at or before the seek target, but
    /// for those that seek by DTS FFmpeg first moves the target back by 3/23 s when the video has
    /// frame reordering. Aiming this far past the wanted keyframe lands on it in both cases.
    /// (Measured against FFmpeg 6.1; covered by the integration tests.)
    /// </summary>
    internal const double SeekMargin = 3.0 / 23 + 0.001;

    /// <summary>
    /// MPEG transport/program streams have no index: FFmpeg seeks by position and the copy starts
    /// at the first keyframe <em>after</em> the target, so there we must aim exactly at the keyframe.
    /// </summary>
    internal static bool SeeksForward(string? formatName, string path)
    {
        if (!string.IsNullOrEmpty(formatName))
            return formatName.Split(',').Any(f => f is "mpegts" or "mpeg" or "mpegtsraw" or "mpegvideo");
        return Path.GetExtension(path).ToLowerInvariant() is ".ts" or ".mts" or ".m2ts" or ".mpg" or ".mpeg" or ".vob" or ".m2v";
    }

    /// <summary>The <c>-ss</c> value that makes a stream copy begin exactly at <paramref name="keyframe"/>.</summary>
    internal static double SeekTargetFor(double keyframe, KeyframeIndex? keyframes, bool seeksForward = false)
    {
        if (seeksForward) return keyframe;
        double margin = SeekMargin;
        // Never aim past the middle of the GOP (matters for intra-only codecs where every frame is a keyframe).
        if (keyframes?.After(keyframe) is double next) margin = Math.Min(margin, (next - keyframe) / 2);
        return keyframe + margin;
    }

    public static ExportPlan Plan(string source, IReadOnlyList<Segment> segments, double duration,
        KeyframeIndex? keyframes, ExportOptions options, Func<string, bool>? fileExists = null)
    {
        if (segments.Count == 0) throw new InvalidOperationException("No segments to export.");
        fileExists ??= File.Exists;

        var sourceFull = Path.GetFullPath(source);
        var dir = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? Path.GetDirectoryName(sourceFull) ?? "."
            : Path.GetFullPath(options.OutputDirectory!);
        var baseName = Path.GetFileNameWithoutExtension(sourceFull);
        var ext = string.IsNullOrWhiteSpace(options.Extension) ? Path.GetExtension(sourceFull) : options.Extension!;
        if (!ext.StartsWith('.')) ext = "." + ext;

        bool forward = SeeksForward(options.SourceFormat, sourceFull);
        var effective = new List<Segment>();
        var steps = new List<ExportStep>();
        var outputs = new List<string>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sourceFull };

        foreach (var seg in segments)
        {
            double start = seg.Start;
            if (options.SnapToKeyframes && keyframes is { IsEmpty: false })
                start = keyframes.AtOrBefore(start) ?? start;
            double? end = seg.End;
            if (end is double e0 && duration > 0 && e0 >= duration - 0.0005) end = null; // run to the real end
            var eff = seg with { Start = start, End = end };
            effective.Add(eff);

            double segEnd = end ?? duration;
            string label = string.IsNullOrWhiteSpace(seg.Name)
                ? $"{TimeFormat.FileStamp(start)}-{TimeFormat.FileStamp(segEnd)}"
                : Sanitize(seg.Name!);
            string output = Unique(Path.Combine(dir, $"{baseName}-{label}{ext}"), taken, fileExists);

            bool onKeyframe = keyframes is { IsEmpty: false } && keyframes.IsKeyframe(start);
            double seek = start > 0 && onKeyframe ? SeekTargetFor(start, keyframes, forward) : start;
            steps.Add(new ExportStep($"cut {TimeFormat.Clock(start, true)} – {TimeFormat.Clock(segEnd, true)}",
                FfmpegArgs.Cut(sourceFull, output, seek, end, options.KeepAllStreams), output,
                Math.Max(0, segEnd - start)));
            outputs.Add(output);
        }

        string? listPath = null, listContent = null;
        var temp = new List<string>();
        if (options.Merge && steps.Count > 1)
        {
            string merged = Unique(Path.Combine(dir, $"{baseName}-merged{ext}"), taken, fileExists);
            listPath = Path.Combine(dir, $".{baseName}-concat-{Guid.NewGuid():N}.txt");
            listContent = FfmpegArgs.ConcatList(outputs);
            temp.Add(listPath);
            if (!options.KeepSegmentFiles) temp.AddRange(outputs);
            steps.Add(new ExportStep("merge", FfmpegArgs.Concat(listPath, merged, options.KeepAllStreams), merged,
                steps.Sum(s => s.Duration)));
            outputs = options.KeepSegmentFiles ? outputs.Append(merged).ToList() : new List<string> { merged };
        }

        return new ExportPlan(sourceFull, steps, effective, listPath, listContent, temp, outputs);
    }

    /// <summary>Output path for a derived file ("name-suffix.ext") that does not clobber anything.</summary>
    public static string DerivedPath(string source, string suffix, string? extension = null, string? directory = null,
        Func<string, bool>? fileExists = null)
    {
        var full = Path.GetFullPath(source);
        var dir = string.IsNullOrWhiteSpace(directory) ? Path.GetDirectoryName(full) ?? "." : Path.GetFullPath(directory!);
        var ext = extension ?? Path.GetExtension(full);
        return Unique(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(full)}-{suffix}{ext}"),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { full }, fileExists ?? File.Exists);
    }

    private static string Unique(string path, HashSet<string> taken, Func<string, bool> exists)
    {
        string candidate = path;
        string stem = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
        string ext = Path.GetExtension(path);
        for (int n = 2; taken.Contains(candidate) || exists(candidate); n++)
            candidate = $"{stem} ({n}){ext}";
        taken.Add(candidate);
        return candidate;
    }

    internal static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var clean = new string(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim(' ', '.');
        return clean.Length == 0 ? "segment" : clean;
    }
}
