using System.Text;

namespace Mashhad.Core.Media;

/// <summary>Minimal M3U/M3U8 reader and writer.</summary>
public static class PlaylistFile
{
    public static List<MediaItem> Read(string playlistPath)
    {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(playlistPath)) ?? "";
        return Parse(File.ReadAllLines(playlistPath, Encoding.UTF8), baseDir);
    }

    public static List<MediaItem> Parse(IEnumerable<string> lines, string baseDirectory)
    {
        var result = new List<MediaItem>();
        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] == '#') continue;
            string path;
            if (line.Contains("://", StringComparison.Ordinal))
            {
                if (!line.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new MediaItem(line, MediaKind.Video)); // network stream
                    continue;
                }
                path = new Uri(line).LocalPath;
            }
            else
            {
                path = Path.IsPathRooted(line) ? line : Path.GetFullPath(Path.Combine(baseDirectory, line));
            }
            var item = MediaItem.FromPath(path);
            result.Add(item.Kind == MediaKind.Unknown ? item with { Kind = MediaKind.Video } : item);
        }
        return result;
    }

    public static void Write(string playlistPath, IEnumerable<MediaItem> items)
    {
        var sb = new StringBuilder("#EXTM3U\n");
        foreach (var item in items) sb.Append(item.Path).Append('\n');
        File.WriteAllText(playlistPath, sb.ToString(), new UTF8Encoding(false));
    }
}
