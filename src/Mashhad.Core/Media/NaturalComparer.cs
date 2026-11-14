namespace Mashhad.Core.Media;

/// <summary>
/// Explorer-like ordering: digit runs compare numerically ("img2" &lt; "img10"), text compares
/// case-insensitively. Culture-invariant so the gallery order is stable across locales.
/// </summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            char a = x[i], b = y[j];
            if (char.IsAsciiDigit(a) && char.IsAsciiDigit(b))
            {
                int si = i, sj = j;
                while (si < x.Length && x[si] == '0') si++;
                while (sj < y.Length && y[sj] == '0') sj++;
                int ei = si, ej = sj;
                while (ei < x.Length && char.IsAsciiDigit(x[ei])) ei++;
                while (ej < y.Length && char.IsAsciiDigit(y[ej])) ej++;
                int lenA = ei - si, lenB = ej - sj;
                if (lenA != lenB) return lenA < lenB ? -1 : 1;
                for (int k = 0; k < lenA; k++)
                {
                    int d = x[si + k] - y[sj + k];
                    if (d != 0) return d < 0 ? -1 : 1;
                }
                i = ei;
                j = ej;
                continue;
            }

            char ua = char.ToUpperInvariant(a), ub = char.ToUpperInvariant(b);
            if (ua != ub) return ua < ub ? -1 : 1;
            i++;
            j++;
        }

        int rest = (x.Length - i) - (y.Length - j);
        if (rest != 0) return rest < 0 ? -1 : 1;
        return string.CompareOrdinal(x, y);
    }
}
