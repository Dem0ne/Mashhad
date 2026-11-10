using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace Mashhad.Core.Imaging;

public sealed record MetadataRow(string Key, string Value);

/// <summary>Reads the EXIF facts worth showing in the info panel. Keys are localisation ids.</summary>
public static class ImageMetadataReader
{
    public static List<MetadataRow> Read(string path)
    {
        IReadOnlyList<MetadataExtractor.Directory> dirs;
        try
        {
            dirs = ImageMetadataReader2.ReadMetadata(path);
        }
        catch (Exception e) when (e is ImageProcessingException or IOException or UnauthorizedAccessException)
        {
            return new List<MetadataRow>();
        }
        return Extract(dirs);
    }

    public static List<MetadataRow> Read(Stream stream)
    {
        try { return Extract(ImageMetadataReader2.ReadMetadata(stream)); }
        catch (Exception e) when (e is ImageProcessingException or IOException) { return new List<MetadataRow>(); }
    }

    private static List<MetadataRow> Extract(IReadOnlyList<MetadataExtractor.Directory> dirs)
    {
        var rows = new List<MetadataRow>();
        var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
        var sub = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) rows.Add(new MetadataRow(key, value.Trim()));
        }

        var taken = sub?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal) ?? ifd0?.GetDescription(ExifDirectoryBase.TagDateTime);
        if (taken is not null && DateTime.TryParseExact(taken, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            taken = dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Add("Info.DateTaken", taken);

        var make = ifd0?.GetDescription(ExifDirectoryBase.TagMake);
        var model = ifd0?.GetDescription(ExifDirectoryBase.TagModel);
        if (make is not null && model is not null && model.StartsWith(make, StringComparison.OrdinalIgnoreCase)) make = null;
        Add("Info.Camera", string.Join(" ", new[] { make, model }.Where(s => !string.IsNullOrWhiteSpace(s))));
        Add("Info.Lens", sub?.GetDescription(ExifDirectoryBase.TagLensModel));
        Add("Info.Exposure", sub?.GetDescription(ExifDirectoryBase.TagExposureTime));
        Add("Info.Aperture", sub?.GetDescription(ExifDirectoryBase.TagFNumber));
        Add("Info.Iso", sub?.GetDescription(ExifDirectoryBase.TagIsoEquivalent));
        Add("Info.FocalLength", sub?.GetDescription(ExifDirectoryBase.TagFocalLength));
        Add("Info.Flash", sub?.GetDescription(ExifDirectoryBase.TagFlash));
        Add("Info.Orientation", ifd0?.GetDescription(ExifDirectoryBase.TagOrientation));
        Add("Info.Software", ifd0?.GetDescription(ExifDirectoryBase.TagSoftware));

        var location = gps?.GetGeoLocation();
        if (location is { IsZero: false } loc)
            Add("Info.Gps", string.Create(CultureInfo.InvariantCulture, $"{loc.Latitude:0.000000}, {loc.Longitude:0.000000}"));

        return rows;
    }
}

// MetadataExtractor's entry point has the same simple name as our reader; alias it locally.
internal static class ImageMetadataReader2
{
    public static IReadOnlyList<MetadataExtractor.Directory> ReadMetadata(string path) =>
        MetadataExtractor.ImageMetadataReader.ReadMetadata(path);

    public static IReadOnlyList<MetadataExtractor.Directory> ReadMetadata(Stream stream) =>
        MetadataExtractor.ImageMetadataReader.ReadMetadata(stream);
}
