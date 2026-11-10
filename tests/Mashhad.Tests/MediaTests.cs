using Mashhad.Core;
using Mashhad.Core.Media;

namespace Mashhad.Tests;

public class MediaTypesTests
{
    [Theory]
    [InlineData("a.JPG", MediaKind.Image)]
    [InlineData("a.heic", MediaKind.Image)]
    [InlineData("a.avif", MediaKind.Image)]
    [InlineData("a.webp", MediaKind.Image)]
    [InlineData("a.gif", MediaKind.AnimatedImage)]
    [InlineData("C:\\x\\movie.MKV", MediaKind.Video)]
    [InlineData("clip.mp4", MediaKind.Video)]
    [InlineData("song.flac", MediaKind.Audio)]
    [InlineData("notes.txt", MediaKind.Unknown)]
    [InlineData("noext", MediaKind.Unknown)]
    [InlineData("", MediaKind.Unknown)]
    public void Classifies_by_extension(string path, MediaKind expected) => Assert.Equal(expected, MediaTypes.Classify(path));

    [Fact]
    public void Extension_sets_do_not_overlap()
    {
        var all = MediaTypes.ImageExtensions.Concat(MediaTypes.AnimatedImageExtensions)
            .Concat(MediaTypes.VideoExtensions).Concat(MediaTypes.AudioExtensions).ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

public class NaturalComparerTests
{
    [Fact]
    public void Sorts_numbers_numerically()
    {
        var names = new[] { "img10.jpg", "img2.jpg", "IMG1.jpg", "img02.png", "a.mp4", "img2.avi", "10.jpg", "9.jpg" };
        var sorted = names.OrderBy(n => n, NaturalComparer.Instance).ToArray();
        Assert.Equal(new[] { "9.jpg", "10.jpg", "a.mp4", "IMG1.jpg", "img2.avi", "img2.jpg", "img02.png", "img10.jpg" }, sorted);
    }

    [Fact]
    public void Is_a_consistent_total_order()
    {
        var c = NaturalComparer.Instance;
        Assert.Equal(0, c.Compare("a1", "a1"));
        Assert.True(c.Compare("a1", "a01") != 0);
        Assert.Equal(-c.Compare("a1", "a01"), c.Compare("a01", "a1"));
        Assert.True(c.Compare("abc", "abcd") < 0);
        Assert.True(c.Compare("a99999999999999999999", "a100000000000000000000") < 0); // no overflow
    }
}

public class FolderScannerTests
{
    [Fact]
    public void Gallery_mixes_images_and_videos_in_natural_order()
    {
        using var dir = new TempDir();
        foreach (var n in new[] { "b10.mp4", "b2.jpg", "a.png", "notes.txt", "song.mp3", "c.gif", "sub.srt" }) dir.Touch(n);

        var (items, index) = FolderScanner.ScanAround(dir.File("b10.mp4"));

        Assert.Equal(new[] { "a.png", "b2.jpg", "b10.mp4", "c.gif" }, items.Select(i => i.FileName));
        Assert.Equal(2, index);
        Assert.Equal(MediaKind.Video, items[2].Kind);
    }

    [Fact]
    public void Opening_an_audio_file_lists_audio_siblings_too()
    {
        using var dir = new TempDir();
        foreach (var n in new[] { "1.mp3", "2.flac", "cover.jpg" }) dir.Touch(n);
        var (items, index) = FolderScanner.ScanAround(dir.File("2.flac"));
        Assert.Equal(3, items.Count);
        Assert.Equal("2.flac", items[index].FileName);
    }

    [Fact]
    public void Unknown_extension_is_still_included_when_opened_directly()
    {
        using var dir = new TempDir();
        dir.Touch("a.jpg");
        var odd = dir.Touch("stream.bin");
        var (items, index) = FolderScanner.ScanAround(odd);
        Assert.Equal(odd, items[index].Path);
    }

    [Fact]
    public void Sorts_by_size_and_date()
    {
        using var dir = new TempDir();
        dir.Touch("big.jpg", 300);
        dir.Touch("small.jpg", 10);
        dir.Touch("mid.mp4", 100);
        File.SetLastWriteTimeUtc(dir.File("big.jpg"), new DateTime(2020, 1, 1));
        File.SetLastWriteTimeUtc(dir.File("small.jpg"), new DateTime(2022, 1, 1));
        File.SetLastWriteTimeUtc(dir.File("mid.mp4"), new DateTime(2021, 1, 1));

        Assert.Equal(new[] { "small.jpg", "mid.mp4", "big.jpg" }, FolderScanner.Scan(dir.Path, GallerySort.Size).Select(i => i.FileName));
        Assert.Equal(new[] { "small.jpg", "mid.mp4", "big.jpg" },
            FolderScanner.Scan(dir.Path, GallerySort.DateModified, descending: true).Select(i => i.FileName));
    }

    [Fact]
    public void Missing_folder_gives_empty_gallery() =>
        Assert.Empty(FolderScanner.Scan(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid())));
}

public class MediaQueueTests
{
    private static MediaQueue Make(int n, int index = 0, int? seed = 1)
    {
        var q = new MediaQueue(seed);
        q.Load(Enumerable.Range(0, n).Select(i => new MediaItem($"/m/{i}.jpg", MediaKind.Image)), index, QueueSource.Folder);
        return q;
    }

