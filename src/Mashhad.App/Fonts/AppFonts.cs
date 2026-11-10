using System.Security.Cryptography;
using Mashhad.Core.Fonts;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Mashhad.App.Fonts;

/// <summary>
/// The build key for the encrypted UI font. CI generates the implementation
/// (Generated/FontKey.g.cs, never committed) from the MASHHAD_FONT_KEY secret, in an obfuscated
/// form. Without it — local and fork builds — the partial method is empty and the key is null.
/// </summary>
internal static partial class FontKeySource
{
    static partial void Load(ref byte[]? key);

    public static byte[]? Get()
    {
        byte[]? key = null;
        Load(ref key);
        return key;
    }
}

/// <summary>
/// UI fonts: Thmanyah Sans (Arabic and Latin) when the build carries it, otherwise Segoe UI
/// Variable; Consolas for timecodes. The licensed font is embedded encrypted, decrypted in
/// memory, and handed to XAML through randomly named files in a private per-user cache folder
/// (XAML only loads custom fonts from files). Nothing is written to the install folder, and
/// the files are deleted on exit (and any left by a crash on the next start).
/// </summary>
public static class AppFonts
{
    public const string FallbackFamily = "Segoe UI Variable Text, Segoe UI";
    private static readonly string[] Weights = { "Light", "Regular", "Medium", "Bold", "Black" };

    public static FontFamily Mono { get; } = new("Consolas");
    public static FontFamily Regular { get; private set; } = new(FallbackFamily);
    public static FontFamily Medium { get; private set; } = new(FallbackFamily);
    public static FontFamily Bold { get; private set; } = new(FallbackFamily);
    public static FontFamily Light { get; private set; } = new(FallbackFamily);
    /// <summary>True when Thmanyah Sans is in use.</summary>
    public static bool Branded { get; private set; }

    private static string? _dir;

    private static string CacheRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mashhad", "Cache", "r");

