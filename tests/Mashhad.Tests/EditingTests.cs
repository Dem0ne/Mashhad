using Mashhad.Core.Imaging;
using Mashhad.Editing;

namespace Mashhad.Tests;

public class SegmentListTests
{
    private static SegmentList Make(double duration = 100) => new() { Duration = duration };

    [Fact]
    public void In_then_out_creates_one_segment()
    {
        var s = Make();
        s.SetStart(10);
        Assert.Equal(new Segment(10), s.Current);      // open-ended until an out point is set
        s.SetEnd(25);
        Assert.Equal(new[] { new Segment(10, 25) }, s.Segments);
    }

    [Fact]
    public void Out_without_in_starts_at_zero()
    {
        var s = Make();
        s.SetEnd(30);
        Assert.Equal(new Segment(0, 30), s.Current);
    }

    [Fact]
    public void Contradicting_points_never_produce_a_negative_segment()
    {
        var s = Make();
        s.SetStart(10); s.SetEnd(20);
        s.SetStart(50);                                 // in after out -> out released
        Assert.Equal(new Segment(50), s.Current);
        s.SetEnd(20);                                   // out before in -> in moves to 0
        Assert.Equal(new Segment(0, 20), s.Current);
        Assert.All(s.Resolved(), seg => Assert.True(seg.End > seg.Start));
    }

    [Fact]
    public void Times_are_clamped_to_the_file()
    {
        var s = Make(100);
        s.SetStart(-5); s.SetEnd(500);
        Assert.Equal(new Segment(0, 100), s.Current);
        s.AddAt(100);                                   // nothing left to cut at the very end
        Assert.Equal(1, s.Count);
    }

    [Fact]
    public void Multiple_segments_split_and_remove()
    {
        var s = Make();
        s.SetStart(10); s.SetEnd(40);
        s.AddAt(60); s.SetEnd(80);
        Assert.Equal(2, s.Count);
        Assert.Equal(1, s.CurrentIndex);

        Assert.True(s.SplitAt(20));
        Assert.Equal(new[] { new Segment(10, 20), new Segment(20, 40), new Segment(60, 80) }, s.Segments);
        Assert.Equal(1, s.CurrentIndex);
        Assert.False(s.SplitAt(50));                    // not inside any segment
        Assert.False(s.SplitAt(10));                    // on a boundary

        Assert.True(s.RemoveAt(0));
        Assert.Equal(0, s.CurrentIndex);
        Assert.Equal(new Segment(20, 40), s.Current);
    }

    [Fact]
    public void Resolved_sorts_and_closes_open_ends()
    {
        var s = Make(100);
        s.AddAt(70);
        s.AddAt(10); s.SetEnd(20);
        Assert.Equal(new[] { new Segment(10, 20), new Segment(70, 100) }, s.Resolved());
    }

    [Fact]
    public void Invert_keeps_the_gaps()
    {
        var s = Make(100);
        s.Load(new[] { new Segment(10, 20), new Segment(15, 40), new Segment(90) });
        Assert.True(s.Invert());
        Assert.Equal(new[] { new Segment(0, 10), new Segment(40, 90) }, s.Segments);
        Assert.False(new SegmentList().Invert());       // needs a duration
    }

    [Fact]
    public void Raises_changed_and_load_drops_invalid_segments()
    {
        var s = Make();
        int changes = 0;
        s.Changed += () => changes++;
        s.Load(new[] { new Segment(5, 3), new Segment(-1, 4), new Segment(1, 2) });
        Assert.Equal(new[] { new Segment(1, 2) }, s.Segments);
        s.Rename(0, "  intro ");
        Assert.Equal("intro", s.Current!.Name);
        Assert.Equal(2, changes);
    }
}

public class KeyframeIndexTests
{
    private static readonly KeyframeIndex K = new(new[] { 4.0, 0.0, 2.0, 2.0, 6.0 });

    [Fact]
    public void Finds_neighbours()
    {
        Assert.Equal(new[] { 0.0, 2.0, 4.0, 6.0 }, K.Times);
        Assert.Equal(2.0, K.AtOrBefore(3.9));
        Assert.Equal(4.0, K.AtOrBefore(4.0));
        Assert.Equal(4.0, K.AtOrBefore(3.9999));      // within tolerance counts as "at"
        Assert.Equal(4.0, K.AtOrAfter(2.1));
        Assert.Equal(2.0, K.Before(4.0));
        Assert.Equal(6.0, K.After(4.0));
        Assert.Null(K.After(6.0));
        Assert.Null(K.Before(0.0));
        Assert.Equal(6.0, K.AtOrBefore(999));
        Assert.Equal(2.0, K.Nearest(2.9));
        Assert.Equal(4.0, K.Nearest(3.1));
        Assert.True(K.IsKeyframe(2.0002));
        Assert.False(K.IsKeyframe(2.5));
    }