    [Fact]
    public void Next_and_previous_wrap_by_default()
    {
        var q = Make(3, 2);
        Assert.Equal("0.jpg", q.Next()!.FileName);
        Assert.Equal("2.jpg", q.Previous()!.FileName);
    }

    [Fact]
    public void Without_wrap_the_ends_are_hard()
    {
        var q = Make(3, 2);
        q.Wrap = false;
        Assert.Null(q.Next());
        Assert.Equal(2, q.CurrentIndex);
        q.First();
        Assert.Null(q.Previous());
        Assert.Null(q.Peek(-1));
        Assert.Equal("1.jpg", q.Peek(1)!.FileName);
    }

    [Fact]
    public void Single_item_never_navigates_to_itself()
    {
        var q = Make(1);
        Assert.Null(q.Next());
        Assert.Null(q.Previous());
    }

    [Fact]
    public void Empty_queue_is_safe()
    {
        var q = new MediaQueue();
        Assert.Null(q.Current);
        Assert.Null(q.Next());
        Assert.Equal(-1, q.PeekIndex(1));
    }

    [Fact]
    public void Shuffle_visits_every_item_once_starting_from_current()
    {
        var q = Make(20, 7);
        q.Shuffle = true;
        var seen = new List<int> { q.CurrentIndex };
        for (int i = 0; i < 19; i++)
        {
            Assert.NotNull(q.Next());
            seen.Add(q.CurrentIndex);
        }
        Assert.Equal(7, seen[0]);
        Assert.Equal(Enumerable.Range(0, 20), seen.OrderBy(x => x));
        Assert.NotEqual(Enumerable.Range(7, 13).Concat(Enumerable.Range(0, 7)), seen); // actually shuffled
        Assert.Equal(7, q.Next() is not null ? q.CurrentIndex : -1); // wraps to the start of the order
    }

    [Fact]
    public void Removing_items_keeps_the_current_item_stable()
    {
        var q = Make(5, 3);
        q.RemoveAt(1);
        Assert.Equal("3.jpg", q.Current!.FileName);
        q.RemoveAt(q.CurrentIndex);
        Assert.Equal("4.jpg", q.Current!.FileName);
        q.RemoveAt(q.CurrentIndex);
        Assert.Equal("2.jpg", q.Current!.FileName);
    }
}

public class PlaylistAndOpenTests
{
    [Fact]
    public void M3u_resolves_relative_paths_and_skips_comments()
    {
        using var dir = new TempDir();
        var items = PlaylistFile.Parse(new[] { "\uFEFF#EXTM3U", "#EXTINF:1,x", "sub/a.mp4", "", "https://example.com/live.m3u8", dir.File("b.jpg") }, dir.Path);
        Assert.Equal(3, items.Count);
        Assert.Equal(Path.GetFullPath(Path.Combine(dir.Path, "sub/a.mp4")), items[0].Path);
        Assert.Equal("https://example.com/live.m3u8", items[1].Path);
        Assert.Equal(MediaKind.Image, items[2].Kind);
    }

