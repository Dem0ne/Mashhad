using System.Security.Cryptography;
using Mashhad.Core.Fonts;
using Xunit;

namespace Mashhad.Tests;

public class FontTests
{
    private static readonly string[] Weights = { "Light", "Regular", "Medium", "Bold", "Black" };

    private static string EncryptedDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mashhad.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repository root not found"), "assets", "fonts", "encrypted");
    }

    [SkippableFact]
    public void Encrypted_fonts_decrypt_with_the_build_key()
    {
        var key = FontCrypto.ParseKey(Environment.GetEnvironmentVariable("MASHHAD_FONT_KEY"));
        Skip.If(key is null, "MASHHAD_FONT_KEY is not set (local or fork build)");

        int lastWeight = 0;
        foreach (var weight in Weights)
        {
            var blob = File.ReadAllBytes(Path.Combine(EncryptedDir(), $"thmanyahsans-{weight}.otf.enc"));
            var plain = FontCrypto.Decrypt(blob, key!);
            try
            {
                Assert.True(OpenTypeInfo.IsSfnt(plain), $"{weight}: not an OpenType font after decryption");
                var info = OpenTypeInfo.Read(plain);
                Assert.Contains("thmanyah", info.PreferredFamily, StringComparison.OrdinalIgnoreCase);
                Assert.True(info.Weight > lastWeight, $"{weight}: weight {info.Weight} should be above {lastWeight}");
                lastWeight = info.Weight;
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
    }

    [Fact]
    public void Every_weight_is_present_and_no_plain_font_is_committed()
    {
        var dir = EncryptedDir();
        foreach (var weight in Weights)
        {
            var file = new FileInfo(Path.Combine(dir, $"thmanyahsans-{weight}.otf.enc"));
            Assert.True(file.Exists && file.Length > 1024, $"missing {file.Name}");
            Assert.False(OpenTypeInfo.IsSfnt(File.ReadAllBytes(file.FullName)), $"{file.Name} is not encrypted");
        }
        var root = Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));
        var plain = new[] { "assets", "src" }
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(plain);
    }

    [Fact]
    public void Round_trip_and_tamper_detection()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var font = MinimalFont("Test Sans", "Test Sans", 500);
        var blob = FontCrypto.Encrypt(font, key);
        Assert.Equal(font, FontCrypto.Decrypt(blob, key));

        blob[20] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => FontCrypto.Decrypt(blob, key));
        Assert.ThrowsAny<CryptographicException>(() => FontCrypto.Decrypt(FontCrypto.Encrypt(font, key), RandomNumberGenerator.GetBytes(32)));
        Assert.ThrowsAny<CryptographicException>(() => FontCrypto.Decrypt(new byte[10], key));
    }

    [Fact]
    public void Key_parsing()
    {
        Assert.Null(FontCrypto.ParseKey(null));
        Assert.Null(FontCrypto.ParseKey(""));
        Assert.Null(FontCrypto.ParseKey("not base64!"));
        Assert.Null(FontCrypto.ParseKey(Convert.ToBase64String(new byte[16])));
        Assert.Equal(32, FontCrypto.ParseKey(" " + Convert.ToBase64String(new byte[32]) + "\n")!.Length);
    }

    [Fact]
    public void OpenType_names_and_weight()
    {
        var info = OpenTypeInfo.Read(MinimalFont("Test Sans Medium", "Test Sans", 500));
        Assert.Equal("Test Sans Medium", info.Family);
        Assert.Equal("Test Sans", info.PreferredFamily);
        Assert.Equal(500, info.Weight);
        Assert.Throws<InvalidDataException>(() => OpenTypeInfo.Read(new byte[64]));
    }

    /// <summary>An sfnt with just a name table (IDs 1, 16; Windows UTF-16BE) and an OS/2 weight.</summary>
    private static byte[] MinimalFont(string family, string typographic, int weight)
    {
        var names = new[] { (1, family), (16, typographic) };
        var strings = names.Select(n => System.Text.Encoding.BigEndianUnicode.GetBytes(n.Item2)).ToArray();
        var name = new List<byte>();
        void U16(List<byte> b, int v) { b.Add((byte)(v >> 8)); b.Add((byte)v); }
        U16(name, 0); U16(name, names.Length); U16(name, 6 + 12 * names.Length);
        int off = 0;
        for (int i = 0; i < names.Length; i++)
        {
            U16(name, 3); U16(name, 1); U16(name, 0x409); U16(name, names[i].Item1); U16(name, strings[i].Length); U16(name, off);
            off += strings[i].Length;
        }
        foreach (var s in strings) name.AddRange(s);
        var os2 = new List<byte>(); U16(os2, 4); U16(os2, 500); U16(os2, weight); U16(os2, 5);

        var font = new List<byte>();
        void U32(List<byte> b, uint v) { U16(b, (int)(v >> 16)); U16(b, (int)(v & 0xFFFF)); }
        U32(font, 0x4F54544F); U16(font, 2); U16(font, 32); U16(font, 1); U16(font, 0);
        int nameOffset = 12 + 32, os2Offset = nameOffset + name.Count;
        U32(font, 0x4F532F32); U32(font, 0); U32(font, (uint)os2Offset); U32(font, (uint)os2.Count);
        U32(font, 0x6E616D65); U32(font, 0); U32(font, (uint)nameOffset); U32(font, (uint)name.Count);
        font.AddRange(name); font.AddRange(os2);
        return font.ToArray();
    }
}
