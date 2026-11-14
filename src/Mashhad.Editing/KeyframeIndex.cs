using System.Globalization;

namespace Mashhad.Editing;

/// <summary>Sorted keyframe timestamps of the main video stream. Stream copy can only start on these.</summary>
public sealed class KeyframeIndex
{
    public static readonly KeyframeIndex Empty = new(Array.Empty<double>());

    private readonly double[] _times;

    public KeyframeIndex(IEnumerable<double> times)
    {
        _times = times.Where(t => !double.IsNaN(t)).Distinct().OrderBy(t => t).ToArray();
    }

    public IReadOnlyList<double> Times => _times;

    /// <summary>
    /// The same keyframes on a timeline that starts at zero. ffprobe reports absolute timestamps,
    /// while both mpv's position and FFmpeg's <c>-ss</c> are relative to the file's start time.
    /// </summary>
    public KeyframeIndex RelativeTo(double startTime) =>
        Math.Abs(startTime) < 1e-9 ? this : new KeyframeIndex(_times.Select(t => Math.Round(t - startTime, 6)));
    public int Count => _times.Length;
    public bool IsEmpty => _times.Length == 0;

    /// <summary>Last keyframe at or before <paramref name="time"/> (within <paramref name="tolerance"/>).</summary>
    public double? AtOrBefore(double time, double tolerance = 0.0005)
    {
        int i = UpperBound(time + tolerance) - 1;
        return i >= 0 ? _times[i] : null;
    }

    /// <summary>First keyframe at or after <paramref name="time"/>.</summary>
    public double? AtOrAfter(double time, double tolerance = 0.0005)
    {
        int i = LowerBound(time - tolerance);
        return i < _times.Length ? _times[i] : null;
    }

    /// <summary>Keyframe strictly before <paramref name="time"/> (for "previous keyframe" navigation).</summary>
    public double? Before(double time, double tolerance = 0.0005)
    {
        int i = LowerBound(time - tolerance) - 1;
        return i >= 0 ? _times[i] : null;
    }

    /// <summary>Keyframe strictly after <paramref name="time"/>.</summary>
    public double? After(double time, double tolerance = 0.0005)
    {
        int i = UpperBound(time + tolerance);
        return i < _times.Length ? _times[i] : null;
    }

    public double? Nearest(double time)
    {
        var a = AtOrBefore(time);
        var b = AtOrAfter(time);
        if (a is null) return b;
        if (b is null) return a;
        return time - a.Value <= b.Value - time ? a : b;
    }

    public bool IsKeyframe(double time, double tolerance = 0.0005)
    {
        var n = Nearest(time);
        return n is not null && Math.Abs(n.Value - time) <= tolerance;
    }

    private int LowerBound(double value)
    {
        int lo = 0, hi = _times.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_times[mid] < value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private int UpperBound(double value)
    {
        int lo = 0, hi = _times.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_times[mid] <= value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Parses <c>ffprobe -show_entries packet=pts_time,flags -of csv=p=0</c> output:
    /// one "pts_time,flags" line per packet, keyframes carry a 'K' flag.
    /// </summary>
    public static KeyframeIndex ParsePacketCsv(IEnumerable<string> lines)
    {
        var times = new List<double>();
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length < 2 || !parts[1].Contains('K')) continue;
            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) times.Add(t);
        }
        return new KeyframeIndex(times);
    }
}
