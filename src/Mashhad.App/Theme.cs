using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mashhad.App;

/// <summary>
/// The look of every floating surface (cards, pills, dock, chips, mode switcher, panels), switchable
/// live between the default dark theme and «Liquid Glass». Surfaces made by <see cref="Ui.Pill"/>
/// register here, and <see cref="Set"/> restyles all that are still alive.
/// </summary>
/// <summary>Kinds of surface: floating over the wall, floating over the native video, or a docked panel.</summary>
internal enum SurfaceKind { Floating, OverVideo, Panel }

internal static class Theme
{
    private sealed record Surface(WeakReference<Border> Border, SurfaceKind Kind, double Radius);
    private static readonly List<Surface> s_surfaces = new();

    /// <summary>True while the Liquid Glass theme is active.</summary>
    public static bool Glass { get; private set; }

    /// <summary>Raised after a switch, for views that draw something themselves.</summary>
    public static event Action? Changed;

    public static void Register(Border border, SurfaceKind kind, double radius)
    {
        if (s_surfaces.Count > 0 && s_surfaces.Count % 64 == 0) s_surfaces.RemoveAll(s => !s.Border.TryGetTarget(out _));
        s_surfaces.Add(new Surface(new WeakReference<Border>(border), kind, radius));
        Style(border, kind, radius);
    }

    public static void Set(bool glass)
    {
        if (Glass == glass) return;
        Glass = glass;
        s_surfaces.RemoveAll(s => !s.Border.TryGetTarget(out _));
        foreach (var s in s_surfaces)
            if (s.Border.TryGetTarget(out var b)) Style(b, s.Kind, s.Radius);
        Changed?.Invoke();
    }

    private static void Style(Border b, SurfaceKind kind, double radius)
    {
        // Docked panels are denser than floating cards in the default theme; in glass all are glass.
        b.Background = kind == SurfaceKind.Panel && !Glass ? Ui.Solid(0xC8, 0x16, 0x18, 0x1C) : Fill(kind == SurfaceKind.OverVideo);
        b.BorderBrush = Stroke(kind == SurfaceKind.OverVideo);
        b.BorderThickness = new Thickness(1);
        b.CornerRadius = new CornerRadius(Radius(radius));
    }

    /// <summary>Corner radius for a surface designed with <paramref name="radius"/> in the default theme.</summary>
    public static double Radius(double radius) => Glass ? radius + 6 : radius;

    /// <summary>
    /// The panel fill. Inside the window it is acrylic (blurring what is behind it); over the native
    /// video window only a translucent fill is possible, so glass there is a tinted, lit gradient.
    /// </summary>
    public static Brush Fill(bool overVideo)
    {
        if (!Glass)
        {
            if (overVideo) return Ui.Solid(0xD0, 0x14, 0x15, 0x18);
            return new AcrylicBrush
            {
                TintColor = Color.FromArgb(255, 0x14, 0x15, 0x18),
                TintOpacity = 0.72,
                TintLuminosityOpacity = 0.9,
                FallbackColor = Color.FromArgb(0xF0, 0x1A, 0x1B, 0x1F),
            };
        }
        if (overVideo)
        {
            return Vertical(
                (Color.FromArgb(0xBC, 0x3C, 0x34, 0x40), 0),
                (Color.FromArgb(0xB0, 0x22, 0x1C, 0x26), 0.5),
                (Color.FromArgb(0xB8, 0x16, 0x12, 0x1A), 1));
        }
        return new AcrylicBrush
        {
            TintColor = Color.FromArgb(255, 0x2A, 0x22, 0x2C),
            TintOpacity = 0.22,
            TintLuminosityOpacity = 0.18,
            FallbackColor = Color.FromArgb(0xB8, 0x24, 0x1E, 0x28),
        };
    }

    /// <summary>The edge: a hairline in the default theme, a soft highlight (lit from above) in glass.</summary>
    public static Brush Stroke(bool overVideo)
    {
        if (!Glass) return Ui.White(0.09);
        return Vertical(
            (Color.FromArgb(overVideo ? (byte)0x70 : (byte)0x66, 255, 255, 255), 0),
            (Color.FromArgb(0x1C, 255, 255, 255), 0.45),
            (Color.FromArgb(0x10, 255, 255, 255), 1));
    }

    /// <summary>The wall behind everything (the window backdrop shows through it in glass).</summary>
    public static Brush Wall()
    {
        if (!Glass) return new SolidColorBrush(Ui.Wall);
        var b = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x99, 0x1A, 0x10, 0x16), Offset = 0 });
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x80, 0x0E, 0x0D, 0x12), Offset = 0.6 });
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x99, 0x14, 0x0A, 0x10), Offset = 1 });
        return b;
    }

    private static LinearGradientBrush Vertical(params (Color Color, double Offset)[] stops)
    {
        var b = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(0, 1) };
        foreach (var (c, o) in stops) b.GradientStops.Add(new GradientStop { Color = c, Offset = o });
        return b;
    }
}
