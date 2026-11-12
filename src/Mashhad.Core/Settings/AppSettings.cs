using System.Text.Json;
using System.Text.Json.Serialization;
using Mashhad.Core.Media;

namespace Mashhad.Core.Settings;

/// <summary>The app's looks. The default is the dark crimson theme.</summary>
public static class UiThemes
{
    public const string Default = "default";
    public const string LiquidGlass = "liquid-glass";
    public static readonly IReadOnlyList<string> All = new[] { Default, LiquidGlass };
}

public sealed class AppSettings
{
    /// <summary>"system", "en" or "ar".</summary>
    public string Language { get; set; } = "system";
    /// <summary>One of <see cref="UiThemes"/>.</summary>
    public string UiTheme { get; set; } = UiThemes.Default;

    /// <summary>mpv <c>hwdec</c> value. D3D11VA keeps CPU and memory use low; "no" forces software decoding.</summary>
    public string HardwareDecoding { get; set; } = "d3d11va";
    public double Volume { get; set; } = 100;
    public bool Muted { get; set; }
    public bool ResumePlayback { get; set; } = true;
    public bool WrapGallery { get; set; } = true;
    /// <summary>Newest first, grouped by day, like a photo library.</summary>
    public GallerySort Sort { get; set; } = GallerySort.DateModified;
    public bool SortDescending { get; set; } = true;
    /// <summary>Keep the folder pane visible instead of letting it auto-hide (F9).</summary>
    public bool FolderPanePinned { get; set; } = true;
    /// <summary>Folders opened recently (most recent first), for the folder pane.</summary>
    public List<string> RecentFolders { get; set; } = new();
    public bool SingleInstance { get; set; } = true;

    public double SlideshowSeconds { get; set; } = 5;
    public bool SlideshowLoop { get; set; } = true;
    public bool SlideshowShuffle { get; set; }

    /// <summary>Empty = the user's Pictures\Mashhad folder.</summary>
    public string ScreenshotDirectory { get; set; } = "";
    public string ScreenshotFormat { get; set; } = "png";

    /// <summary>Empty = look next to the app, then on PATH.</summary>
    public string FfmpegDirectory { get; set; } = "";
    /// <summary>Empty = next to the source file.</summary>
    public string ExportDirectory { get; set; } = "";
    public bool MergeSegmentsOnExport { get; set; }
    public bool SnapCutsToKeyframes { get; set; } = true;

    public string PreferredAudioLanguages { get; set; } = "";
    public string PreferredSubtitleLanguages { get; set; } = "";

    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 760;

    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, SettingsStore.Json), SettingsStore.Json)!;
}

public sealed class SettingsStore
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string FilePath { get; }
    public AppSettings Current { get; private set; } = new();

    public SettingsStore(string filePath)
    {
        FilePath = filePath;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Current = new AppSettings(); // A corrupt settings file must never stop the app from starting.
        }
        Sanitize(Current);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Json));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static void Sanitize(AppSettings s)
    {
        s.Volume = double.IsNaN(s.Volume) ? 100 : Math.Clamp(s.Volume, 0, 150);
        s.SlideshowSeconds = double.IsNaN(s.SlideshowSeconds) ? 5 : Math.Clamp(s.SlideshowSeconds, 1, 3600);
        s.WindowWidth = Math.Clamp(s.WindowWidth, 480, 16000);
        s.WindowHeight = Math.Clamp(s.WindowHeight, 320, 16000);
        if (s.Language is not ("system" or "en" or "ar")) s.Language = "system";
        if (!UiThemes.All.Contains(s.UiTheme)) s.UiTheme = UiThemes.Default;
        if (s.ScreenshotFormat is not ("png" or "jpg" or "webp")) s.ScreenshotFormat = "png";
        if (string.IsNullOrWhiteSpace(s.HardwareDecoding)) s.HardwareDecoding = "d3d11va";
        s.ScreenshotDirectory ??= "";
        s.FfmpegDirectory ??= "";
        s.ExportDirectory ??= "";
        s.PreferredAudioLanguages ??= "";
        s.PreferredSubtitleLanguages ??= "";
        s.RecentFolders ??= new List<string>();
        s.RecentFolders = s.RecentFolders.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
    }
}

public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mashhad");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string ResumeFile => Path.Combine(DataDirectory, "resume.json");
    public static string LogFile => Path.Combine(DataDirectory, "mashhad.log");

    public static string DefaultScreenshotDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Mashhad");
}
