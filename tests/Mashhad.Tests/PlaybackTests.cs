using Mashhad.Core.Imaging;
using Mashhad.Core.Input;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Mashhad.Core.Settings;

namespace Mashhad.Tests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(59.9, "00:59")]
    [InlineData(61, "01:01")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "00:00")]
    [InlineData(double.NaN, "00:00")]
    public void Clock(double s, string expected) => Assert.Equal(expected, TimeFormat.Clock(s));

    [Fact]
    public void Clock_with_millis_and_file_stamp()
    {
        Assert.Equal("01:01.250", TimeFormat.Clock(61.25, millis: true));
        Assert.Equal("1:00:00.000", TimeFormat.Clock(3599.9996, millis: true)); // rounds up cleanly
        Assert.Equal("01.02.03.450", TimeFormat.FileStamp(3723.45));
    }

    [Theory]
    [InlineData("90", 90.0)]
    [InlineData("1:30", 90.0)]
    [InlineData("1:02:03.5", 3723.5)]
    public void Parses(string text, double expected) => Assert.Equal(expected, TimeFormat.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:2:3:4")]
    [InlineData("-3")]
    public void Rejects_malformed(string text) => Assert.Null(TimeFormat.Parse(text));
}

public class SpeedTests
{
    [Fact]
    public void Steps_and_clamps()
    {
        Assert.Equal(1.25, SpeedSteps.StepPreset(1.0, +1));
        Assert.Equal(0.75, SpeedSteps.StepPreset(1.0, -1));
        Assert.Equal(1.0, SpeedSteps.StepPreset(1.1, -1));
        Assert.Equal(4.0, SpeedSteps.StepPreset(4.0, +1));
        Assert.Equal(SpeedSteps.Max, SpeedSteps.Multiply(7.9, 1.1));
        Assert.Equal(1.1, SpeedSteps.Multiply(1.0, 1.1));
    }
}

public class ResumeStoreTests
{
    [Fact]
    public void Remembers_middle_positions_only()
    {
        var store = new ResumeStore();
        Assert.True(store.Update("a.mkv", 600, 3600));
        Assert.Equal(600, store.GetPosition("a.mkv"));
        Assert.False(store.Update("a.mkv", 3550, 3600));   // finished -> forgotten
        Assert.Null(store.GetPosition("a.mkv"));
        Assert.False(store.Update("b.mkv", 2, 3600));      // barely started
        Assert.False(store.Update("clip.mp4", 20, 30));    // too short to bother
    }

    [Fact]
    public void Persists_and_evicts_oldest()
    {
        using var dir = new TempDir();
        var file = dir.File("sub/resume.json");
        var store = new ResumeStore(file) { Capacity = 3 };
        for (int i = 0; i < 5; i++)
        {
            store.Update($"v{i}.mkv", 100 + i, 1000);
            Thread.Sleep(5);
        }
        store.Save();

        var reloaded = new ResumeStore(file);
        Assert.Equal(3, reloaded.Count);
        Assert.Null(reloaded.GetPosition("v0.mkv"));
        Assert.Equal(104, reloaded.GetPosition("v4.mkv"));
    }

    [Fact]
    public void Corrupt_file_is_ignored()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("r.json"), "{not json");
        Assert.Equal(0, new ResumeStore(dir.File("r.json")).Count);
    }
}

public class SlideshowTests
{
    [Fact]
    public void Images_wait_videos_play_to_the_end()
    {
        var p = new SlideshowPolicy { ImageDuration = TimeSpan.FromSeconds(3) };
        Assert.Equal((SlideshowAdvance.AfterDelay, TimeSpan.FromSeconds(3)), p.For(MediaKind.Image));
        Assert.Equal(SlideshowAdvance.AtEndOfFile, p.For(MediaKind.Video).Mode);
        Assert.Equal(SlideshowAdvance.AfterDelayLooping, p.For(MediaKind.AnimatedImage).Mode);
    }

    [Fact]
    public void Stops_at_the_end_unless_looping_and_restores_queue_wrap()
    {
        var q = new MediaQueue();
        q.Load(new[] { new MediaItem("a.jpg", MediaKind.Image), new MediaItem("b.mp4", MediaKind.Video) }, 1, QueueSource.Folder);
        var p = new SlideshowPolicy { Loop = false };
        Assert.Null(p.Advance(q));
        Assert.True(q.Wrap);
        p.Loop = true;
        Assert.Equal("a.jpg", p.Advance(q)!.FileName);
    }
}

public class ImageViewTests
{
    private static readonly Viewport Vp = new(1000, 500, 2000, 2000); // fit scale 0.25

    [Fact]
    public void Fit_centres_the_image()
    {
        var r = ImageView.Fit.DisplayRect(Vp);
        Assert.Equal(new RectD(250, 0, 500, 500), r);
        Assert.Equal(25, ImageView.Fit.Percent(Vp), 6);
    }

    [Fact]
    public void Actual_size_is_one_to_one()
    {
        var v = ImageView.ActualSize(Vp);
        Assert.Equal(2, v.Zoom, 9);
        Assert.Equal(100, v.Percent(Vp), 6);
    }