    [Fact]
    public void Empty_index_answers_null()
    {
        Assert.Null(KeyframeIndex.Empty.AtOrBefore(1));
        Assert.Null(KeyframeIndex.Empty.Nearest(1));
        Assert.False(KeyframeIndex.Empty.IsKeyframe(0));
    }

    [Fact]
    public void Parses_ffprobe_packet_csv()
    {
        var k = KeyframeIndex.ParsePacketCsv(new[] { "0.000000,K__", "0.040000,___", "1.000000,K_", "N/A,K__", "", "2.000000,__" });
        Assert.Equal(new[] { 0.0, 1.0 }, k.Times);
    }
}

public class FfmpegArgsTests
{
    [Fact]
    public void Cut_is_a_stream_copy_with_input_seeking()
    {
        var a = FfmpegArgs.Cut("in file.mkv", "out.mkv", 12.5, 20);
        var s = string.Join(' ', a);
        Assert.True(a.IndexOf("-ss") < a.IndexOf("-i"), "-ss must come before -i (keyframe seek, no decode)");
        Assert.Equal("12.5", a[a.IndexOf("-ss") + 1]);
        Assert.Equal("7.5", a[a.IndexOf("-t") + 1]);
        Assert.Contains("-c copy", s);
        Assert.Contains("-map 0", s);
        Assert.Contains("-avoid_negative_ts make_zero", s);
        Assert.Equal("out.mkv", a[^1]);
        Assert.Contains("in file.mkv", a);                // one argument, no quoting needed
        Assert.DoesNotContain(a, x => x.StartsWith("-c:v") || x == "-vf" || x == "-crf"); // never re-encodes
    }

    [Fact]
    public void Cut_from_start_and_to_end_omits_seek_and_duration()
    {
        var a = FfmpegArgs.Cut("in.mp4", "out.mp4", 0, null);
        Assert.DoesNotContain("-ss", a);
        Assert.DoesNotContain("-t", a);
        Assert.Contains("+faststart", a);
    }

    [Fact]
    public void Times_use_invariant_culture()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA"); // different digits/separators
            var a = FfmpegArgs.Cut("in.mkv", "out.mkv", 1.25, 3.75);
            Assert.Equal("1.25", a[a.IndexOf("-ss") + 1]);
            Assert.Equal("2.5", a[a.IndexOf("-t") + 1]);
        }
        finally { Thread.CurrentThread.CurrentCulture = old; }
    }

    [Fact]
    public void Concat_list_escapes_quotes_and_backslashes()
    {
        var list = FfmpegArgs.ConcatList(new[] { @"C:\My Videos\it's here.mp4", "/tmp/فيديو.mkv" });
        Assert.Equal("ffconcat version 1.0\nfile 'C:/My Videos/it'\\''s here.mp4'\nfile '/tmp/فيديو.mkv'\n", list);
    }

    [Fact]
    public void Concat_and_remux_copy_streams()
    {
        var c = string.Join(' ', FfmpegArgs.Concat("list.txt", "out.mkv"));
        Assert.Contains("-f concat -safe 0 -i list.txt", c);
        Assert.Contains("-c copy", c);
        var r = string.Join(' ', FfmpegArgs.Remux("in.mkv", "out.mp4"));
        Assert.Contains("-c copy", r);
        Assert.Contains("-c:s mov_text", r);              // only subtitles are converted for MP4
        Assert.DoesNotContain("-map 0 ", r);
    }

    [Fact]
    public void Image_transform_builds_rotation_then_crop()
    {
        var a = FfmpegArgs.TransformImage("in.jpg", "out.png", 90, new PixelRect(10, 20, 300, 200), FrameFormat.Png);
        Assert.Equal("transpose=clock,crop=300:200:10:20", a[a.IndexOf("-vf") + 1]);
        var none = FfmpegArgs.TransformImage("in.jpg", "out.jpg", 0, null, FrameFormat.Jpeg);
        Assert.DoesNotContain("-vf", none);
        Assert.Equal("hflip,vflip", FfmpegArgs.TransformImage("i.png", "o.png", 180, null, FrameFormat.Png) is var x ? x[x.IndexOf("-vf") + 1] : "");
    }

    [Fact]
    public void Frame_sequence_is_not_written_in_update_mode()
    {
        Assert.DoesNotContain("-update", FfmpegArgs.ExtractFrames("in.mp4", "f_%05d.png", 1, 2, FrameFormat.Png));
        Assert.Contains("-update", FfmpegArgs.ExtractFrame("in.mp4", "f.png", 1, FrameFormat.Png));
    }

    [Fact]
    public void Display_quotes_only_when_needed() =>
        Assert.Equal("ffmpeg -i \"a b.mp4\" out.mp4", FfmpegArgs.Display("ffmpeg", new[] { "-i", "a b.mp4", "out.mp4" }));
}

