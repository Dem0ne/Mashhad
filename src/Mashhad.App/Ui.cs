using Mashhad.Core.Playback;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace Mashhad.App;

/// <summary>Small factory helpers so the views can be built (and type-checked) in C#.</summary>
internal static class Ui
{
    public static readonly Color Rose = Color.FromArgb(255, 0xFF, 0x5E, 0x7E);
    public static readonly Color Crimson = Color.FromArgb(255, 0xE6, 0x22, 0x3F);
    public static readonly Color Wine = Color.FromArgb(255, 0x8E, 0x10, 0x35);
    public static readonly Color Wall = Color.FromArgb(255, 0x0B, 0x0C, 0x0F);
    public static readonly Color Amber = Color.FromArgb(255, 0xF2, 0xB8, 0x4B);

    public static LinearGradientBrush Brand()
    {
        var b = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
        b.GradientStops.Add(new GradientStop { Color = Rose, Offset = 0 });
        b.GradientStops.Add(new GradientStop { Color = Crimson, Offset = 0.5 });
        b.GradientStops.Add(new GradientStop { Color = Wine, Offset = 1 });
        return b;
    }

    public static SolidColorBrush Solid(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));
    public static SolidColorBrush White(double opacity) => new(Color.FromArgb((byte)Math.Round(255 * opacity), 255, 255, 255));
    public static SolidColorBrush Black(double opacity) => new(Color.FromArgb((byte)Math.Round(255 * opacity), 0, 0, 0));

    /// <summary>Floating "acrylic" card. Over the native video window only a translucent fill works.</summary>
    public static Brush Card(bool overVideo = false)
    {
        if (overVideo) return Solid(0xD0, 0x14, 0x15, 0x18);
        return new AcrylicBrush
        {
            TintColor = Color.FromArgb(255, 0x14, 0x15, 0x18),
            TintOpacity = 0.72,
            TintLuminosityOpacity = 0.9,
            FallbackColor = Color.FromArgb(0xF0, 0x1A, 0x1B, 0x1F),
        };
    }

    public static Border Pill(UIElement child, bool overVideo = false, double radius = 18, Thickness? padding = null) => new()
    {
        Child = child,
        Background = Card(overVideo),
        BorderBrush = White(0.09),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = padding ?? new Thickness(4),
    };

    public static FontIcon Icon(string glyph, double size = 16) => new()
    {
        Glyph = glyph,
        FontSize = size,
        FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
    };

    public static TextBlock Text(string text, double size = 13.5, bool semibold = false, double opacity = 1) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = semibold ? FontWeights.SemiBold : FontWeights.Normal,
        Opacity = opacity,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>A localised text block (follows language switches).</summary>
    public static TextBlock LText(string key, double size = 13.5, bool semibold = false, double opacity = 1)
    {
        var t = Text("", size, semibold, opacity);
        L.SetText(t, key);
        return t;
    }

    public static StackPanel Row(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    public static StackPanel Column(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    /// <summary>Round, borderless icon button (chrome style).</summary>
    public static Button IconButton(string glyph, string? tipKey = null, double size = 38, double glyphSize = 16, RoutedEventHandler? click = null)
    {
        var b = new Button
        {
            Content = Icon(glyph, glyphSize),
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(size / 2),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            IsTabStop = false,
        };
        if (tipKey is not null) L.SetTip(b, tipKey);
        if (click is not null) b.Click += click;
        return b;
    }

    /// <summary>Text (and optional icon) button with a pill shape.</summary>
    public static Button PillButton(string? glyph, string textKey, RoutedEventHandler? click = null, bool accent = false, double height = 34)
    {
        var label = LText(textKey, 13.5, accent);
        var content = glyph is null ? (UIElement)label : Row(8, Icon(glyph, 15), label);
        var b = new Button
        {
            Content = content,
            Height = height,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(height / 2),
            BorderThickness = new Thickness(0),
            Background = accent ? Brand() : new SolidColorBrush(Colors.Transparent),
            Foreground = new SolidColorBrush(Colors.White),
            IsTabStop = false,
        };
        if (accent) KeepBackground(b);
        if (click is not null) b.Click += click;
        return b;
    }

    /// <summary>The big round gradient play button.</summary>
    public static Button PlayButton(double size, RoutedEventHandler click, out FontIcon glyph)
    {
        glyph = Icon("", size * 0.38);
        var b = new Button
        {
            Content = glyph,
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(size / 2),
            BorderThickness = new Thickness(0),
            Background = Brand(),
            Foreground = new SolidColorBrush(Colors.White),
            IsTabStop = false,
            Margin = new Thickness(8, 0, 8, 0),
        };
        KeepBackground(b);
        b.Click += click;
        return b;
    }

    /// <summary>Keeps a custom (gradient) background in the hover/pressed states of the default button template.</summary>
    public static void KeepBackground(Button b)
    {
        var bg = b.Background;
        b.Resources["ButtonBackgroundPointerOver"] = bg;
        b.Resources["ButtonBackgroundPressed"] = bg;
        b.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(Colors.White);
        b.Resources["ButtonForegroundPressed"] = White(0.8);
    }

    public static Image Logo(double size) => new()
    {
        Width = size,
        Height = size,
        Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", size > 40 ? "Logo256.png" : "Logo32.png"))),
    };

    public static TextBlock Kbd(string key) => new()
    {
        Text = key,
        FontSize = 11.5,
        FontWeight = FontWeights.SemiBold,
        Padding = new Thickness(6, 2, 6, 2),
    };

    public static Brush Res(string key, Brush fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : fallback;

    /// <summary>Glyphs from Segoe Fluent Icons (MDL2-compatible code points).</summary>
    public static class G
    {
        public const string Play = "", Pause = "", Prev = "", Next = "", Back = "", Fwd = "",
            Volume = "", Mute = "", Full = "", Unfull = "", More = "", Settings = "",
            Folder = "", Pictures = "", Video = "", List = "", Info = "", Cut = "", Add = "",
            Delete = "", Share = "", Rotate = "", Crop = "", ZoomIn = "", ZoomOut = "",
            Search = "", Audio = "", CC = "", ChevL = "", ChevR = "", ChevD = "",
            Close = "", Min = "", Max = "", Restore = "", Home = "", Recent = "",
            Desktop = "", Download = "", Export = "", Camera = "", Sort = "", Slideshow = "",
            Repeat = "", Shuffle = "", Pin = "", Unpin = "", Save = "", Check = "",
            Speed = "", OpenFolder = "", Image = "", Edit = "", Copy = "", Split = "";
    }

    public static string ModeGlyph(AppMode m) => m switch { AppMode.Folder => G.Folder, AppMode.Play => G.Play, _ => G.Cut };
}
