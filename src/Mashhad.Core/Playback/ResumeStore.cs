using System.Text.Json;

namespace Mashhad.Core.Playback;

/// <summary>Remembers where playback stopped, per file. Bounded, persisted as a small JSON file.</summary>
public sealed class ResumeStore
{
    public sealed record Entry(double Position, double Duration, DateTime Updated);

    private readonly string? _filePath;
    private readonly Dictionary<string, Entry> _entries;
    private readonly object _gate = new();

    public int Capacity { get; init; } = 500;
    /// <summary>Files shorter than this are never resumed.</summary>
    public double MinDuration { get; init; } = 60;
    /// <summary>Positions within this many seconds of the start are not worth remembering.</summary>
    public double MinPosition { get; init; } = 5;
    /// <summary>Fraction of the file after which it counts as finished.</summary>
    public double FinishedFraction { get; init; } = 0.95;

    public ResumeStore(string? filePath = null)
    {
        _filePath = filePath;
        _entries = new Dictionary<string, Entry>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Load();
    }

    public int Count { get { lock (_gate) return _entries.Count; } }

    /// <summary>Records a stop position. Returns true when it was stored, false when it was cleared.</summary>
    public bool Update(string path, double position, double duration)
    {
        lock (_gate)
        {
            bool keep = duration >= MinDuration && position >= MinPosition && position < duration * FinishedFraction
                        && !double.IsNaN(position);
            if (!keep)
            {
                _entries.Remove(path);
                return false;
            }
            _entries[path] = new Entry(Math.Round(position, 3), Math.Round(duration, 3), DateTime.UtcNow);
            if (_entries.Count > Capacity)
            {
                foreach (var old in _entries.OrderBy(e => e.Value.Updated).Take(_entries.Count - Capacity).Select(e => e.Key).ToList())
                    _entries.Remove(old);
            }
            return true;
        }
    }

    public double? GetPosition(string path)
    {
        lock (_gate) return _entries.TryGetValue(path, out var e) ? e.Position : null;
    }

    public void Forget(string path) { lock (_gate) _entries.Remove(path); }

    public void Save()
    {
        if (_filePath is null) return;
        string json;
        lock (_gate) json = JsonSerializer.Serialize(_entries);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private void Load()
    {
        if (_filePath is null || !File.Exists(_filePath)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(_filePath));
            if (loaded is null) return;
            foreach (var (k, v) in loaded) _entries[k] = v;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
    }
}