    /// <summary>Call once on the UI thread before any window is created.</summary>
    public static async Task InitializeAsync()
    {
        RemoveStaleFiles();
        byte[]? key = null;
        try { key = FontKeySource.Get(); } catch { key = null; }

        if (key is null)
        {
            Log.Write("FONT: fallback (this build has no font key) -> Segoe UI Variable");
        }
        else
        {
            try { await LoadBrandedAsync(key); }
            catch (Exception e)
            {
                Log.Write($"FONT: fallback (Thmanyah Sans unavailable: {e.GetType().Name}: {e.Message}) -> Segoe UI Variable");
                UseFallback();
                Cleanup();
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        ApplyToResources();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
    }

    /// <summary>Sets the family and a matching weight on a text block.</summary>
    public static void Style(TextBlock text, bool strong, double size)
    {
        if (strong && size >= 20) { text.FontFamily = Bold; text.FontWeight = FontWeights.Bold; }
        else if (strong) { text.FontFamily = Medium; text.FontWeight = Branded ? FontWeights.Medium : FontWeights.SemiBold; }
        else { text.FontFamily = Regular; text.FontWeight = FontWeights.Normal; }
    }

    /// <summary>Timecodes and other aligned numbers.</summary>
    public static TextBlock Monospace(TextBlock text) { text.FontFamily = Mono; return text; }

    private static async Task LoadBrandedAsync(byte[] key)
    {
        var assembly = typeof(AppFonts).Assembly;
        _dir = Path.Combine(CacheRoot, RandomName());
        var dir = Directory.CreateDirectory(_dir);
        dir.Attributes |= FileAttributes.Hidden | FileAttributes.NotContentIndexed;

        var faces = new Dictionary<string, (string Path, OpenTypeInfo Info)>();
        foreach (var weight in Weights)
        {
            using var stream = assembly.GetManifestResourceStream($"Mashhad.Fonts.thmanyahsans-{weight}.otf.enc")
                ?? throw new FileNotFoundException($"embedded font {weight} missing");
            var blob = new byte[stream.Length];
            stream.ReadExactly(blob);
            var plain = FontCrypto.Decrypt(blob, key);
            try
            {
                var info = OpenTypeInfo.Read(plain);
                var path = Path.Combine(_dir, RandomName() + ".otf");
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    file.Write(plain);
                File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.NotContentIndexed);
                faces[weight] = (path, info);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }

        // XAML accepts a font file path with "#Family"; which family name it matches depends on how
        // DirectWrite groups the face. As a second route the files are also registered privately
        // for this process. Custom fonts load asynchronously, so measure the candidates until one
        // renders differently from the fallback.
        var regular = faces["Regular"];
        var forms = new (Func<string, string, string> Form, string Label)[]
        {
            ((path, name) => $"{path}#{name}", "path"),
            ((path, name) => $"{new Uri(path).AbsoluteUri}#{name}", "uri"),
            ((path, name) => name, "private"),
        };
        var names = new (Func<OpenTypeInfo, string> Name, string Label)[]
        {
            (i => i.PreferredFamily, "typographic"),
            (i => i.Family, "family"),
            (i => i.FullName, "full"),
        };
        foreach (var face in faces.Values)
        {
            if (AddFontResourceExW(face.Path, FR_PRIVATE, IntPtr.Zero) > 0) Registered.Add(face.Path);
        }
        Candidates.Clear();
        foreach (var (form, formLabel) in forms)
            foreach (var (name, nameLabel) in names)
            {
                var family = name(regular.Info);
                if (family.Length == 0 || Candidates.Any(c => c.Label.StartsWith(formLabel + "/") && c.Source == form(regular.Path, family))) continue;
                Candidates.Add(($"{formLabel}/{nameLabel}", form(regular.Path, family)));
            }

        // Unrendered families fall back to the system font glyph by glyph, so applying this before
        // it is confirmed is harmless; VerifyAsync confirms it once the window is up.
        (Func<string, string, string> Form, Func<OpenTypeInfo, string> Name, string Label)? chosen = (forms[0].Form, names[0].Name, "path/typographic");
        _applied = forms[0].Form(regular.Path, names[0].Name(regular.Info));
        await Task.CompletedTask;

        var (f, n, label) = chosen.Value;
        FontFamily Make(string weight)
        {
            var face = faces[weight];
            var familyName = n(face.Info);
            return new FontFamily(f(face.Path, familyName.Length > 0 ? familyName : face.Info.Family));
        }
        Light = Make("Light");
        Regular = Make("Regular");
        Medium = Make("Medium");
        Bold = Make("Bold");
        Branded = true;
        Log.Write($"FONT: thmanyah applied ({regular.Info.PreferredFamily}; {label}; weights {string.Join("/", Weights.Select(w => faces[w].Info.Weight))}; {Registered.Count} privately registered)");
    }

    private static readonly List<(string Label, string Source)> Candidates = new();
    private static string? _applied;

    /// <summary>
    /// Confirms in the live window that XAML renders the font (custom fonts load asynchronously)
    /// and logs how each way of referring to it fares. Probes are invisible and removed afterwards.
    /// </summary>
    public static async Task VerifyAsync(Panel host)
    {
        if (!Branded) return;
        var probes = new List<(string Label, string Source, TextBlock Probe)>
        {
            ("missing", "Mashhad Missing Font", Probe(new FontFamily("Mashhad Missing Font"))),
            ("consolas", "Consolas", Probe(new FontFamily("Consolas"))),
        };
        foreach (var (label, source) in Candidates) probes.Add((label, source, Probe(new FontFamily(source))));
        var panel = new StackPanel { Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        foreach (var p in probes) panel.Children.Add(p.Probe);
        host.Children.Add(panel);
        try
        {
            string? works = null;
            for (int i = 0; i < 30 && works is null; i++)
            {
                await Task.Delay(100);
                double missing = probes[0].Probe.ActualWidth;
                works = probes.Skip(2).Where(p => Math.Abs(p.Probe.ActualWidth - missing) > 0.5).Select(p => p.Label).FirstOrDefault();
            }
            string widths = string.Join(", ", probes.Select(p => $"{p.Label}={p.Probe.ActualWidth:0.#}"));
            var applied = probes.FirstOrDefault(p => p.Source == _applied);
            bool ok = applied.Probe is not null && Math.Abs(applied.Probe.ActualWidth - probes[0].Probe.ActualWidth) > 0.5;
            Log.Write(ok
                ? $"FONT: thmanyah verified in the window via {applied.Label} ({widths})"
                : $"FONT: thmanyah NOT rendered via {applied.Label}; works: {works ?? "none"} ({widths})");
        }
        finally { host.Children.Remove(panel); }
    }

    private static TextBlock Probe(FontFamily family) =>
        new() { Text = "Mashhad مشهد Hamburgefonstiv 0123456789", FontSize = 48, FontFamily = family };


    private static void UseFallback()
    {
        Branded = false;
        Regular = Medium = Bold = Light = new FontFamily(FallbackFamily);
    }

    private static void ApplyToResources()
    {
        var resources = Application.Current.Resources;
        resources["ContentControlThemeFontFamily"] = Regular;
        try
        {
            var style = new Microsoft.UI.Xaml.Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, Regular));
            resources[typeof(TextBlock)] = style;
        }
        catch (Exception e) { Log.Write("FONT: implicit text style not set: " + e.Message); }
    }

    /// <summary>Deletes this run's decrypted font files (best effort; XAML may still hold them).</summary>
    public static void Cleanup()
    {
        foreach (var path in Registered) RemoveFontResourceExW(path, FR_PRIVATE, IntPtr.Zero);
        Registered.Clear();
        if (_dir is null) return;
        try { Directory.Delete(_dir, recursive: true); _dir = null; }
        catch { /* still mapped by the text stack; removed on the next start */ }
    }

    private static void RemoveStaleFiles()
    {
        try
        {
            if (!Directory.Exists(CacheRoot)) return;
            foreach (var stale in Directory.EnumerateDirectories(CacheRoot))
            {
                try { Directory.Delete(stale, recursive: true); } catch { }
            }
        }
        catch { }
    }

    private static readonly List<string> Registered = new();
    private const uint FR_PRIVATE = 0x10;
    [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int AddFontResourceExW(string path, uint flags, IntPtr reserved);
    [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool RemoveFontResourceExW(string path, uint flags, IntPtr reserved);

    private static string RandomName() => Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
}
