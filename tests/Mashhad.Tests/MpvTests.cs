using Mashhad.Core.Mpv;
using Mashhad.Core.Settings;

namespace Mashhad.Tests;

public class MpvOptionsTests
{
    [Fact]
    public void Defaults_are_embedded_hardware_decoded_and_self_contained()
    {
        var o = MpvOptions.Build(new AppSettings(), 0x1234, "/shots").ToDictionary(k => k.Key, v => v.Value);
        Assert.Equal("d3d11va", o["hwdec"]);
        Assert.Equal("4660", o["wid"]);
        Assert.Equal("no", o["config"]);
        Assert.Equal("no", o["input-default-bindings"]);
        Assert.Equal("inf", o["image-display-duration"]);
        Assert.Equal("yes", o["keep-open"]);
        Assert.Equal("/shots", o["screenshot-directory"]);
        Assert.DoesNotContain("wid", MpvOptions.Build(new AppSettings(), null, "/s").Select(k => k.Key));
    }

    [Fact]
    public void Track_list_json_is_parsed()
    {
        const string json = """
        [{"id":1,"type":"video","codec":"h264","selected":true,"image":false},
         {"id":1,"type":"audio","lang":"ara","title":"Stereo","codec":"aac","default":true,"selected":true},
         {"id":2,"type":"sub","lang":"eng","external":true,"codec":"subrip"}]
        """;
        var tracks = MpvPlayer.ParseTracks(json);
        Assert.Equal(3, tracks.Count);
        Assert.Equal("#1 ara Stereo (aac)", tracks[1].DisplayName);
        Assert.True(tracks[2].External);
        Assert.False(tracks[2].Selected);
        Assert.Empty(MpvPlayer.ParseTracks("not json"));
        Assert.Empty(MpvPlayer.ParseTracks(null));
    }
}