    [Theory]
    [InlineData(300, 100)]
    [InlineData(700, 450)]
    [InlineData(500, 250)]
    public void Zooming_keeps_the_point_under_the_cursor_fixed(double cx, double cy)
    {
        var before = new ImageView(1.5, 0.1, -0.05); // image larger than the window on both axes
        (double X, double Y) ImagePoint(ImageView v)
        {
            var r = v.DisplayRect(Vp);
            return ((cx - r.X) / v.Scale(Vp), (cy - r.Y) / v.Scale(Vp));
        }
        var after = before.ZoomAt(Vp, 0.5, cx, cy);
        Assert.Equal(2.0, after.Zoom, 9);
        Assert.Equal(ImagePoint(before).X, ImagePoint(after).X, 6);
        Assert.Equal(ImagePoint(before).Y, ImagePoint(after).Y, 6);
    }

    [Fact]
    public void Pan_is_clamped_so_the_image_cannot_leave_the_window()
    {
        var v = new ImageView(2, 0, 0).PanBy(Vp, 100_000, -100_000); // 2000x2000 px on a 1000x500 window
        var r = v.DisplayRect(Vp);
        Assert.Equal(0, r.X, 6);                 // left edge pinned to the window edge
        Assert.Equal(500, r.Bottom, 6);          // bottom edge pinned
        Assert.Equal(ImageView.Fit, ImageView.Fit.PanBy(Vp, 50, 50)); // smaller than window: stays centred
    }

    [Fact]
    public void Zoom_is_bounded()
    {
        Assert.Equal(ImageView.MaxZoom, new ImageView(5.9, 0, 0).ZoomBy(Vp, 3).Zoom);
        Assert.Equal(ImageView.MinZoom, ImageView.Fit.ZoomBy(Vp, -10).Zoom);
    }
}

public class CropMathTests
{
    private static readonly Viewport Vp = new(1000, 500, 2000, 1000); // fit scale 0.5, image fills the window

