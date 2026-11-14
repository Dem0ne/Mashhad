using Mashhad.Core.Imaging;
using Mashhad.Core.Mpv;
using Mashhad.Core.Settings;

namespace Mashhad.Tests;

public class MpvCommandCompatibilityTests
{
    /// <summary>The exact mpv commands and properties the app relies on must be accepted by libmpv.</summary>
    [SkippableFact]
    public void Commands_and_properties_used_by_the_app_are_accepted()
    {
        Skip.IfNot(MpvPlayer.IsAvailable(out var error), "libmpv not found: " + error);
        var options = MpvOptions.Build(new AppSettings(), null, Path.GetTempPath()); // default settings incl. hwdec=d3d11va
        options.Add(new("vo", "null"));
        options.Add(new("ao", "null"));
        using var mpv = new MpvPlayer(options, MpvOptions.BuildOptional(embedded: true));

        string ass = CropMath.BuildOverlayAss(800, 600, new RectD(100, 100, 300, 200));
        Assert.True(mpv.TryCommand("osd-overlay", "1", "ass-events", ass, "800", "600"));
        Assert.True(mpv.TryCommand("osd-overlay", "1", "none", ""));
        Assert.True(mpv.TryCommand("show-text", "مرحبا", "1500"));

        foreach (var (name, value) in new[]
                 {
                     ("video-zoom", "0.5"), ("video-pan-x", "0.1"), ("video-pan-y", "-0.1"), ("video-rotate", "270"),
                     ("loop-file", "inf"), ("loop-file", "no"), ("ab-loop-a", "no"), ("ab-loop-b", "no"), ("start", "12.5"),
                     ("start", "none"), ("hwdec", "no"), ("hwdec", "d3d11va"), ("screenshot-format", "webp"),
                     ("screenshot-directory", Path.GetTempPath()), ("aid", "no"), ("sid", "no"), ("mute", "yes"),
                 })
            Assert.True(mpv.Set(name, value), $"{name}={value} rejected");

        foreach (var name in new[]
                 {
                     "time-pos", "duration", "pause", "eof-reached", "volume", "mute", "speed", "ab-loop-a", "ab-loop-b",
                     "path", "track-list",
                 })
            mpv.Observe(name, name == "path" || name == "track-list" ? MpvFormat.String : MpvFormat.Double);
    }
}
