using System.Globalization;

namespace Mashhad.Core.Imaging;

/// <summary>A crop rectangle in image pixels (in the displayed, i.e. rotated, orientation).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public override string ToString() => $"{Width}×{Height} @ {X},{Y}";
}

public static class CropMath
{
    /// <summary>Normalises a clockwise rotation to 0, 90, 180 or 270.</summary>
    public static int NormalizeRotation(int degrees)
    {
        int r = ((degrees % 360) + 360) % 360;
        return (int)Math.Round(r / 90.0) % 4 * 90;
    }

    /// <summary>Image size as displayed after a clockwise rotation.</summary>
    public static (int Width, int Height) RotatedSize(int width, int height, int rotation) =>
        NormalizeRotation(rotation) is 90 or 270 ? (height, width) : (width, height);

    /// <summary>Maps a selection dragged in the window (any corner order) to image pixels, clamped to the image.</summary>
    public static PixelRect WindowToImage(Viewport vp, ImageView view, double x1, double y1, double x2, double y2)
    {
        if (!vp.IsValid) return default;
        var rect = view.DisplayRect(vp);
        double s = view.Scale(vp);
        double left = (Math.Min(x1, x2) - rect.X) / s, right = (Math.Max(x1, x2) - rect.X) / s;
        double top = (Math.Min(y1, y2) - rect.Y) / s, bottom = (Math.Max(y1, y2) - rect.Y) / s;

        int ix1 = (int)Math.Clamp(Math.Round(left), 0, vp.ImageWidth);
        int ix2 = (int)Math.Clamp(Math.Round(right), 0, vp.ImageWidth);
        int iy1 = (int)Math.Clamp(Math.Round(top), 0, vp.ImageHeight);
        int iy2 = (int)Math.Clamp(Math.Round(bottom), 0, vp.ImageHeight);
        return new PixelRect(ix1, iy1, ix2 - ix1, iy2 - iy1);
    }

    /// <summary>Inverse of <see cref="WindowToImage"/>: where an image-pixel rectangle appears in the window.</summary>
    public static RectD ImageToWindow(Viewport vp, ImageView view, PixelRect crop)
    {
        var rect = view.DisplayRect(vp);
        double s = view.Scale(vp);
        return new RectD(rect.X + crop.X * s, rect.Y + crop.Y * s, crop.Width * s, crop.Height * s);
    }

    /// <summary>
    /// ASS drawing (for mpv's <c>osd-overlay</c>, resolution = window size) that dims everything
    /// outside the selection and outlines it in the brand teal.
    /// </summary>
    public static string BuildOverlayAss(double windowWidth, double windowHeight, RectD selection)
    {
        static string F(double v) => Math.Round(v).ToString(CultureInfo.InvariantCulture);
        string w = F(windowWidth), h = F(windowHeight);
        string x1 = F(selection.X), y1 = F(selection.Y), x2 = F(selection.Right), y2 = F(selection.Bottom);

        // Outer rectangle clockwise, inner counter-clockwise: the non-zero winding rule leaves a hole.
        string dim = "{\\an7\\pos(0,0)\\bord0\\shad0\\1c&H000000&\\1a&H70&\\p1}" +
                     $"m 0 0 l {w} 0 l {w} {h} l 0 {h} m {x1} {y1} l {x1} {y2} l {x2} {y2} l {x2} {y1}" + "{\\p0}";
        // ASS colours are BGR: #2FD8B8 -> B8D82F.
        string frame = "{\\an7\\pos(0,0)\\bord2\\shad0\\1a&HFF&\\3c&HB8D82F&\\p1}" +
                       $"m {x1} {y1} l {x2} {y1} l {x2} {y2} l {x1} {y2}" + "{\\p0}";
        return dim + "\n" + frame;
    }
}
