namespace Mashhad.Editing;

/// <summary>A span of the source to keep. Times are seconds; a null end means "to the end of the file".</summary>
public sealed record Segment(double Start, double? End = null, string? Name = null)
{
    public double EndOr(double duration) => End ?? duration;
    public double Length(double duration) => Math.Max(0, EndOr(duration) - Start);
    public bool Contains(double t, double duration) => t >= Start && t <= EndOr(duration);
}

/// <summary>
/// The LosslessCut-style segment list: a set of in/out spans with one "current" segment that
/// the in/out commands act on.
/// </summary>
public sealed class SegmentList
{
    /// <summary>Segments shorter than this are rejected.</summary>
    public const double MinLength = 0.001;

    private readonly List<Segment> _segments = new();

    public IReadOnlyList<Segment> Segments => _segments;
    public int CurrentIndex { get; private set; } = -1;
    public Segment? Current => CurrentIndex >= 0 && CurrentIndex < _segments.Count ? _segments[CurrentIndex] : null;
    public int Count => _segments.Count;
    /// <summary>Source duration in seconds (0 when unknown).</summary>
    public double Duration { get; set; }

    public event Action? Changed;

    public void Clear()
    {
        _segments.Clear();
        CurrentIndex = -1;
        Changed?.Invoke();
    }

    public void Load(IEnumerable<Segment> segments)
    {
        _segments.Clear();
        _segments.AddRange(segments.Where(IsValid));
        CurrentIndex = _segments.Count > 0 ? 0 : -1;
        Changed?.Invoke();
    }

    public bool Select(int index)
    {
        if (index < 0 || index >= _segments.Count) return false;
        CurrentIndex = index;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Sets the in point of the current segment. With no segment yet, starts one that runs to the
    /// end of the file. If the new start is past the current end, the end is released.
    /// </summary>
    public void SetStart(double time)
    {
        time = ClampTime(time);
        if (Current is null)
        {
            _segments.Add(new Segment(time));
            CurrentIndex = _segments.Count - 1;
        }
        else
        {
            var cur = Current;
            double? end = cur.End is double e && e - time < MinLength ? null : cur.End;
            _segments[CurrentIndex] = cur with { Start = time, End = end };
        }
        Changed?.Invoke();
    }

    /// <summary>Sets the out point of the current segment. If it is before the start, the start moves to 0.</summary>
    public void SetEnd(double time)
    {
        time = ClampTime(time);
        if (Current is null)
        {
            if (time < MinLength) return;
            _segments.Add(new Segment(0, time));
            CurrentIndex = _segments.Count - 1;
        }
        else
        {
            var cur = Current;
            double start = time - cur.Start < MinLength ? 0 : cur.Start;
            if (time - start < MinLength) return;
            _segments[CurrentIndex] = cur with { Start = start, End = time };
        }
        Changed?.Invoke();
    }

    /// <summary>Starts a new segment at <paramref name="time"/> (open-ended) and makes it current.</summary>
    public void AddAt(double time)
    {
        time = ClampTime(time);
        if (Duration > 0 && Duration - time < MinLength) return;
        _segments.Add(new Segment(time));
        CurrentIndex = _segments.Count - 1;
        Changed?.Invoke();
    }

    /// <summary>Splits the segment under <paramref name="time"/> in two. Returns false if no segment contains it.</summary>
    public bool SplitAt(double time)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            double end = s.EndOr(Duration > 0 ? Duration : double.MaxValue);
            if (time - s.Start < MinLength || end - time < MinLength) continue;
            _segments[i] = s with { End = time };
            _segments.Insert(i + 1, s with { Start = time, Name = null });
            CurrentIndex = i + 1;
            Changed?.Invoke();
            return true;
        }
        return false;
    }

    public bool RemoveCurrent() => RemoveAt(CurrentIndex);

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _segments.Count) return false;
        _segments.RemoveAt(index);
        if (CurrentIndex >= _segments.Count) CurrentIndex = _segments.Count - 1;
        else if (index < CurrentIndex) CurrentIndex--;
        Changed?.Invoke();
        return true;
    }

    public void Rename(int index, string? name)
    {
        if (index < 0 || index >= _segments.Count) return;
        _segments[index] = _segments[index] with { Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim() };
        Changed?.Invoke();
    }

    /// <summary>Segments in timeline order with open ends resolved, ready for export.</summary>
    public List<Segment> Resolved()
    {
        double duration = Duration > 0 ? Duration : double.MaxValue;
        return _segments
            .Select(s => s with { End = s.End ?? (Duration > 0 ? Duration : null) })
            .Where(s => s.EndOr(duration) - s.Start >= MinLength)
            .OrderBy(s => s.Start).ThenBy(s => s.EndOr(duration))
            .ToList();
    }

    /// <summary>
    /// Replaces the segments by the gaps between them (cut the marked parts <em>out</em>).
    /// Requires a known duration.
    /// </summary>
    public bool Invert()
    {
        if (Duration <= 0 || _segments.Count == 0) return false;
        var kept = new List<Segment>();
        double cursor = 0;
        foreach (var s in Resolved())
        {
            if (s.Start - cursor >= MinLength) kept.Add(new Segment(cursor, s.Start));
            cursor = Math.Max(cursor, s.EndOr(Duration));
        }
        if (Duration - cursor >= MinLength) kept.Add(new Segment(cursor, Duration));
        _segments.Clear();
        _segments.AddRange(kept);
        CurrentIndex = _segments.Count > 0 ? 0 : -1;
        Changed?.Invoke();
        return true;
    }

    private double ClampTime(double t)
    {
        if (double.IsNaN(t) || t < 0) t = 0;
        return Duration > 0 ? Math.Min(t, Duration) : t;
    }

    private static bool IsValid(Segment s) => s.Start >= 0 && (s.End is null || s.End - s.Start >= MinLength);
}
