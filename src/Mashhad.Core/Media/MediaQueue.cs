namespace Mashhad.Core.Media;

public enum QueueSource
{
    /// <summary>Siblings of the opened file (the unified folder gallery).</summary>
    Folder,
    /// <summary>An explicit list: several files opened/dropped, or an .m3u playlist.</summary>
    Playlist,
}

/// <summary>
/// The ordered list of items being browsed plus the current position. Serves both as the
/// folder gallery and as the playlist; navigation rules (wrap, shuffle) live here so they can
/// be unit-tested without a player.
/// </summary>
public sealed class MediaQueue
{
    private readonly List<MediaItem> _items = new();
    private readonly Random _random;
    private List<int>? _shuffleOrder;
    private int _shufflePos;

    public MediaQueue(int? seed = null) => _random = seed is null ? new Random() : new Random(seed.Value);

    public IReadOnlyList<MediaItem> Items => _items;
    public int CurrentIndex { get; private set; } = -1;
    public QueueSource Source { get; private set; } = QueueSource.Folder;
    public bool Wrap { get; set; } = true;
    public int Count => _items.Count;
    public MediaItem? Current => CurrentIndex >= 0 && CurrentIndex < _items.Count ? _items[CurrentIndex] : null;

    public bool Shuffle
    {
        get => _shuffleOrder is not null;
        set
        {
            if (value == Shuffle) return;
            if (value) BuildShuffle();
            else _shuffleOrder = null;
        }
    }

    public event Action? Changed;

    public void Load(IEnumerable<MediaItem> items, int index, QueueSource source)
    {
        _items.Clear();
        _items.AddRange(items);
        Source = source;
        CurrentIndex = _items.Count == 0 ? -1 : Math.Clamp(index, 0, _items.Count - 1);
        if (Shuffle) BuildShuffle();
        Changed?.Invoke();
    }

    public void Clear() => Load(Array.Empty<MediaItem>(), -1, QueueSource.Folder);

    public void Append(IEnumerable<MediaItem> items)
    {
        _items.AddRange(items);
        if (CurrentIndex < 0 && _items.Count > 0) CurrentIndex = 0;
        Source = QueueSource.Playlist;
        if (Shuffle) BuildShuffle();
        Changed?.Invoke();
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        _items.RemoveAt(index);
        if (index < CurrentIndex) CurrentIndex--;
        else if (CurrentIndex >= _items.Count) CurrentIndex = _items.Count - 1;
        if (Shuffle) BuildShuffle();
        Changed?.Invoke();
        return true;
    }

    public MediaItem? MoveTo(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        CurrentIndex = index;
        if (_shuffleOrder is not null) _shufflePos = _shuffleOrder.IndexOf(index);
        return Current;
    }

    /// <summary>Index reached by stepping <paramref name="delta"/> items (±1), or -1 at a hard end.</summary>
    public int PeekIndex(int delta)
    {
        if (_items.Count == 0 || CurrentIndex < 0) return -1;
        if (delta == 0) return CurrentIndex;
        int step = Math.Sign(delta);

        if (_shuffleOrder is not null)
        {
            int pos = _shufflePos + step;
            if (pos < 0 || pos >= _shuffleOrder.Count)
            {
                if (!Wrap) return -1;
                pos = (pos % _shuffleOrder.Count + _shuffleOrder.Count) % _shuffleOrder.Count;
            }
            return _shuffleOrder[pos];
        }

        int next = CurrentIndex + step;
        if (next < 0 || next >= _items.Count)
        {
            if (!Wrap) return -1;
            next = (next % _items.Count + _items.Count) % _items.Count;
        }
        return next;
    }

    public MediaItem? Peek(int delta)
    {
        int i = PeekIndex(delta);
        return i < 0 ? null : _items[i];
    }

    public MediaItem? Next() => Step(1);
    public MediaItem? Previous() => Step(-1);
    public MediaItem? First() => MoveTo(0);
    public MediaItem? Last() => MoveTo(_items.Count - 1);

    private MediaItem? Step(int delta)
    {
        int i = PeekIndex(delta);
        if (i < 0 || (i == CurrentIndex && _items.Count > 1)) return null;
        if (_items.Count == 1) return null;
        return MoveTo(i);
    }

    private void BuildShuffle()
    {
        var order = Enumerable.Range(0, _items.Count).ToList();
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        // Keep the current item first so enabling shuffle does not jump away from it.
        if (CurrentIndex >= 0)
        {
            int at = order.IndexOf(CurrentIndex);
            if (at > 0) (order[0], order[at]) = (order[at], order[0]);
        }
        _shuffleOrder = order;
        _shufflePos = 0;
    }
}
