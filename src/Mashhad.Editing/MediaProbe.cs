using System.Globalization;
using System.Text.Json;

namespace Mashhad.Editing;

public sealed record ProbeStream(int Index, string Type, string Codec, int Width, int Height, double FrameRate,
    string? Language, string? Title, int Channels)
{
    public bool IsVideo => Type == "video";
    public bool IsAudio => Type == "audio";
    public bool IsSubtitle => Type == "subtitle";
    /// <summary>Cover art and thumbnails are "video" streams that should not drive cutting.</summary>
    public bool IsAttachedPicture { get; init; }
}

public sealed record MediaProbe(double Duration, double StartTime, string FormatName, long BitRate, IReadOnlyList<ProbeStream> Streams)
{
    public ProbeStream? MainVideo => Streams.FirstOrDefault(s => s.IsVideo && !s.IsAttachedPicture);
    public bool HasVideo => MainVideo is not null;

    /// <summary>Parses <c>ffprobe -show_format -show_streams -of json</c>.</summary>
    public static MediaProbe Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        double duration = 0, start = 0;
        long bitRate = 0;
        string format = "";
        if (root.TryGetProperty("format", out var f))
        {
            duration = Num(f, "duration");
            start = Num(f, "start_time");
            bitRate = (long)Num(f, "bit_rate");
            format = Str(f, "format_name") ?? "";
        }

        var streams = new List<ProbeStream>();
        if (root.TryGetProperty("streams", out var ss) && ss.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in ss.EnumerateArray())
            {
                string? lang = null, title = null;
                if (s.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                {
                    foreach (var tag in tags.EnumerateObject())
                    {
                        if (tag.Name.Equals("language", StringComparison.OrdinalIgnoreCase)) lang = tag.Value.GetString();
                        else if (tag.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) title = tag.Value.GetString();
                    }
                }
                bool attached = s.TryGetProperty("disposition", out var disp) && disp.ValueKind == JsonValueKind.Object
                                && disp.TryGetProperty("attached_pic", out var ap) && ap.ValueKind == JsonValueKind.Number && ap.GetInt32() == 1;
                streams.Add(new ProbeStream(
                    (int)Num(s, "index"), Str(s, "codec_type") ?? "", Str(s, "codec_name") ?? "",
                    (int)Num(s, "width"), (int)Num(s, "height"), Rate(Str(s, "avg_frame_rate")),
                    lang, title, (int)Num(s, "channels")) { IsAttachedPicture = attached });
                if (duration <= 0) duration = Math.Max(duration, Num(s, "duration"));
            }
        }
        return new MediaProbe(duration, start, format, bitRate, streams);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String &&
            double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        return 0;
    }

    private static double Rate(string? ratio)
    {
        if (string.IsNullOrEmpty(ratio)) return 0;
        var parts = ratio.Split('/');
        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d != 0) return n / d;
        return double.TryParse(ratio, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }
}