public class ExportPlannerTests
{
    private static readonly KeyframeIndex Keys = new(new[] { 0.0, 2.0, 4.0, 6.0, 8.0 });
    private static readonly Func<string, bool> NoFiles = _ => false;

    /// <summary>Expected output path: the planner works with full paths ("/v" is "D:\v" on Windows).</summary>
    private static string P(string dir, string name) => Path.Combine(Path.GetFullPath(dir), name);

    [Fact]
    public void Starts_snap_back_to_keyframes_and_names_reflect_the_real_cut()
    {
        var plan = ExportPlanner.Plan("/v/movie.mkv", new[] { new Segment(2.7, 5.5) }, 10, Keys, new ExportOptions(), NoFiles);

        Assert.Equal(new Segment(2.0, 5.5), plan.EffectiveSegments.Single());
        var step = plan.Steps.Single();
        Assert.Equal(P("/v", "movie-00.00.02.000-00.00.05.500.mkv"), step.Output);
        Assert.Equal(3.5, step.Duration, 6);
        var a = step.Arguments;
        // Aim past the keyframe (but inside its GOP) so FFmpeg lands on it in every container.
        double ss = double.Parse(a[a.IndexOf("-ss") + 1], System.Globalization.CultureInfo.InvariantCulture);
        double t = double.Parse(a[a.IndexOf("-t") + 1], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(ss, 2.13, 2.14);
        Assert.Equal(5.5, ss + t, 5);                     // still ends exactly at 5.5
    }

    [Fact]
    public void Seek_target_stays_inside_short_gops()
    {
        var intra = new KeyframeIndex(Enumerable.Range(0, 100).Select(i => i * 0.04)); // every frame is a keyframe
        double seek = ExportPlanner.SeekTargetFor(2.0, intra);
        Assert.Equal(2.0, intra.AtOrBefore(seek));
        Assert.Equal(8.0 + ExportPlanner.SeekMargin, ExportPlanner.SeekTargetFor(8.0, Keys), 9); // last keyframe: no next
    }

    [Fact]
    public void Transport_streams_seek_exactly_to_the_keyframe()
    {
        Assert.True(ExportPlanner.SeeksForward("mpegts", "x.bin"));
        Assert.True(ExportPlanner.SeeksForward(null, "x.M2TS"));
        Assert.False(ExportPlanner.SeeksForward("matroska,webm", "x.ts")); // the probed format wins over the extension
        var plan = ExportPlanner.Plan("/v/m.ts", new[] { new Segment(2.7, 5.5) }, 10, Keys, new ExportOptions(), NoFiles);
        Assert.Equal("2", plan.Steps[0].Arguments[plan.Steps[0].Arguments.IndexOf("-ss") + 1]);
    }

    [Fact]
    public void Keyframe_times_can_be_rebased_to_the_file_start() =>
        Assert.Equal(new[] { 0.0, 1.0 }, new KeyframeIndex(new[] { 1.4, 2.4 }).RelativeTo(1.4).Times);

    [Fact]
    public void Snapping_can_be_disabled_and_missing_index_is_tolerated()
    {
        var off = ExportPlanner.Plan("/v/m.mkv", new[] { new Segment(2.7, 5.5) }, 10, Keys, new ExportOptions { SnapToKeyframes = false }, NoFiles);
        Assert.Equal(2.7, off.EffectiveSegments[0].Start);
        var none = ExportPlanner.Plan("/v/m.mkv", new[] { new Segment(2.7, 5.5) }, 10, null, new ExportOptions(), NoFiles);
        Assert.Equal(2.7, none.EffectiveSegments[0].Start);
    }

    [Fact]
    public void Segment_reaching_the_end_has_no_duration_limit()
    {
        var plan = ExportPlanner.Plan("/v/m.mkv", new[] { new Segment(4, 10) }, 10, Keys, new ExportOptions(), NoFiles);
        Assert.DoesNotContain("-t", plan.Steps[0].Arguments);
    }

    [Fact]
    public void Merge_adds_a_concat_step_and_cleans_up_parts()
    {
        var segs = new[] { new Segment(0, 2), new Segment(4, 6, "the: end?") };
        var plan = ExportPlanner.Plan("/v/m.mp4", segs, 10, Keys, new ExportOptions { Merge = true, OutputDirectory = "/out" }, NoFiles);

        Assert.Equal(3, plan.Steps.Count);
        Assert.Equal(P("/out", "m-the_ end_.mp4"), plan.Steps[1].Output);   // names are sanitised
        Assert.Equal(P("/out", "m-merged.mp4"), plan.Steps[2].Output);
        Assert.Equal(new[] { plan.Steps[2].Output }, plan.Outputs);
        Assert.Contains(plan.Steps[0].Output, plan.TemporaryFiles);
        Assert.Contains(plan.ConcatListPath!, plan.Steps[2].Arguments);
        Assert.Contains("m-00.00.00.000-00.00.02.000.mp4", plan.ConcatListContent);
        Assert.Equal(4, plan.Steps[2].Duration, 6);
    }

    [Fact]
    public void Single_segment_merge_is_just_a_cut()
    {
        var plan = ExportPlanner.Plan("/v/m.mp4", new[] { new Segment(0, 2) }, 10, Keys, new ExportOptions { Merge = true }, NoFiles);
        Assert.Single(plan.Steps);
        Assert.Null(plan.ConcatListPath);
    }

    [Fact]
    public void Never_overwrites_existing_files_or_the_source()
    {
        var existing = new HashSet<string> { P("/v", "m-00.00.00.000-00.00.02.000.mp4") };
        var plan = ExportPlanner.Plan("/v/m.mp4", new[] { new Segment(0, 2), new Segment(0, 2) }, 10, Keys, new ExportOptions(), existing.Contains);
        Assert.Equal(P("/v", "m-00.00.00.000-00.00.02.000 (2).mp4"), plan.Steps[0].Output);
        Assert.Equal(P("/v", "m-00.00.00.000-00.00.02.000 (3).mp4"), plan.Steps[1].Output);
        Assert.NotEqual(Path.GetFullPath("/v/m.mp4"), ExportPlanner.DerivedPath("/v/m.mp4", "x", fileExists: NoFiles));
    }

    [Fact]
    public void Empty_segment_list_is_an_error() =>
        Assert.Throws<InvalidOperationException>(() => ExportPlanner.Plan("/v/m.mp4", Array.Empty<Segment>(), 10, Keys, new ExportOptions(), NoFiles));
}

public class ProbeAndProgressTests
{
    [Fact]
    public void Parses_ffprobe_json()
    {
        const string json = """
        {"streams":[
          {"index":0,"codec_name":"h264","codec_type":"video","width":1920,"height":1080,"avg_frame_rate":"30000/1001","disposition":{"attached_pic":0}},
          {"index":1,"codec_name":"aac","codec_type":"audio","channels":2,"avg_frame_rate":"0/0","tags":{"language":"ara","title":"عربي"}},
          {"index":2,"codec_name":"mjpeg","codec_type":"video","width":600,"height":600,"disposition":{"attached_pic":1}},
          {"index":3,"codec_name":"subrip","codec_type":"subtitle","tags":{"LANGUAGE":"eng"}}],
         "format":{"format_name":"matroska,webm","start_time":"0.000000","duration":"125.500000","bit_rate":"800000"}}
        """;
        var p = MediaProbe.Parse(json);
        Assert.Equal(125.5, p.Duration);
        Assert.Equal(4, p.Streams.Count);
        Assert.Equal(0, p.MainVideo!.Index);
        Assert.Equal(29.97, p.MainVideo.FrameRate, 2);
        Assert.Equal(("ara", "عربي", 2), (p.Streams[1].Language, p.Streams[1].Title, p.Streams[1].Channels));
        Assert.True(p.Streams[2].IsAttachedPicture);
        Assert.Equal("eng", p.Streams[3].Language);
    }

