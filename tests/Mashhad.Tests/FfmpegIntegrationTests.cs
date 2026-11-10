using Mashhad.Core.Imaging;
using Mashhad.Editing;

namespace Mashhad.Tests;

/// <summary>
/// Runs the generated commands against a real FFmpeg. Skipped when ffmpeg/ffprobe are not
/// installed (set MASHHAD_FFMPEG_DIR or put them on PATH).
/// </summary>
public class FfmpegIntegrationTests
{
    private static FfmpegRunner Runner()
    {
        Skip.If(Tools.Paths is null, "ffmpeg/ffprobe not found");
        return new FfmpegRunner(Tools.Paths!);
    }

    [SkippableFact]
    public async Task Probe_reports_streams_duration_and_keyframes()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("src.mp4"));

        var probe = await runner.ProbeAsync(src);
        Assert.Equal(10, probe.Duration, 1);
        Assert.Equal("h264", probe.MainVideo!.Codec);
        Assert.Equal(25, probe.MainVideo.FrameRate, 3);
        Assert.Contains(probe.Streams, s => s.IsAudio);

        var keys = await runner.ProbeKeyframesAsync(src);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => (double)i), keys.Times);
    }

    [SkippableTheory]
    [InlineData(".mp4", 0)]
    [InlineData(".mkv", 0)]
    [InlineData(".mov", 0)]
    [InlineData(".ts", 0)]    // absolute timestamps start at 1.4 s; FFmpeg seeks forward to the next keyframe
    [InlineData(".mp4", 2)]   // with B-frames (frame reordering)
    [InlineData(".mkv", 2)]   // FFmpeg shifts the seek target by 3/23 s here
    [InlineData(".ts", 2)]
    public async Task Keyframe_snapped_cut_is_a_lossless_stream_copy(string ext, int bframes)
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("src" + ext), bframes: bframes);
        var probe = await runner.ProbeAsync(src);
        var keys = await runner.ProbeKeyframesAsync(src, probe.StartTime);
        // One keyframe per second; some containers shift video by the audio priming (~23 ms).
        Assert.Equal(10, keys.Count);
        Assert.All(keys.Times.Select((t, i) => t - i), d => Assert.InRange(d, 0, 0.05));

        // In point 2.4 s is not a keyframe; it must snap back to 2.0 s. Out point 5.2 s is exclusive.
        var plan = ExportPlanner.Plan(src, new[] { new Segment(2.4, 5.2) }, probe.Duration, keys,
            new ExportOptions { SourceFormat = probe.FormatName });
        var progress = new List<double>();
        var outputs = await new Exporter(runner).RunAsync(plan, new Collect(p => progress.Add(p.Overall)));

        var output = Assert.Single(outputs);
        var outPackets = Tools.VideoPackets(output);
        Assert.True(outPackets[0].Key, "output must start on a keyframe");

        // Frames 50..129 of the source (2.0 s – 5.2 s at 25 fps), pixel-identical after decoding...
        var outHashes = Tools.FrameHashes(output);
        Assert.Equal(Tools.FrameHashes(src).Skip(50).Take(outHashes.Count), outHashes);
        // The in point is exact. A stream copy ends on a decode-order boundary, so with B-frames
        // the out point may run over by the reorder depth; without them it is exact too.
        Assert.InRange(outHashes.Count, 80, 80 + bframes);
        // ...because they are the very same compressed packets (same count, same sizes), not a re-encode.
        if (bframes == 0)
        {
            var srcPackets = Tools.VideoPackets(src).OrderBy(p => p.Pts).Skip(50).Take(80).ToList();
            Assert.Equal(srcPackets.Select(p => p.Size), outPackets.OrderBy(p => p.Pts).Select(p => p.Size));
        }

        var outProbe = await runner.ProbeAsync(output);
        Assert.InRange(outProbe.Duration, 3.1, 3.6); // container durations include reorder delay / estimates
        Assert.Equal("h264", outProbe.MainVideo!.Codec);
        Assert.Contains(outProbe.Streams, s => s.IsAudio);
        Assert.Equal(1.0, progress[^1]);
    }

    [SkippableFact]
    public async Task Cut_from_the_very_start_and_to_the_very_end()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("src.mkv"), seconds: 4);
        var keys = await runner.ProbeKeyframesAsync(src);
        var plan = ExportPlanner.Plan(src, new[] { new Segment(0, 1), new Segment(3, 4) }, 4, keys, new ExportOptions());
        var outputs = await new Exporter(runner).RunAsync(plan);
        var all = Tools.FrameHashes(src);
        Assert.Equal(all.Take(25), Tools.FrameHashes(outputs[0]));
        Assert.Equal(all.Skip(75), Tools.FrameHashes(outputs[1]));
    }

    [SkippableTheory]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    public async Task Multiple_segments_merge_losslessly_and_parts_are_removed(string ext)
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("my clip's source" + ext)); // space + quote in the name
        var keys = await runner.ProbeKeyframesAsync(src);

        var segments = new[] { new Segment(1, 3), new Segment(6, 8) };
        var plan = ExportPlanner.Plan(src, segments, 10, keys, new ExportOptions { Merge = true });
        var outputs = await new Exporter(runner).RunAsync(plan);

        var merged = Assert.Single(outputs);
        Assert.EndsWith("-merged" + ext, merged);
        var all = Tools.FrameHashes(src);
        Assert.Equal(all.Skip(25).Take(50).Concat(all.Skip(150).Take(50)), Tools.FrameHashes(merged));
        Assert.Equal(4.0, (await runner.ProbeAsync(merged)).Duration, 1);
        // Only the source and the merged file remain: no part files, no concat list.
        Assert.Equal(2, Directory.GetFiles(dir.Path).Length);
    }

    [SkippableFact]
    public async Task Remux_changes_container_without_touching_packets()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("src.mp4"), seconds: 3);
        var dst = dir.File("out.mkv");
        await runner.RunAsync(FfmpegArgs.Remux(src, dst));
        Assert.Equal(Tools.VideoPackets(src).Select(p => p.Size), Tools.VideoPackets(dst).Select(p => p.Size));
        Assert.Contains("matroska", (await runner.ProbeAsync(dst)).FormatName);
    }

    [SkippableFact]
    public async Task Frame_export_single_and_range()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("src.mp4"), seconds: 3);

        await runner.RunAsync(FfmpegArgs.ExtractFrame(src, dir.File("one.png"), 1.5, FrameFormat.Png));
        var one = await runner.ProbeAsync(dir.File("one.png"));
        Assert.Equal((320, 240), (one.MainVideo!.Width, one.MainVideo.Height));

        await runner.RunAsync(FfmpegArgs.ExtractFrame(src, dir.File("one.jpg"), 0, FrameFormat.Jpeg));
        Assert.True(new FileInfo(dir.File("one.jpg")).Length > 0);

        Directory.CreateDirectory(dir.File("frames"));
        await runner.RunAsync(FfmpegArgs.ExtractFrames(src, dir.File("frames/f_%05d.png"), 1, 2, FrameFormat.Png));
        Assert.Equal(25, Directory.GetFiles(dir.File("frames")).Length);
    }

    [SkippableFact]
    public async Task Image_crop_and_rotate_produce_the_expected_geometry()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleImage(dir.File("src.png"), "640x480");

        var rotated = dir.File("rot.png");
        await runner.RunAsync(FfmpegArgs.TransformImage(src, rotated, 90, null, FrameFormat.Png));
        var r = (await runner.ProbeAsync(rotated)).MainVideo!;
        Assert.Equal((480, 640), (r.Width, r.Height));

        // Crop is expressed in the rotated orientation (what the user sees).
        var cropped = dir.File("crop.jpg");
        await runner.RunAsync(FfmpegArgs.TransformImage(src, cropped, 90, new PixelRect(40, 100, 300, 500), FrameFormat.Jpeg));
        var c = (await runner.ProbeAsync(cropped)).MainVideo!;
        Assert.Equal((300, 500), (c.Width, c.Height));
    }

    [SkippableFact]
    public async Task Failure_reports_ffmpeg_error_and_leaves_no_partial_output()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var bogus = dir.Touch("not-a-video.mp4", 128);
        var plan = ExportPlanner.Plan(bogus, new[] { new Segment(0, 1) }, 5, null, new ExportOptions());
        var ex = await Assert.ThrowsAsync<FfmpegException>(() => new Exporter(runner).RunAsync(plan));
        Assert.NotEqual(0, ex.ExitCode);
        Assert.NotEmpty(ex.Log);
        Assert.Equal(new[] { bogus }, Directory.GetFiles(dir.Path));
    }

    [SkippableFact]
    public async Task Cancellation_stops_ffmpeg_and_cleans_up()
    {
        var runner = Runner();
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("src.mp4"), seconds: 3);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var plan = ExportPlanner.Plan(src, new[] { new Segment(0, 2) }, 3, null, new ExportOptions());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new Exporter(runner).RunAsync(plan, ct: cts.Token));
        Assert.Equal(new[] { src }, Directory.GetFiles(dir.Path));
    }

    private sealed class Collect : IProgress<ExportProgress>
    {
        private readonly Action<ExportProgress> _a;
        public Collect(Action<ExportProgress> a) => _a = a;
        public void Report(ExportProgress value) { lock (this) _a(value); }
    }
}
