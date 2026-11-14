using System.Buffers.Binary;
using System.Globalization;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Mashhad.Editing;

namespace Mashhad.Tests;

public class ImageHeaderTests
{
    [SkippableTheory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("gif")]
    [InlineData("bmp")]
    [InlineData("webp")]
    public void Reads_size_from_real_files(string ext)
    {
        Skip.If(Tools.Paths is null, "ffmpeg not found");
        using var dir = new TempDir();
        var path = dir.File("i." + ext);
        var codec = ext == "webp" ? new[] { "-c:v", "libwebp" } : Array.Empty<string>();
        Tools.Run(Tools.Paths!.Ffmpeg, new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=334x218:rate=1",
            "-frames:v", "1" }.Concat(codec).Concat(new[] { "-update", "1", path }).ToArray());
        Assert.Equal((334, 218), ImageHeader.TryRead(path));
    }

    [Fact]
    public void Jpeg_exif_orientation_swaps_width_and_height()
    {
        // SOI, APP1(Exif, big-endian TIFF, IFD0 with Orientation = 6), SOF0 400x300, EOI.
        var tiff = new byte[] { (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, 1, 0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0 };
        var app1 = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 });
        ms.Write(BE16(app1.Length + 2)); ms.Write(app1);
        ms.Write(new byte[] { 0xFF, 0xC0 }); ms.Write(BE16(17)); ms.WriteByte(8); ms.Write(BE16(300)); ms.Write(BE16(400));
        ms.Write(new byte[] { 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1 });
        ms.Write(new byte[] { 0xFF, 0xD9 });
        ms.Position = 0;
        Assert.Equal((300, 400), ImageHeader.TryRead(ms));
    }

    [Fact]
    public void Unknown_or_truncated_data_gives_null()
    {
        Assert.Null(ImageHeader.TryRead(new MemoryStream("hello, not an image"u8.ToArray())));
        Assert.Null(ImageHeader.TryRead(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16 })));
        Assert.Null(ImageHeader.TryRead("/no/such/file.jpg"));
    }

    private static byte[] BE16(int v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)v); return b; }
}

public class JustifiedLayoutTests
{
    [Fact]
    public void Rows_fill_the_width_exactly_except_the_last()
    {
        var aspects = new double[] { 1.5, 1.5, 0.75, 1.78, 1.33, 1, 1.5, 1.5, 1.78 };
        var rows = JustifiedLayout.Compute(aspects, 1000, 200, 8);
        Assert.Equal(aspects.Length, rows.Sum(r => r.Count));
        for (int i = 0; i < rows.Count - 1; i++)
        {
            var r = rows[i];
            double w = Enumerable.Range(r.Start, r.Count).Sum(k => JustifiedLayout.ItemWidth(aspects[k], r.Height)) + 8 * (r.Count - 1);
            Assert.Equal(1000, w, 6);
            Assert.InRange(r.Height, 100, 200);
        }
        Assert.Equal(200, rows[^1].Height);                         // last row keeps the target height
    }

    [Fact]
    public void Extreme_aspects_are_clamped_and_empty_input_is_safe()
    {
        Assert.Empty(JustifiedLayout.Compute(Array.Empty<double>(), 1000, 200, 8));
        Assert.Empty(JustifiedLayout.Compute(new[] { 1.0 }, 0, 200, 8));
        var rows = JustifiedLayout.Compute(new[] { 50.0, double.NaN, 0.001 }, 1000, 200, 8);
        Assert.Equal(3, rows.Sum(r => r.Count));
        Assert.Equal(JustifiedLayout.MaxAspect, JustifiedLayout.ClampAspect(50));
        Assert.Equal(1.5, JustifiedLayout.ClampAspect(double.NaN));
    }
}

public class GalleryModelTests
{
    private static GalleryModel Make(out List<GalleryEntry> entries)
    {
        var day1 = new DateTime(2026, 11, 8, 10, 0, 0);
        var day2 = new DateTime(2026, 11, 7, 18, 0, 0);
        entries = new List<GalleryEntry>();
        for (int i = 0; i < 7; i++) entries.Add(new GalleryEntry(new MediaItem($"/f/a{i}.jpg", MediaKind.Image), day1.AddMinutes(-i), 100));
        entries.Add(new GalleryEntry(new MediaItem("/f/v.mp4", MediaKind.Video), day2, 1000));
        for (int i = 0; i < 4; i++) entries.Add(new GalleryEntry(new MediaItem($"/f/b{i}.png", MediaKind.Image), day2.AddMinutes(-i - 1), 100));
        var m = new GalleryModel();
        m.Load(entries);
        return m;
    }

    [Fact]
    public void Groups_by_day_with_headers_before_tiles()
    {
        var m = Make(out _);
        var rows = m.Layout(900, 180, 8, CultureInfo.GetCultureInfo("en-US"), n => $"{n} items");
        Assert.Equal(GalleryRowKind.Header, rows[0].Kind);
        Assert.Contains("November", rows[0].Title);
        Assert.Contains("8", rows[0].Title);
        Assert.Equal("7 items", rows[0].Subtitle);
        int second = rows.ToList().FindIndex(1, r => r.Kind == GalleryRowKind.Header);
        Assert.Equal("5 items", rows[second].Subtitle);
        Assert.Equal(12, rows.Where(r => r.Kind == GalleryRowKind.Tiles).Sum(r => r.Count));
        Assert.All(rows.Where(r => r.Kind == GalleryRowKind.Tiles), r =>
            Assert.True(r.Start + r.Count <= (r.Group == 0 ? 7 : 12) && r.Start >= (r.Group == 0 ? 0 : 7)));  // rows never span days
    }

