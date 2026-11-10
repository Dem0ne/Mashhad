using System.Text.Json;

namespace Mashhad.Editing;

/// <summary>Sidecar file ("video.mp4.mashhad.json") that remembers the segments marked on a video.</summary>
public static class ProjectFile
{
    private sealed record Dto(int Version, string Source, List<Segment> Segments);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string PathFor(string mediaPath) => mediaPath + ".mashhad.json";

    public static void Save(string mediaPath, IEnumerable<Segment> segments)
    {
        var list = segments.ToList();
        var path = PathFor(mediaPath);
        if (list.Count == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new Dto(1, Path.GetFileName(mediaPath), list), Json));
    }

    public static List<Segment> Load(string mediaPath)
    {
        var path = PathFor(mediaPath);
        if (!File.Exists(path)) return new List<Segment>();
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path), Json);
            return dto?.Segments?.Where(s => s.Start >= 0 && (s.End is null || s.End > s.Start)).ToList() ?? new List<Segment>();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new List<Segment>();
        }
    }
}