    [Fact]
    public void M3u_round_trips()
    {
        using var dir = new TempDir();
        var list = new[] { new MediaItem(dir.File("فيديو 1.mp4"), MediaKind.Video), new MediaItem(dir.File("b.png"), MediaKind.Image) };
        PlaylistFile.Write(dir.File("p.m3u8"), list);
        Assert.Equal(list, PlaylistFile.Read(dir.File("p.m3u8")));
    }

    [Fact]
    public void Single_file_opens_its_folder_gallery_and_keeps_subtitles_aside()
    {
        using var dir = new TempDir();
        dir.Touch("a.jpg"); dir.Touch("b.mkv"); dir.Touch("b.srt");
        var r = OpenRequest.Resolve(new[] { dir.File("b.mkv"), dir.File("b.srt") });
        Assert.Equal(QueueSource.Folder, r.Source);
        Assert.Equal(2, r.Items.Count);
        Assert.Equal("b.mkv", r.Items[r.Index].FileName);
        Assert.Equal(new[] { dir.File("b.srt") }, r.Subtitles);
    }

    [Fact]
    public void Several_files_become_a_playlist_in_the_given_order()
    {
        using var dir = new TempDir();
        dir.Touch("a.jpg"); dir.Touch("b.mkv"); dir.Touch("c.png"); dir.Touch("x.txt");
        var r = OpenRequest.Resolve(new[] { dir.File("c.png"), dir.File("a.jpg"), dir.File("x.txt") });
        Assert.Equal(QueueSource.Playlist, r.Source);
        Assert.Equal(new[] { "c.png", "a.jpg" }, r.Items.Select(i => i.FileName));
    }

    [Fact]
    public void Folder_opens_as_gallery_and_missing_file_as_empty()
    {
        using var dir = new TempDir();
        dir.Touch("a.jpg"); dir.Touch("b.mkv");
        Assert.Equal(2, OpenRequest.Resolve(new[] { dir.Path }).Items.Count);
        Assert.Equal(-1, OpenRequest.Resolve(new[] { dir.File("nope.mp4") }).Index);
    }
}

public class PreloaderTests
{
    [Fact]
    public async Task Warms_whole_images_but_only_head_and_tail_of_videos()
    {
        using var dir = new TempDir();
        var image = new MediaItem(dir.Touch("a.jpg", 5000), MediaKind.Image);
        var video = new MediaItem(dir.Touch("b.mp4", 10_000), MediaKind.Video);
        using var pre = new Preloader { VideoHeadBytes = 1000, VideoTailBytes = 500 };
        var warmed = new Dictionary<string, long>();
        pre.Warmed += (p, n) => { lock (warmed) warmed[p] = n; };

        await pre.Preload(image, null, video);

        Assert.Equal(5000, warmed[image.Path]);
        Assert.Equal(1500, warmed[video.Path]);
    }

    [Fact]
    public async Task Missing_files_and_urls_are_ignored()
    {
        using var pre = new Preloader();
        await pre.Preload(new MediaItem("/nope/x.jpg", MediaKind.Image), new MediaItem("https://e.com/v.mp4", MediaKind.Video));
    }
}

public class CommandLineTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Mashhad\\Mashhad.exe\" \"D:\\My Videos\\a b.mp4\" c.jpg",
        new[] { "C:\\Program Files\\Mashhad\\Mashhad.exe", "D:\\My Videos\\a b.mp4", "c.jpg" })]
    [InlineData("a \"b\\\\\" c", new[] { "a", "b\\", "c" })]
    [InlineData("a \"say \\\"hi\\\"\"", new[] { "a", "say \"hi\"" })]
    [InlineData("  ", new string[0])]
    [InlineData("\"\" x", new[] { "", "x" })]
    public void Splits_like_windows(string line, string[] expected) => Assert.Equal(expected, CommandLine.Split(line));

    [Fact]
    public void Media_arguments_drop_exe_and_switches() =>
        Assert.Equal(new[] { "D:\\a.mp4" }, CommandLine.MediaArguments(new[] { "C:\\x\\Mashhad.exe", "--smoke-test", "D:\\a.mp4", "" }));
}