    [Fact]
    public void Filters_and_search()
    {
        var m = Make(out _);
        Assert.Equal((11, 1, 12), (m.PhotoCount, m.VideoCount, m.TotalCount));
        m.SetFilter(GalleryFilter.Videos, null);
        Assert.Equal(new[] { "v.mp4" }, m.Visible.Select(e => e.Item.FileName));
        m.SetFilter(GalleryFilter.Photos, "B");
        Assert.Equal(4, m.Visible.Count);
        Assert.Equal(-1, m.IndexOf("/f/v.mp4"));
        m.SetFilter(GalleryFilter.All, "");
        Assert.Equal(7, m.IndexOf("/f/v.mp4"));
    }

    [Fact]
    public void Arrow_navigation_moves_within_and_across_rows_and_days()
    {
        var m = Make(out _);
        m.Layout(900, 180, 8, CultureInfo.InvariantCulture);
        Assert.Equal(1, m.Move(0, 1, 0, 8));
        Assert.Equal(0, m.Move(0, -1, 0, 8));                       // clamps at the start
        Assert.Equal(11, m.Move(11, 1, 0, 8));                      // and at the end
        int down = m.Move(0, 0, 1, 8);
        Assert.True(m.RowOf(down) > m.RowOf(0));
        Assert.Equal(0, m.Move(down, 0, -1, 8));
        int last = m.Move(11, 0, 1, 8);                             // nothing below the last row
        Assert.Equal(11, last);
        Assert.Equal(0, m.Move(-1, 0, 1, 8));                       // no focus yet: first item
    }

    [Fact]
    public void Day_labels_are_gregorian_and_localised()
    {
        var date = new DateTime(2026, 11, 7);
        var en = GalleryModel.DayLabel(date, CultureInfo.GetCultureInfo("en-US"));
        Assert.Contains("2026", en);
        Assert.Contains("Saturday", en);
        var ar = GalleryModel.DayLabel(date, CultureInfo.GetCultureInfo("ar"));
        Assert.Contains("2026", ar);                                 // Gregorian year, not Hijri
        Assert.Contains(ar, c => c >= '\u0600' && c <= '\u06FF');
    }
}

public class AppModeTests
{
    [Fact]
    public void Modes_need_the_right_kind_of_item()
    {
        var video = new MediaItem("a.mp4", MediaKind.Video);
        var image = new MediaItem("a.jpg", MediaKind.Image);
        Assert.True(AppModes.CanEnter(AppMode.Folder, null));
        Assert.False(AppModes.CanEnter(AppMode.Play, null));
        Assert.True(AppModes.CanEnter(AppMode.Play, image));
        Assert.False(AppModes.CanEnter(AppMode.Edit, image));
        Assert.True(AppModes.CanEnter(AppMode.Edit, video));
        Assert.Equal(AppMode.Play, AppModes.Back(AppMode.Edit));
        Assert.Equal(AppMode.Folder, AppModes.Back(AppMode.Play));
    }
}

public class WaveformTests
{
    [Fact]
    public async Task Folds_samples_into_peak_buckets_across_odd_reads()
    {
        // 8 samples into 4 buckets: peaks are the max |sample| per pair.
        short[] samples = { 100, -16384, 0, 3276, -32768, 10, 8192, 8191 };
        var bytes = samples.SelectMany(v => BitConverter.GetBytes(v)).ToArray();
        var peaks = await Waveform.ComputeAsync(new OneByteStream(bytes), samples.Length, 4);
        Assert.Equal(new[] { 0.5f, 0.1f, 1.0f, 0.25f }, peaks.Select(p => (float)Math.Round(p, 2)));
    }

    [SkippableFact]
    public async Task Real_audio_through_ffmpeg()
    {
        Skip.If(Tools.Paths is null, "ffmpeg not found");
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("tone.mp4"), seconds: 3);
        var runner = new FfmpegRunner(Tools.Paths!);
        var peaks = await runner.RunWithOutputAsync(Waveform.Args(src), s => Waveform.ComputeAsync(s, 3 * Waveform.SampleRate, 100));
        Assert.Equal(100, peaks.Length);
        Assert.True(peaks.Skip(5).Take(90).All(p => p > 0.05), "a steady tone has energy in every bucket");
    }

    [SkippableFact]
    public async Task Thumbnail_frames_are_small_jpegs()
    {
        Skip.If(Tools.Paths is null, "ffmpeg not found");
        using var dir = new TempDir();
        var src = Tools.MakeSampleVideo(dir.File("v.mp4"), seconds: 3);
        var runner = new FfmpegRunner(Tools.Paths!);
        await runner.RunAsync(FfmpegArgs.Thumbnail(src, dir.File("t.jpg"), 1.5, 72));
        var probe = await runner.ProbeAsync(dir.File("t.jpg"));
        Assert.Equal(72, probe.MainVideo!.Height);
        Assert.Equal(96, probe.MainVideo.Width);
    }

    /// <summary>Returns one byte per read, to exercise sample reassembly across reads.</summary>
    private sealed class OneByteStream : MemoryStream
    {
        public OneByteStream(byte[] b) : base(b) { }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], ct);
    }
}