    [Theory]
    [InlineData("out_time_us=1500000", 1.5)]
    [InlineData("out_time_ms=2000000", 2.0)]
    [InlineData("out_time_us=N/A", null)]
    [InlineData("out_time=00:00:01.500000", null)]
    [InlineData("progress=end", null)]
    [InlineData("out_time_us=-5", null)]
    [InlineData(null, null)]
    public void Parses_progress_lines(string? line, double? expected) => Assert.Equal(expected, ProgressParser.ParseSeconds(line));

    [Fact]
    public void Locator_prefers_configured_folder_then_app_folder()
    {
        using var dir = new TempDir();
        string exe = OperatingSystem.IsWindows() ? ".exe" : "";
        Directory.CreateDirectory(dir.File("cfg/bin"));
        Directory.CreateDirectory(dir.File("app/ffmpeg"));
        dir.Touch("cfg/bin/ffmpeg" + exe); dir.Touch("cfg/bin/ffprobe" + exe);
        dir.Touch("app/ffmpeg/ffmpeg" + exe); dir.Touch("app/ffmpeg/ffprobe" + exe);

        Assert.Equal(Path.Combine(dir.File("cfg"), "bin", "ffmpeg" + exe), FfmpegLocator.Locate(dir.File("cfg"), dir.File("app"), "")!.Ffmpeg);
        Assert.Equal(Path.Combine(dir.File("app"), "ffmpeg", "ffprobe" + exe), FfmpegLocator.Locate(null, dir.File("app"), "")!.Ffprobe);
        Assert.Null(FfmpegLocator.Locate(null, dir.File("nowhere"), ""));
    }
}
