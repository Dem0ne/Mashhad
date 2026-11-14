using System.Buffers.Binary;

namespace Mashhad.Core.Media;

/// <summary>
/// Reads an image's pixel size from its header only (a few hundred bytes for most files), so the
/// gallery can lay out thousands of photos at their real aspect ratios without decoding them.
/// JPEG sizes honour the EXIF orientation (a portrait photo stored sideways reports portrait).
/// </summary>
public static class ImageHeader
{
    public static (int Width, int Height)? TryRead(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096);
            return TryRead(fs);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static (int Width, int Height)? TryRead(Stream s)
    {
        Span<byte> h = stackalloc byte[32];
        int n = ReadAtMost(s, h);
        if (n < 10) return null;

        if (h[0] == 0xFF && h[1] == 0xD8) return Jpeg(s);
        if (n >= 24 && h[0] == 0x89 && h[1] == (byte)'P' && h[2] == (byte)'N' && h[3] == (byte)'G')
            return Valid(BinaryPrimitives.ReadInt32BigEndian(h[16..]), BinaryPrimitives.ReadInt32BigEndian(h[20..]));
        if (h[0] == (byte)'G' && h[1] == (byte)'I' && h[2] == (byte)'F')
            return Valid(BinaryPrimitives.ReadUInt16LittleEndian(h[6..]), BinaryPrimitives.ReadUInt16LittleEndian(h[8..]));
        if (n >= 26 && h[0] == (byte)'B' && h[1] == (byte)'M')
            return Valid(BinaryPrimitives.ReadInt32LittleEndian(h[18..]), Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(h[22..])));
        if (n >= 30 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return WebP(h, n);
        return null;
    }

    private static (int, int)? WebP(ReadOnlySpan<byte> h, int n)
    {
        var chunk = h[12..16];
        if (chunk.SequenceEqual("VP8 "u8))
            return Valid(BinaryPrimitives.ReadUInt16LittleEndian(h[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(h[28..]) & 0x3FFF);
        if (chunk.SequenceEqual("VP8L"u8) && n >= 25 && h[20] == 0x2F)
        {
            int b0 = h[21], b1 = h[22], b2 = h[23], b3 = h[24];
            return Valid(1 + (b0 | ((b1 & 0x3F) << 8)), 1 + ((b1 >> 6) | (b2 << 2) | ((b3 & 0x0F) << 10)));
        }
        if (chunk.SequenceEqual("VP8X"u8))
            return Valid(1 + (h[24] | (h[25] << 8) | (h[26] << 16)), 1 + (h[27] | (h[28] << 8) | (h[29] << 16)));
        return null;
    }

    private static (int, int)? Jpeg(Stream s)
    {
        s.Position = 2;
        int orientation = 1;
        Span<byte> seg = stackalloc byte[8];
        for (int guard = 0; guard < 512; guard++)
        {
            int b = s.ReadByte();
            if (b < 0) return null;
            if (b != 0xFF) continue;
            int marker;
            do { marker = s.ReadByte(); } while (marker == 0xFF);
            if (marker < 0) return null;
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue; // no length
            if (marker == 0xD9 || marker == 0xDA) return null;                            // EOI / start of scan: no SOF found
            if (ReadAtMost(s, seg[..2]) < 2) return null;
            int len = BinaryPrimitives.ReadUInt16BigEndian(seg) - 2;
            if (len < 0) return null;
            long next = s.Position + len;

            bool sof = marker >= 0xC0 && marker <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (sof)
            {
                if (ReadAtMost(s, seg[..5]) < 5) return null;
                int height = BinaryPrimitives.ReadUInt16BigEndian(seg[1..]), width = BinaryPrimitives.ReadUInt16BigEndian(seg[3..]);
                return orientation is >= 5 and <= 8 ? Valid(height, width) : Valid(width, height);
            }
            if (marker == 0xE1 && len > 14)
            {
                var app1 = new byte[Math.Min(len, 65535)];
                int got = ReadAtMost(s, app1);
                orientation = ExifOrientation(app1.AsSpan(0, got)) ?? orientation;
            }
            s.Position = next;
        }
        return null;
    }

    /// <summary>The EXIF orientation tag (1–8) from an APP1 payload, or null.</summary>
    internal static int? ExifOrientation(ReadOnlySpan<byte> app1)
    {
        if (app1.Length < 14 || !app1[..6].SequenceEqual("Exif\0\0"u8)) return null;
        var tiff = app1[6..];
        bool le = tiff[0] == (byte)'I';
        if (!(le && tiff[1] == (byte)'I') && !(tiff[0] == (byte)'M' && tiff[1] == (byte)'M')) return null;
        ushort U16(ReadOnlySpan<byte> x) => le ? BinaryPrimitives.ReadUInt16LittleEndian(x) : BinaryPrimitives.ReadUInt16BigEndian(x);
        uint U32(ReadOnlySpan<byte> x) => le ? BinaryPrimitives.ReadUInt32LittleEndian(x) : BinaryPrimitives.ReadUInt32BigEndian(x);
        uint ifd = U32(tiff[4..]);
        if (ifd + 2 > tiff.Length) return null;
        int count = U16(tiff[(int)ifd..]);
        for (int i = 0; i < count; i++)
        {
            int e = (int)ifd + 2 + i * 12;
            if (e + 12 > tiff.Length) return null;
            if (U16(tiff[e..]) == 0x0112)
            {
                int v = U16(tiff[(e + 8)..]);
                return v is >= 1 and <= 8 ? v : null;
            }
        }
        return null;
    }

    private static (int, int)? Valid(int w, int h) => w > 0 && h > 0 && w < 1_000_000 && h < 1_000_000 ? (w, h) : null;

    private static int ReadAtMost(Stream s, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = s.Read(buffer[total..]);
            if (n <= 0) break;
            total += n;
        }
        return total;
    }
}
