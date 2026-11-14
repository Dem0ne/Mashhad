using System.Security.Cryptography;
using Mashhad.Core.Fonts;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
/// UI fonts: Segoe UI Variable for text, Consolas for timecodes.
///
/// Official builds also carry Thmanyah Sans, encrypted, as resources in the executable. At start-up
/// it is decrypted in memory only, to confirm the build's key and resources are intact. It is not
/// used yet: an unpackaged WinUI 3 app renders custom fonts only from its install folder or from
/// fonts installed for the user, and the font's licence rules out both, so the decrypted bytes are
/// discarded and nothing is ever written to disk.
/// </summary>
public static class AppFonts
{
    public const string FallbackFamily = "Segoe UI Variable Text, Segoe UI";
    private static readonly string[] Weights = { "Light", "Regular", "Medium", "Bold", "Black" };

    public static FontFamily Mono { get; } = new("Consolas");
    public static FontFamily Regular { get; } = new(FallbackFamily);
    /// <summary>True when the UI renders in Thmanyah Sans (not yet possible, see the class notes).</summary>
    public static bool Branded => false;

    /// <summary>Call once on the UI thread before any window is created.</summary>
    public static void Initialize()
    {
        Log.Write("FONT: " + CheckEmbeddedFonts() + " -> UI font Segoe UI Variable");
        var resources = Application.Current.Resources;
        resources["ContentControlThemeFontFamily"] = Regular;
    }

    /// <summary>Decrypts the embedded fonts in memory and reports the result (never the key).</summary>
    private static string CheckEmbeddedFonts()
    {
        byte[]? key = null;
        try { key = FontKeySource.Get(); } catch { key = null; }
        if (key is null) return "no font key in this build";
        try
        {
            var assembly = typeof(AppFonts).Assembly;
            var weights = new List<int>();
            string family = "";
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
                    family = info.PreferredFamily;
                    weights.Add(info.Weight);
                }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }
            return $"thmanyah decrypted in memory ({family}; weights {string.Join("/", weights)}), not applied";
        }
        catch (Exception e) { return $"embedded fonts could not be decrypted ({e.GetType().Name})"; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>Sets the family and a matching weight on a text block.</summary>
    public static void Style(TextBlock text, bool strong, double size)
    {
        text.FontFamily = Regular;
        text.FontWeight = strong && size >= 20 ? FontWeights.Bold : strong ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>Timecodes and other aligned numbers.</summary>
    public static TextBlock Monospace(TextBlock text) { text.FontFamily = Mono; return text; }
}
