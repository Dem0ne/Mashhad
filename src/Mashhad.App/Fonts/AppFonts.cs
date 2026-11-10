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
    public static void Initialize()
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
            try { LoadBranded(key); }
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

    private static void LoadBranded(byte[] key)
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
        // DirectWrite groups the face, so try the candidates and keep the first one that renders.
        var regular = faces["Regular"];
        double baseline = Width(new FontFamily("Mashhad Missing Font"));
        (Func<string, string, string> Form, Func<OpenTypeInfo, string> Name, string Label)? chosen = null;
        var forms = new (Func<string, string, string>, string)[]
        {
            ((path, name) => $"{path}#{name}", "path"),
            ((path, name) => $"{new Uri(path).AbsoluteUri}#{name}", "uri"),
        };
        var names = new (Func<OpenTypeInfo, string>, string)[]
        {
            (i => i.PreferredFamily, "typographic"),
            (i => i.Family, "family"),
            (i => i.FullName, "full"),
        };
        foreach (var (form, formLabel) in forms)
        {
            foreach (var (name, nameLabel) in names)
            {
                if (name(regular.Info).Length == 0) continue;
                double width = Width(new FontFamily(form(regular.Path, name(regular.Info))));
                if (baseline > 0 && Math.Abs(width - baseline) > 0.5) { chosen = (form, name, $"{formLabel}/{nameLabel}"); break; }
            }
            if (chosen is not null) break;
        }
        if (chosen is null)
        {
            if (baseline > 0) throw new InvalidOperationException("XAML did not render the decrypted font");
            chosen = (forms[0].Item1, names[0].Item1, "path/typographic (unverified)");
        }

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
        Log.Write($"FONT: thmanyah ({regular.Info.PreferredFamily}; {label}; weights {string.Join("/", Weights.Select(w => faces[w].Info.Weight))})");
    }

    private static double Width(FontFamily family)
    {
        var probe = new TextBlock { Text = "Mashhad مشهد Hamburgefonstiv 0123456789", FontSize = 48, FontFamily = family };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

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

    private static string RandomName() => Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
}
