using System.Globalization;

namespace Mashhad.Core.Playback;

public static class TimeFormat
{
    /// <summary>"1:02:03" or "02:03"; with <paramref name="millis"/> "02:03.450".</summary>
    public static string Clock(double seconds, bool millis = false, bool forceHours = false)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
        long totalMs = (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
        if (!millis) totalMs = totalMs / 1000 * 1000;
        long h = totalMs / 3_600_000, m = totalMs / 60_000 % 60, s = totalMs / 1000 % 60, ms = totalMs % 1000;
        var inv = CultureInfo.InvariantCulture;
        string core = h > 0 || forceHours
            ? string.Create(inv, $"{h}:{m:00}:{s:00}")
            : string.Create(inv, $"{m:00}:{s:00}");
        return millis ? string.Create(inv, $"{core}.{ms:000}") : core;
    }

    /// <summary>File-name safe time stamp, LosslessCut style: "00.01.02.345".</summary>
    public static string FileStamp(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        long totalMs = (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture,
            $"{totalMs / 3_600_000:00}.{totalMs / 60_000 % 60:00}.{totalMs / 1000 % 60:00}.{totalMs % 1000:000}");
    }

    /// <summary>Parses "90", "1:30", "1:02:03.5". Returns null when malformed.</summary>
    public static double? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Trim().Split(':');
        if (parts.Length > 3) return null;
        double total = 0;
        foreach (var p in parts)
        {
            if (!double.TryParse(p, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) || v < 0)
                return null;
            total = total * 60 + v;
        }
        return total;
    }
}
