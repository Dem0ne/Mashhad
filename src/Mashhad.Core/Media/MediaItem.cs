namespace Mashhad.Core.Media;

public sealed record MediaItem(string Path, MediaKind Kind)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public static MediaItem FromPath(string path) => new(path, MediaTypes.Classify(path));

    public override string ToString() => FileName;
}

public enum GallerySort
{
    Name,
    DateModified,
    Size,
}
