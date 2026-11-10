using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Mashhad.Editing;

public sealed class FfmpegException : Exception
{
    public int ExitCode { get; }
    public string Log { get; }
    public FfmpegException(string message, int exitCode, string log) : base(message)
    {
        ExitCode = exitCode;
        Log = log;
    }
}

public sealed record ToolPaths(string Ffmpeg, string Ffprobe);

/// <summary>Finds ffmpeg/ffprobe: configured folder, next to the app, then PATH.</summary>
public static class FfmpegLocator
{
    public static ToolPaths? Locate(string? configuredDirectory = null, string? appDirectory = null, string? pathVariable = null)
    {
        string exe = OperatingSystem.IsWindows() ? ".exe" : "";
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            dirs.Add(configuredDirectory);
            dirs.Add(Path.Combine(configuredDirectory, "bin"));
        }
        appDirectory ??= AppContext.BaseDirectory;
        dirs.Add(Path.Combine(appDirectory, "ffmpeg"));
        dirs.Add(Path.Combine(appDirectory, "ffmpeg", "bin"));
        dirs.Add(appDirectory);
        pathVariable ??= Environment.GetEnvironmentVariable("PATH") ?? "";
        dirs.AddRange(pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var dir in dirs)
        {
            try
            {
                var ffmpeg = Path.Combine(dir, "ffmpeg" + exe);
                var ffprobe = Path.Combine(dir, "ffprobe" + exe);
                if (File.Exists(ffmpeg) && File.Exists(ffprobe)) return new ToolPaths(ffmpeg, ffprobe);
            }
            catch (ArgumentException) { }
        }
        return null;
    }
}

public static class ProgressParser
{
    /// <summary>
    /// Extracts the output position in seconds from a <c>-progress</c> line, or null for other lines.
    /// (<c>out_time_ms</c> is, despite its name, in microseconds.)
    /// </summary>
    public static double? ParseSeconds(string? line)
    {
        if (line is null) return null;
        int eq = line.IndexOf('=');
        if (eq <= 0) return null;
        var key = line.AsSpan(0, eq).Trim();
        var value = line.AsSpan(eq + 1).Trim();
        if (key.SequenceEqual("out_time_us") || key.SequenceEqual("out_time_ms"))
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us >= 0 ? us / 1_000_000.0 : null;
        return null;
    }
}

/// <summary>Runs ffmpeg/ffprobe without a console window, reporting progress and supporting cancellation.</summary>
public sealed class FfmpegRunner
{
    public ToolPaths Tools { get; }
    public FfmpegRunner(ToolPaths tools) => Tools = tools;

    /// <summary>Runs ffmpeg. <paramref name="progress"/> receives 0..1 when <paramref name="duration"/> is known.</summary>
    public async Task RunAsync(IReadOnlyList<string> args, double duration = 0, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var (exit, _, err) = await RunProcessAsync(Tools.Ffmpeg, args, line =>
        {
            if (progress is null || duration <= 0) return;
            if (ProgressParser.ParseSeconds(line) is double s) progress.Report(Math.Clamp(s / duration, 0, 1));
        }, ct).ConfigureAwait(false);
        if (exit != 0)
            throw new FfmpegException($"ffmpeg failed with exit code {exit}: {LastLine(err)}", exit, err);
        progress?.Report(1);
    }

    public async Task<MediaProbe> ProbeAsync(string input, CancellationToken ct = default)
    {
        var (exit, output, err) = await RunProcessAsync(Tools.Ffprobe, FfmpegArgs.Probe(input), null, ct).ConfigureAwait(false);
        if (exit != 0) throw new FfmpegException($"ffprobe failed: {LastLine(err)}", exit, err);
        return MediaProbe.Parse(output);
    }

    /// <summary>Keyframe times relative to <paramref name="startTime"/> (pass <see cref="MediaProbe.StartTime"/>).</summary>
    public async Task<KeyframeIndex> ProbeKeyframesAsync(string input, double startTime = 0, CancellationToken ct = default)
    {
        var (exit, output, err) = await RunProcessAsync(Tools.Ffprobe, FfmpegArgs.ProbeKeyframes(input), null, ct).ConfigureAwait(false);
        if (exit != 0) throw new FfmpegException($"ffprobe failed: {LastLine(err)}", exit, err);
        return KeyframeIndex.ParsePacketCsv(output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .RelativeTo(startTime);
    }

    private static string LastLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? "" : lines[^1];
    }

    private static async Task<(int Exit, string Out, string Err)> RunProcessAsync(string exe, IReadOnlyList<string> args,
        Action<string>? onStdoutLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (onStdoutLine is not null) onStdoutLine(e.Data);
            else lock (stdout) stdout.Append(e.Data).Append('\n');
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stderr)
            {
                stderr.Append(e.Data).Append('\n');
                if (stderr.Length > 64 * 1024) stderr.Remove(0, stderr.Length - 32 * 1024); // keep the tail
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
        process.WaitForExit(); // flush async readers
        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
