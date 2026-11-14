namespace Mashhad.Core.Imaging;

/// <summary>Window and (already rotated) image sizes in physical pixels.</summary>
public readonly record struct Viewport(double WindowWidth, double WindowHeight, double ImageWidth, double ImageHeight)
{
    public bool IsValid => WindowWidth > 0 && WindowHeight > 0 && ImageWidth > 0 && ImageHeight > 0;
    /// <summary>Scale at which the image exactly fits the window (mpv's zoom 0).</summary>
    public double FitScale => IsValid ? Math.Min(WindowWidth / ImageWidth, WindowHeight / ImageHeight) : 1;
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>
/// Zoom/pan state expressed in mpv's own units: <c>video-zoom</c> is log2 of the scale relative
/// to "fit", <c>video-pan-x/y</c> are fractions of the scaled image size.
/// </summary>
public readonly record struct ImageView(double Zoom, double PanX, double PanY)
{
    public const double MinZoom = -2, MaxZoom = 6;
    public static readonly ImageView Fit = new(0, 0, 0);

    public double Scale(Viewport vp) => vp.FitScale * Math.Pow(2, Zoom);

    /// <summary>Scale relative to the image's own pixels (1.0 = 100%).</summary>
    public double Percent(Viewport vp) => Scale(vp) * 100;

    /// <summary>Where the image is drawn, in window pixels (may extend beyond the window).</summary>
    public RectD DisplayRect(Viewport vp)
    {
        double s = Scale(vp), w = vp.ImageWidth * s, h = vp.ImageHeight * s;
        double cx = vp.WindowWidth / 2 + PanX * w, cy = vp.WindowHeight / 2 + PanY * h;
        return new RectD(cx - w / 2, cy - h / 2, w, h);
    }

    /// <summary>Zoom level at which one image pixel equals one screen pixel.</summary>
    public static ImageView ActualSize(Viewport vp) => new(Math.Log2(1 / vp.FitScale), 0, 0);

    /// <summary>Changes the zoom by <paramref name="delta"/> (log2 steps) keeping the point under the cursor fixed.</summary>
    public ImageView ZoomAt(Viewport vp, double delta, double cursorX, double cursorY)
    {
        if (!vp.IsValid) return this;
        double newZoom = Math.Clamp(Zoom + delta, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - Zoom) < 1e-9) return this;

        var rect = DisplayRect(vp);
        // Normalised image coordinate under the cursor (-0.5..0.5 inside the image).
        double u = (cursorX - (rect.X + rect.Width / 2)) / rect.Width;
        double v = (cursorY - (rect.Y + rect.Height / 2)) / rect.Height;

        double s = vp.FitScale * Math.Pow(2, newZoom);
        double w = vp.ImageWidth * s, h = vp.ImageHeight * s;
        double panX = (cursorX - vp.WindowWidth / 2 - u * w) / w;
        double panY = (cursorY - vp.WindowHeight / 2 - v * h) / h;
        return new ImageView(newZoom, panX, panY).Clamp(vp);
    }

    /// <summary>Zoom around the window centre.</summary>
    public ImageView ZoomBy(Viewport vp, double delta) => ZoomAt(vp, delta, vp.WindowWidth / 2, vp.WindowHeight / 2);

    /// <summary>Pans by a distance in window pixels.</summary>
    public ImageView PanBy(Viewport vp, double dx, double dy)
    {
        if (!vp.IsValid) return this;
        double s = Scale(vp);
        return new ImageView(Zoom, PanX + dx / (vp.ImageWidth * s), PanY + dy / (vp.ImageHeight * s)).Clamp(vp);
    }

    /// <summary>
    /// Keeps the image from being dragged away: on an axis where it is smaller than the window
    /// it stays centred, otherwise its edges cannot move inside the window.
    /// </summary>
    public ImageView Clamp(Viewport vp)
    {
        if (!vp.IsValid) return this;
        double s = Scale(vp), w = vp.ImageWidth * s, h = vp.ImageHeight * s;
        double maxX = w > vp.WindowWidth ? (w - vp.WindowWidth) / 2 / w : 0;
        double maxY = h > vp.WindowHeight ? (h - vp.WindowHeight) / 2 / h : 0;
        return new ImageView(Zoom, Math.Clamp(PanX, -maxX, maxX), Math.Clamp(PanY, -maxY, maxY));
    }
}
