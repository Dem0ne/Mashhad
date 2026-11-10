using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Mashhad.Core.Localization;

/// <summary>
/// String tables embedded as JSON (English + Arabic). Kept in Core rather than in .resw files so
/// that key parity between languages is covered by unit tests.
/// </summary>
public sealed class Localizer
{
    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "en", "ar" };

    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, string> _fallback;

    public string Language { get; }
    public bool IsRightToLeft => Language == "ar";
    public CultureInfo Culture { get; }

    public Localizer(string language)
    {
        Language = SupportedLanguages.Contains(language) ? language : "en";
        Culture = CultureInfo.GetCultureInfo(Language);
        _fallback = LoadTable("en");
        _strings = Language == "en" ? _fallback : LoadTable(Language);
    }

    /// <summary>Picks the UI language: an explicit setting wins, otherwise the system UI language.</summary>
    public static string ResolveLanguage(string? setting, CultureInfo? systemCulture = null)
    {
        if (setting is not null && SupportedLanguages.Contains(setting)) return setting;
        var two = (systemCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        return SupportedLanguages.Contains(two) ? two : "en";
    }

    public string this[string key] => Get(key);

    public string Get(string key) =>
        _strings.TryGetValue(key, out var s) ? s : _fallback.TryGetValue(key, out var f) ? f : key;

    public string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);

    public bool Has(string key) => _strings.ContainsKey(key);

    public IReadOnlyCollection<string> Keys => _strings.Keys;

    public static Dictionary<string, string> LoadTable(string language)
    {
        var asm = typeof(Localizer).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith($"Strings.{language}.json", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
