using System.Diagnostics;
using Mashhad.Editing;

namespace Mashhad.Tests;

/// <summary>A scratch directory removed when the test finishes.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mashhad-tests-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public string Touch(string name, int bytes = 4)
    {
        var p = File(name);
        System.IO.File.WriteAllBytes(p, new byte[bytes]);
        return p;
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch (IOException) { }
    }
}

public static class Tools
{
    /// <summary>ffmpeg/ffprobe from MASHHAD_FFMPEG_DIR or PATH; null when not installed (tests then skip).</summary>
    public static readonly ToolPaths? Paths =
        FfmpegLocator.Locate(Environment.GetEnvironmentVariable("MASHHAD_FFMPEG_DIR"), appDirectory: AppContext.BaseDirectory);

    public static string Run(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var errTask = p.StandardError.ReadToEndAsync();
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"{exe} failed ({p.ExitCode}): {errTask.Result}");
        return output;
    }

    /// <summary>10 s, 25 fps test clip with a keyframe exactly every second and a stereo tone.</summary>
    public static string MakeSampleVideo(string path, int seconds = 10, string size = "320x240", int bframes = 0)
    {
        Run(Paths!.Ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc2=size={size}:rate=25:duration={seconds}",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-g", "25", "-keyint_min", "25", "-sc_threshold", "0", "-bf", bframes.ToString(),
            "-c:a", "aac", "-b:a", "64k", "-shortest", path);
        return path;
    }

    public static string MakeSampleImage(string path, string size = "640x480")
    {
        Run(Paths!.Ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc2=size={size}:rate=1", "-frames:v", "1", "-update", "1", path);
        return path;
    }

    /// <summary>Checksum of every decoded video frame, in presentation order (proves a copy is lossless).</summary>
    public static List<string> FrameHashes(string path)
    {
        var text = Run(Paths!.Ffmpeg, "-hide_banner", "-loglevel", "error", "-i", path, "-map", "0:v:0", "-f", "framecrc", "-");
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith('#')).Select(l => l.Split(',')[^1].Trim()).ToList();
    }

    /// <summary>(pts, size, isKey) for every packet of the first video stream.</summary>
    public static List<(double Pts, int Size, bool Key)> VideoPackets(string path)
    {
        var csv = Run(Paths!.Ffprobe, "-v", "error", "-select_streams", "v:0",
            "-show_entries", "packet=pts_time,size,flags", "-of", "csv=p=0", path);
        var list = new List<(double, int, bool)>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split(',');
            list.Add((double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(p[1]), p[2].Contains('K')));
        }
        return list;
    }
}
