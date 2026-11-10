namespace Mashhad.Core.Media;

/// <summary>A row of the justified grid: items [Start, Start+Count) shown at Height pixels.</summary>
public readonly record struct JustifiedRow(int Start, int Count, double Height)
{
    public int End => Start + Count;
}

/// <summary>
/// "Justified" photo layout (as in Photos/Flickr): every row is filled edge to edge, each item at
/// its own aspect ratio, rows close to a target height. The last row is not stretched.
/// </summary>
public static class JustifiedLayout
{
    public const double MinAspect = 0.4, MaxAspect = 3.0;

    public static double ClampAspect(double a) => double.IsFinite(a) && a > 0 ? Math.Clamp(a, MinAspect, MaxAspect) : 1.5;

    public static List<JustifiedRow> Compute(IReadOnlyList<double> aspects, double width, double targetHeight, double gap)
    {
        var rows = new List<JustifiedRow>();
        if (aspects.Count == 0 || width <= 0 || targetHeight <= 0) return rows;

        int start = 0;
        double sum = 0;
        for (int i = 0; i < aspects.Count; i++)
        {
            sum += ClampAspect(aspects[i]);
            int count = i - start + 1;
            double natural = (width - gap * (count - 1)) / sum;
            if (natural <= targetHeight)
            {
                rows.Add(new JustifiedRow(start, count, Math.Max(1, natural)));
                start = i + 1;
                sum = 0;
            }
        }
        if (start < aspects.Count) rows.Add(new JustifiedRow(start, aspects.Count - start, targetHeight));
        return rows;
    }

    /// <summary>Pixel width of item <paramref name="index"/> in a row of height <paramref name="height"/>.</summary>
    public static double ItemWidth(double aspect, double height) => ClampAspect(aspect) * height;
}