/// <summary>
/// Drives a real libmpv (headless: vo=null, ao=null) through the interop layer. Skipped when
/// libmpv or ffmpeg is not installed.
/// </summary>
public class MpvIntegrationTests
{
    private static MpvPlayer Create()
    {
        Skip.IfNot(MpvPlayer.IsAvailable(out var error), "libmpv not found: " + error);
        Skip.If(Tools.Paths is null, "ffmpeg not found (needed to make test media)");
        var options = MpvOptions.Build(new AppSettings { HardwareDecoding = "no" }, null, Path.GetTempPath());
        options.Add(new("vo", "null"));
        options.Add(new("ao", "null"));
        return new MpvPlayer(options, MpvOptions.BuildOptional(embedded: false));
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 10_000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return true;
            Thread.Sleep(20);
        }
        return condition();
    }

    [SkippableFact]
    public void Plays_a_video_reports_properties_seeks_and_reaches_eof()
    {
        using var mpv = Create();
        using var dir = new TempDir();
        var video = Tools.MakeSampleVideo(dir.File("فيديو تجريبي.mp4"), seconds: 3); // non-ASCII path

        var loaded = new ManualResetEventSlim();
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, object?>();
        mpv.FileLoaded += loaded.Set;
        mpv.PropertyChanged += (name, value) => seen[name] = value;
        mpv.Observe("duration", MpvFormat.Double);
        mpv.Observe("time-pos", MpvFormat.Double);
        mpv.Observe("pause", MpvFormat.Flag);
        mpv.Observe("eof-reached", MpvFormat.Flag);
        mpv.Observe("path", MpvFormat.String);

        mpv.Set("pause", true);
        mpv.Command("loadfile", video);
        Assert.True(loaded.Wait(10_000), "file-loaded event never arrived");

        Assert.True(WaitFor(() => seen.TryGetValue("duration", out var d) && d is double v && Math.Abs(v - 3) < 0.2));
        Assert.Equal(video, mpv.GetString("path"));
        Assert.Equal(video, seen["path"]);
        Assert.Equal(320, mpv.GetLong("width"));
        Assert.True(mpv.GetFlag("pause"));
        Assert.Equal(true, seen["pause"]);

        var tracks = mpv.GetTracks();
        Assert.Contains(tracks, t => t.Type == "video" && t.Codec == Tools.VideoCodec && t.Selected);
        Assert.Contains(tracks, t => t.Type == "audio");

        mpv.Command("seek", "1.5", "absolute+exact");
        Assert.True(WaitFor(() => mpv.GetDouble("time-pos") is double t && Math.Abs(t - 1.5) < 0.1));
        Thread.Sleep(300); // let the exact seek settle on its frame (1.48 or 1.52)
        double before = mpv.GetDouble("time-pos")!.Value;
        mpv.Command("frame-step");
        Assert.True(WaitFor(() => mpv.GetDouble("time-pos") is double t && t > before + 0.03 && t < before + 0.09),
            "frame-step should advance exactly one 40 ms frame");

        Assert.True(mpv.Set("speed", 4.0));
        Assert.Equal(4.0, mpv.GetDouble("speed"));
        mpv.Set("pause", false);
        Assert.True(WaitFor(() => seen.TryGetValue("eof-reached", out var e) && e is true), "keep-open should stop at EOF");
        Assert.NotNull(mpv.GetString("path")); // still loaded: we decide what happens next
    }

    [SkippableFact]
    public void Resume_start_position_and_ab_loop_are_honoured()
    {
        using var mpv = Create();
        using var dir = new TempDir();
        var video = Tools.MakeSampleVideo(dir.File("v.mkv"), seconds: 4);
        var loaded = new ManualResetEventSlim();
        mpv.FileLoaded += loaded.Set;

        mpv.Set("pause", true);
        Assert.True(mpv.Set("start", "2.0"));       // how the app resumes: version-independent
        mpv.Command("loadfile", video);
        Assert.True(loaded.Wait(10_000));
        mpv.Set("start", "none");
        Assert.True(WaitFor(() => mpv.GetDouble("time-pos") is double t && Math.Abs(t - 2.0) < 0.15));

        mpv.Command("ab-loop");                     // A at 2.0
        Assert.Equal(2.0, mpv.GetDouble("ab-loop-a")!.Value, 1);
        mpv.Command("seek", "3", "absolute+exact");
        Assert.True(WaitFor(() => mpv.GetDouble("time-pos") is double t && Math.Abs(t - 3) < 0.1));
        mpv.Command("ab-loop");                     // B at 3.0
        Assert.Equal(3.0, mpv.GetDouble("ab-loop-b")!.Value, 1);
        mpv.Command("ab-loop");                     // cleared
        Assert.Equal("no", mpv.GetString("ab-loop-a"));
    }

    [SkippableFact]
    public void Shows_still_images_and_applies_zoom_pan_rotate()
    {
        using var mpv = Create();
        using var dir = new TempDir();
        var image = Tools.MakeSampleImage(dir.File("photo.png"), "640x480");
        var loaded = new ManualResetEventSlim();
        mpv.FileLoaded += loaded.Set;

        mpv.Command("loadfile", image);
        Assert.True(loaded.Wait(10_000));
        Assert.True(WaitFor(() => mpv.GetLong("video-params/w") == 640));
        Assert.Equal(480, mpv.GetLong("video-params/h"));
        Assert.Contains(mpv.GetTracks(), t => t.Type == "video" && t.Image);

        Assert.True(mpv.Set("video-zoom", 1.5));
        Assert.True(mpv.Set("video-pan-x", 0.1));
        Assert.True(mpv.Set("video-rotate", "90"));
        Assert.Equal(1.5, mpv.GetDouble("video-zoom"));
        Assert.Equal(90, mpv.GetLong("video-rotate"));

        Thread.Sleep(300);
        Assert.Equal(image, mpv.GetString("path")); // image-display-duration=inf: it stays up
    }

    [SkippableFact]
    public void Errors_are_reported_not_thrown_across_the_native_boundary()
    {
        using var mpv = Create();
        Assert.Throws<MpvException>(() => mpv.Command("no-such-command"));
        Assert.False(mpv.TryCommand("no-such-command"));
        Assert.False(mpv.Set("no-such-property", "1"));
        Assert.Null(mpv.GetDouble("duration"));     // nothing loaded
        Assert.Throws<MpvException>(() => new MpvPlayer(new[] { new KeyValuePair<string, string>("no-such-option", "1") }));

        var ended = new ManualResetEventSlim();
        MpvEndFileReason? reason = null;
        mpv.EndFile += r => { reason = r; ended.Set(); };
        mpv.Command("loadfile", "/definitely/not/here.mp4");
        Assert.True(ended.Wait(10_000));
        Assert.Equal(MpvEndFileReason.Error, reason);
    }

    [SkippableFact]
    public void Client_messages_round_trip_and_dispose_is_clean()
    {
        var mpv = Create();
        string[]? received = null;
        var got = new ManualResetEventSlim();
        mpv.ClientMessage += args => { received = args; got.Set(); };
        mpv.Command("script-message", "mashhad", "مرحبا");
        Assert.True(got.Wait(5000));
        Assert.Equal(new[] { "mashhad", "مرحبا" }, received);
        mpv.Dispose();
        mpv.Dispose(); // idempotent
        Assert.Null(mpv.GetString("path"));
        Assert.False(mpv.TryCommand("stop"));
    }
}
