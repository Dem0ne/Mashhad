using System.Globalization;

namespace Mashhad.Core.Media;

public enum GalleryFilter { All, Photos, Videos }

/// <summary>One file in the folder view.</summary>
public sealed class GalleryEntry
{
    public GalleryEntry(MediaItem item, DateTime modified, long size)
    {
        Item = item;
        Modified = modified;
        Size = size;
        Aspect = item.Kind == MediaKind.Video ? 16.0 / 9 : 1.5;
    }

    public MediaItem Item { get; }
    public DateTime Modified { get; }
    public long Size { get; }
    /// <summary>Width / height as displayed; refined when the header or thumbnail is read.</summary>
    public double Aspect { get; set; }
    public bool AspectKnown { get; set; }
}

public enum GalleryRowKind { Header, Tiles }

/// <summary>A row of the folder view: a date header, or a justified row of tiles.</summary>
public sealed record GalleryRow(GalleryRowKind Kind, string Title, string Subtitle, int Start, int Count, double Height, int Group);

/// <summary>Filtering, date grouping and justified layout for the folder view (UI-independent).</summary>
public sealed class GalleryModel
{
    private List<GalleryEntry> _all = new();

    /// <summary>Entries after filter/search, in display order.</summary>
    public IReadOnlyList<GalleryEntry> Visible { get; private set; } = Array.Empty<GalleryEntry>();
    public IReadOnlyList<GalleryRow> Rows { get; private set; } = Array.Empty<GalleryRow>();
    public GalleryFilter Filter { get; private set; } = GalleryFilter.All;
    public string Search { get; private set; } = "";
    public bool GroupByDay { get; set; } = true;

    public int PhotoCount => _all.Count(e => e.Item.Kind.IsImageLike());
    public int VideoCount => _all.Count(e => e.Item.Kind == MediaKind.Video);
    public int TotalCount => _all.Count;
    public long TotalBytes => _all.Sum(e => e.Size);

    public void Load(IEnumerable<GalleryEntry> entries)
    {
        _all = entries.ToList();
        Apply();
    }

    public void SetFilter(GalleryFilter filter, string? search)
    {
        Filter = filter;
        Search = search?.Trim() ?? "";
        Apply();
    }

    private void Apply()
    {
        IEnumerable<GalleryEntry> q = _all;
        if (Filter == GalleryFilter.Photos) q = q.Where(e => e.Item.Kind.IsImageLike());
        else if (Filter == GalleryFilter.Videos) q = q.Where(e => e.Item.Kind == MediaKind.Video);
        if (Search.Length > 0) q = q.Where(e => e.Item.FileName.Contains(Search, StringComparison.CurrentCultureIgnoreCase));
        Visible = q.ToList();
        Rows = Array.Empty<GalleryRow>();
    }

    public int IndexOf(string path)
    {
        for (int i = 0; i < Visible.Count; i++)
            if (FolderScanner.PathEquals(Visible[i].Item.Path, path)) return i;
        return -1;
    }

    /// <summary>Recomputes the rows for a content width. Headers come before each day's tiles.</summary>
    public IReadOnlyList<GalleryRow> Layout(double width, double targetHeight, double gap, CultureInfo culture,
        Func<int, string>? countLabel = null)
    {
        var rows = new List<GalleryRow>();
        int i = 0, group = 0;
        while (i < Visible.Count)
        {
            int j = i + 1;
            if (GroupByDay)
                while (j < Visible.Count && Visible[j].Modified.Date == Visible[i].Modified.Date) j++;
            else
                j = Visible.Count;

            if (GroupByDay)
                rows.Add(new GalleryRow(GalleryRowKind.Header, DayLabel(Visible[i].Modified, culture),
                    countLabel?.Invoke(j - i) ?? (j - i).ToString(culture), i, j - i, 0, group));

            var aspects = new List<double>(j - i);
            for (int k = i; k < j; k++) aspects.Add(Visible[k].Aspect);
            foreach (var r in JustifiedLayout.Compute(aspects, width, targetHeight, gap))
                rows.Add(new GalleryRow(GalleryRowKind.Tiles, "", "", i + r.Start, r.Count, r.Height, group));
            i = j;
            group++;
        }
        Rows = rows;
        return rows;
    }

    /// <summary>"Friday, 7 November 2026" in the UI language, always on the Gregorian calendar.</summary>
    public static string DayLabel(DateTime date, CultureInfo culture)
    {
        var dtf = (DateTimeFormatInfo)culture.DateTimeFormat.Clone();
        if (dtf.Calendar is not GregorianCalendar)
        {
            var greg = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault();
            if (greg is not null) dtf.Calendar = greg;
        }
        return date.ToString(dtf.LongDatePattern, dtf);
    }

    /// <summary>Row index (in <see cref="Rows"/>) containing visible item <paramref name="index"/>.</summary>
    public int RowOf(int index)
    {
        for (int r = 0; r < Rows.Count; r++)
            if (Rows[r].Kind == GalleryRowKind.Tiles && index >= Rows[r].Start && index < Rows[r].Start + Rows[r].Count) return r;
        return -1;
    }

    /// <summary>
    /// Keyboard navigation in visual terms. <paramref name="dx"/> is ±1 in *reading* order (the
    /// caller swaps it for right-to-left layouts); <paramref name="dy"/> moves a row up or down,
    /// landing on the tile whose centre is closest horizontally.
    /// </summary>
    public int Move(int index, int dx, int dy, double gap)
    {
        if (Visible.Count == 0) return -1;
        if (index < 0) return 0;
        if (dx != 0) return Math.Clamp(index + dx, 0, Visible.Count - 1);
        if (dy == 0) return index;

        int row = RowOf(index);
        if (row < 0) return index;
        double centre = CentreOf(row, index, gap);
        int r = row + dy;
        while (r >= 0 && r < Rows.Count && Rows[r].Kind != GalleryRowKind.Tiles) r += dy;
        if (r < 0 || r >= Rows.Count) return index;

        var target = Rows[r];
        int best = target.Start;
        double bestDist = double.MaxValue;
        for (int k = target.Start; k < target.Start + target.Count; k++)
        {
            double d = Math.Abs(CentreOf(r, k, gap) - centre);
            if (d < bestDist) { bestDist = d; best = k; }
        }
        return best;
    }

    private double CentreOf(int row, int index, double gap)
    {
        var r = Rows[row];
        double x = 0;
        for (int k = r.Start; k < index; k++) x += JustifiedLayout.ItemWidth(Visible[k].Aspect, r.Height) + gap;
        return x + JustifiedLayout.ItemWidth(Visible[index].Aspect, r.Height) / 2;
    }
}