    [Fact]
    public void Window_selection_maps_to_image_pixels_in_any_drag_direction()
    {
        var a = CropMath.WindowToImage(Vp, ImageView.Fit, 100, 50, 600, 300);
        var b = CropMath.WindowToImage(Vp, ImageView.Fit, 600, 300, 100, 50);
        Assert.Equal(new PixelRect(200, 100, 1000, 500), a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Selection_is_clamped_to_the_image()
    {
        var letterboxed = new Viewport(1000, 1000, 2000, 1000); // image occupies y 250..750
        var r = CropMath.WindowToImage(letterboxed, ImageView.Fit, -50, 0, 2000, 1000);
        Assert.Equal(new PixelRect(0, 0, 2000, 1000), r);
        Assert.True(CropMath.WindowToImage(letterboxed, ImageView.Fit, 0, 0, 1000, 100).IsEmpty); // entirely in the black bar
    }

    [Fact]
    public void Round_trips_through_zoom_and_pan()
    {
        var view = new ImageView(1.3, 0.12, -0.2);
        var crop = new PixelRect(300, 200, 640, 360);
        var win = CropMath.ImageToWindow(Vp, view, crop);
        Assert.Equal(crop, CropMath.WindowToImage(Vp, view, win.X, win.Y, win.Right, win.Bottom));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(-90, 270)]
    [InlineData(450, 90)]
    [InlineData(180, 180)]
    public void Normalises_rotation(int input, int expected) => Assert.Equal(expected, CropMath.NormalizeRotation(input));

    [Fact]
    public void Rotated_size_swaps_on_quarter_turns()
    {
        Assert.Equal((480, 640), CropMath.RotatedSize(640, 480, 90));
        Assert.Equal((640, 480), CropMath.RotatedSize(640, 480, 180));
    }

    [Fact]
    public void Overlay_has_a_dimmed_surround_with_a_hole_and_a_frame()
    {
        var ass = CropMath.BuildOverlayAss(1000, 500, new RectD(100.4, 50, 400, 200)).Split('\n');
        Assert.Equal(2, ass.Length);
        Assert.Contains("m 0 0 l 1000 0 l 1000 500 l 0 500 m 100 50 l 100 250 l 500 250 l 500 50", ass[0]);
        Assert.Contains("\\3c&H7E5EFF&", ass[1]);
    }
}

public class ShortcutTests
{
    private readonly ShortcutMap _map = ShortcutMap.CreateDefault();

    [Fact]
    public void Arrows_seek_in_videos_and_browse_in_images()
    {
        var right = KeyGesture.Parse("Right");
        Assert.Equal(PlayerCommand.SeekForward, _map.Resolve(right, ShortcutContext.Timed));
        Assert.Equal(PlayerCommand.NextItem, _map.Resolve(right, ShortcutContext.Image));
        Assert.Equal(PlayerCommand.NextItem, _map.Resolve(KeyGesture.Parse("PageDown"), ShortcutContext.Timed));
    }

    [Fact]
    public void Edit_mode_overrides_only_its_own_keys()
    {
        var i = KeyGesture.Parse("I");
        Assert.Equal(PlayerCommand.SetIn, _map.Resolve(i, ShortcutContext.Edit, ShortcutContext.Timed));
        Assert.Equal(PlayerCommand.None, _map.Resolve(i, ShortcutContext.Timed));
        Assert.Equal(PlayerCommand.PlayPause, _map.Resolve(KeyGesture.Parse("Space"), ShortcutContext.Edit, ShortcutContext.Timed));
    }

    [Fact]
    public void Escape_cancels_crop_before_leaving_fullscreen()
    {
        var esc = KeyGesture.Parse("Escape");
        Assert.Equal(PlayerCommand.ToggleCrop, _map.Resolve(esc, ShortcutContext.Crop, ShortcutContext.Image));
        Assert.Equal(PlayerCommand.Back, _map.Resolve(esc, ShortcutContext.Image));
    }

    [Fact]
    public void Folder_view_owns_the_arrows_and_enter()
    {
        Assert.Equal(PlayerCommand.GalleryDown, _map.Resolve(KeyGesture.Parse("Down"), ShortcutContext.Folder));
        Assert.Equal(PlayerCommand.OpenFocused, _map.Resolve(KeyGesture.Parse("Enter"), ShortcutContext.Folder));
        Assert.Equal(PlayerCommand.ToggleSelection, _map.Resolve(KeyGesture.Parse("Space"), ShortcutContext.Folder));
        Assert.Equal(PlayerCommand.ToggleFolderPane, _map.Resolve(KeyGesture.Parse("F9"), ShortcutContext.Folder));
        Assert.Equal(PlayerCommand.ToggleEditMode, _map.Resolve(KeyGesture.Parse("Ctrl+E"), ShortcutContext.Folder));
    }

    [Fact]
    public void Gesture_parsing_and_formatting()
    {
        Assert.Equal(new KeyGesture("O", Ctrl: true, Shift: true), KeyGesture.Parse("ctrl+shift+o"));
        Assert.Equal(new KeyGesture("+", Ctrl: true), KeyGesture.Parse("Ctrl++"));
        Assert.Equal("Ctrl+Shift+O", KeyGesture.Parse("Shift+Ctrl+O").ToString());
        Assert.Throws<FormatException>(() => KeyGesture.Parse(""));
    }

    [Theory]
    [InlineData(0x20, "Space")]
    [InlineData(0x41, "A")]
    [InlineData(0x39, "9")]
    [InlineData(0x67, "7")]
    [InlineData(0x7A, "F11")]
    [InlineData(0xDB, "[")]
    [InlineData(0xBE, ".")]
    [InlineData(0xBB, "=")]
    public void Virtual_keys_map_to_names(int vk, string name) => Assert.Equal(name, KeyNames.FromVirtualKey(vk));

    [Fact]
    public void Every_binding_uses_a_key_the_keyboard_layer_can_produce()
    {
        var producible = Enumerable.Range(0, 256).Select(KeyNames.FromVirtualKey).Where(n => n is not null).ToHashSet();
        foreach (var (_, gesture, command) in _map.Bindings)
            Assert.True(producible.Contains(gesture.Key), $"{gesture} ({command}) can never be typed");
    }

    [Fact]
    public void Tooltip_gesture_lookup() => Assert.Equal("Ctrl+O", _map.GestureFor(PlayerCommand.OpenFile));
}

public class SettingsTests
{
    [Fact]
    public void Round_trips_and_sanitises()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.File("cfg/settings.json"));
        Assert.Equal("d3d11va", store.Current.HardwareDecoding); // hardware decoding is the default
        store.Current.Language = "ar";
        Assert.Equal(UiThemes.Default, store.Current.UiTheme); // the current dark theme stays the default
        store.Current.UiTheme = UiThemes.LiquidGlass;
        store.Current.Volume = 900;
        store.Save();

        var again = new SettingsStore(dir.File("cfg/settings.json"));
        Assert.Equal("ar", again.Current.Language);
        Assert.Equal(UiThemes.LiquidGlass, again.Current.UiTheme);
        Assert.Equal(150, again.Current.Volume);
    }

    [Fact]
    public void Corrupt_or_hostile_settings_fall_back_to_defaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("s.json"), "]]]");
        Assert.Equal("system", new SettingsStore(dir.File("s.json")).Current.Language);

        File.WriteAllText(dir.File("s2.json"), "{\"Language\":\"xx\",\"SlideshowSeconds\":0,\"ScreenshotFormat\":\"exe\",\"HardwareDecoding\":null}");
        var s = new SettingsStore(dir.File("s2.json")).Current;
        Assert.Equal(("system", 1.0, "png", "d3d11va"), (s.Language, s.SlideshowSeconds, s.ScreenshotFormat, s.HardwareDecoding));

        // An unknown theme, or settings written before themes existed (old "Theme" value), give the default.
        File.WriteAllText(dir.File("s3.json"), "{\"UiTheme\":\"neon\",\"Theme\":2}");
        Assert.Equal(UiThemes.Default, new SettingsStore(dir.File("s3.json")).Current.UiTheme);
    }
}
