using System.Security.Cryptography;
using System.Text;

namespace Mashhad.Core.Fonts;

/// <summary>
/// The encrypted font container used for the licensed UI font (assets/fonts/encrypted):
/// 12-byte nonce | ciphertext | 16-byte tag, AES-256-GCM, associated data "mashhad-font-v1".
/// Everything happens in memory; nothing here touches the disk.
/// </summary>
public static class FontCrypto
{
    public const int KeySize = 32, NonceSize = 12, TagSize = 16;
    public static readonly byte[] AssociatedData = Encoding.ASCII.GetBytes("mashhad-font-v1");

    public static byte[] Decrypt(ReadOnlySpan<byte> blob, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize) throw new CryptographicException("The font key must be 32 bytes.");
        if (blob.Length < NonceSize + TagSize) throw new CryptographicException("The encrypted font is truncated.");
        var nonce = blob[..NonceSize];
        var tag = blob[^TagSize..];
        var cipher = blob[NonceSize..^TagSize];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, AssociatedData);
        return plain;
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> plain, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize) throw new CryptographicException("The font key must be 32 bytes.");
        var blob = new byte[NonceSize + plain.Length + TagSize];
        var span = blob.AsSpan();
        RandomNumberGenerator.Fill(span[..NonceSize]);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(span[..NonceSize], plain, span[NonceSize..^TagSize], span[^TagSize..], AssociatedData);
        return blob;
    }

    /// <summary>Parses a base64 key (as stored in the CI secret); null when missing or malformed.</summary>
    public static byte[]? ParseKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var key = Convert.FromBase64String(base64.Trim());
            return key.Length == KeySize ? key : null;
        }
        catch (FormatException) { return null; }
    }
}

/// <summary>The few OpenType facts the app needs: family names and weight.</summary>
public sealed record OpenTypeInfo(string Family, string TypographicFamily, string Subfamily, string FullName, int Weight)
{
    /// <summary>The name a font collection most likely lists the face under.</summary>
    public string PreferredFamily => TypographicFamily.Length > 0 ? TypographicFamily : Family;

    public static bool IsSfnt(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && (U32(data, 0) is 0x4F54544F /* OTTO */ or 0x00010000 or 0x74727565 /* true */);

    public static OpenTypeInfo Read(ReadOnlySpan<byte> data)
    {
        if (!IsSfnt(data)) throw new InvalidDataException("Not an OpenType font.");
        int tables = U16(data, 4);
        int nameAt = -1, os2At = -1;
        for (int i = 0; i < tables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > data.Length) break;
            uint tag = U32(data, rec);
            int offset = checked((int)U32(data, rec + 8));
            if (tag == 0x6E616D65) nameAt = offset;      // 'name'
            else if (tag == 0x4F532F32) os2At = offset;  // 'OS/2'
        }
        if (nameAt < 0) throw new InvalidDataException("The font has no name table.");
        int weight = os2At >= 0 && os2At + 6 <= data.Length ? U16(data, os2At + 4) : 400;
        return new OpenTypeInfo(Name(data, nameAt, 1), Name(data, nameAt, 16), Name(data, nameAt, 2), Name(data, nameAt, 4), weight);
    }

    private static string Name(ReadOnlySpan<byte> data, int table, int nameId)
    {
        int count = U16(data, table + 2), strings = table + U16(data, table + 4);
        string? best = null; int bestScore = -1;
        for (int i = 0; i < count; i++)
        {
            int rec = table + 6 + i * 12;
            if (rec + 12 > data.Length) break;
            int platform = U16(data, rec), encoding = U16(data, rec + 2), language = U16(data, rec + 4);
            if (U16(data, rec + 6) != nameId) continue;
            int length = U16(data, rec + 8), offset = strings + U16(data, rec + 10);
            if (offset + length > data.Length) continue;
            var raw = data.Slice(offset, length);
            int score; string text;
            if (platform == 3 && (encoding == 1 || encoding == 10)) { score = language == 0x409 ? 3 : 2; text = Encoding.BigEndianUnicode.GetString(raw); }
            else if (platform == 0) { score = 1; text = Encoding.BigEndianUnicode.GetString(raw); }
            else if (platform == 1 && encoding == 0) { score = 0; text = Encoding.Latin1.GetString(raw); }
            else continue;
            if (score > bestScore) { best = text; bestScore = score; }
        }
        return best?.Trim() ?? "";
    }

    private static int U16(ReadOnlySpan<byte> d, int at) => at + 2 <= d.Length ? (d[at] << 8) | d[at + 1] : 0;
    private static uint U32(ReadOnlySpan<byte> d, int at) => at + 4 <= d.Length ? ((uint)d[at] << 24) | ((uint)d[at + 1] << 16) | ((uint)d[at + 2] << 8) | d[at + 3] : 0;
}
