using System.Globalization;
using System.Text.RegularExpressions;
using Mashhad.Core.Localization;

namespace Mashhad.Tests;

public class LocalizationTests
{
    [Fact]
    public void Arabic_and_English_have_exactly_the_same_keys()
    {
        var en = Localizer.LoadTable("en");
        var ar = Localizer.LoadTable("ar");
        Assert.Empty(en.Keys.Except(ar.Keys));
        Assert.Empty(ar.Keys.Except(en.Keys));
        Assert.All(ar, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        var en = Localizer.LoadTable("en");
        var ar = Localizer.LoadTable("ar");
        static IEnumerable<string> Holes(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().OrderBy(x => x);
        foreach (var (key, value) in en) Assert.Equal(Holes(value), Holes(ar[key]));
    }

    [Fact]
    public void Arabic_is_actually_translated_and_right_to_left()
    {
        var ar = new Localizer("ar");
        Assert.True(ar.IsRightToLeft);
        Assert.False(new Localizer("en").IsRightToLeft);
        Assert.Equal("مشهد", ar["App.Name"]);
        int arabic = ar.Keys.Count(k => ar[k].Any(c => c >= '؀' && c <= 'ۿ'));
        Assert.True(arabic >= ar.Keys.Count - 2, "nearly every Arabic string should contain Arabic script");
    }

    [Fact]
    public void Falls_back_gracefully()
    {
        var l = new Localizer("fr");
        Assert.Equal("en", l.Language);
        Assert.Equal("No.Such.Key", l["No.Such.Key"]);
        Assert.Equal("Speed: 1.5×", l.Format("Osd.Speed", "1.5"));
    }

    [Theory]
    [InlineData("ar", "en-US", "ar")]
    [InlineData("system", "ar-SA", "ar")]
    [InlineData("system", "de-DE", "en")]
    [InlineData(null, "en-GB", "en")]
    [InlineData("xx", "ar-EG", "ar")]
    public void Resolves_language(string? setting, string system, string expected) =>
        Assert.Equal(expected, Localizer.ResolveLanguage(setting, CultureInfo.GetCultureInfo(system)));

    /// <summary>Every string id referenced by the WinUI app (XAML and code) must exist, and nothing should be dead.</summary>
    [Fact]
    public void Every_key_used_by_the_app_exists_and_every_key_is_used()
    {
        var root = FindRepoRoot();
        var en = Localizer.LoadTable("en");
        var prefixes = en.Keys.Select(k => k.Split('.')[0]).Distinct().ToArray();
        var pattern = new Regex("\"((?:" + string.Join("|", prefixes) + @")\.[A-Za-z0-9]+)""");

        var used = new HashSet<string>();
        var sources = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        foreach (var file in sources)
            foreach (Match m in pattern.Matches(File.ReadAllText(file))) used.Add(m.Groups[1].Value);

        Assert.True(used.Count > 100, "the scan should find the app's keys");
        Assert.Empty(used.Except(en.Keys).OrderBy(k => k));      // referenced but missing
        Assert.Empty(en.Keys.Except(used).OrderBy(k => k));      // defined but never used
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mashhad.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
