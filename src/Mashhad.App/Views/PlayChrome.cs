using Mashhad.App.Controls;
using Mashhad.Core.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Mashhad.App.Views;

/// <summary>
/// Play mode ("Cinema"): everything that floats over the video — title pill, mode switcher,
/// caption buttons, prev/next edges, the dock, crop bar, info panel and toasts. Hosted in a
/// transparent overlay window above the native video window (or, as a fallback, in the main window).
/// The root itself is the "video surface" for pointer input.
/// </summary>
public sealed class PlayChrome : Grid
{
    public readonly ModeSwitcher Modes = new(overVideo: true);
    public readonly SeekBar Seek = new();
    public readonly Grid Chrome = new();
    public readonly TextBlock TitleText = Ui.Text("", 13.5, true), ContextText = Ui.Text("", 13, false, 0.62);
    public readonly TextBlock TimeText = Ui.Time("00:00", 14, true), ZoomText = Ui.Text("100%", 13, true);
    public readonly TextBlock SpeedText = Ui.Text("1×", 13, true), ChipText = Ui.Text("", 12.5);
    public readonly TextBlock CropSizeText = Ui.Text("", 13.5, true), ToastText = Ui.Text("", 13.5);
    public readonly FontIcon PlayGlyph, VolumeGlyph = Ui.Icon(Ui.G.Volume), FullscreenGlyph = Ui.Icon(Ui.G.Full);
    public readonly Slider Volume = new() { Width = 92, Minimum = 0, Maximum = 100, IsThumbToolTipEnabled = false, VerticalAlignment = VerticalAlignment.Center, IsTabStop = false };
    public readonly MenuFlyout SpeedMenu = new(), SubsMenu = new(), AudioMenu = new(), MoreMenu = new();
    public readonly StackPanel InfoRows = new() { Spacing = 10 };
    public readonly Border InfoPanel, CropBar, Toast, Chip, Dock, EdgePrev, EdgeNext, TitlePill;
    public readonly StackPanel Captions;
    public readonly Button PlayPause, MaxButton;
    public readonly FontIcon MaxGlyph = Ui.Icon(Ui.G.Max, 10);
    private readonly StackPanel _videoLeft, _imageLeft, _videoCenter, _videoRight, _imageRight;
    private readonly Rectangle _scrimTop, _scrimBottom;

    public event Action<PlayerCommand>? Command;
    public event Action? DragWindow, MaximizeToggle, MinimizeRequested, CloseRequested;

    public PlayChrome()
    {
        Background = new SolidColorBrush(Colors.Transparent); // hit-testable: this is the video surface
        RequestedTheme = ElementTheme.Dark; // in the overlay window it does not inherit the main window's theme
        _scrimTop = Scrim(top: true);
        _scrimBottom = Scrim(top: false);
        Children.Add(_scrimTop);
        Children.Add(_scrimBottom);
        Chrome.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(200) };
        Children.Add(Chrome);

        // title pill (drag to move the window, double-click to maximise)
        TitlePill = Ui.Pill(Ui.Row(10, Ui.Logo(18), TitleText, ContextText), overVideo: true, radius: 17, padding: new Thickness(10, 0, 14, 0));
        TitlePill.Height = 34;
        TitlePill.HorizontalAlignment = HorizontalAlignment.Left;
        TitlePill.VerticalAlignment = VerticalAlignment.Top;
        TitlePill.Margin = new Thickness(12, 9, 0, 0);
        TitlePill.MaxWidth = 560;
        TitlePill.PointerPressed += (_, e) => { if (e.GetCurrentPoint(TitlePill).Properties.IsLeftButtonPressed) DragWindow?.Invoke(); };
        TitlePill.DoubleTapped += (_, _) => MaximizeToggle?.Invoke();
        Chrome.Children.Add(TitlePill);

        Modes.HorizontalAlignment = HorizontalAlignment.Center;
        Modes.VerticalAlignment = VerticalAlignment.Top;
        Modes.Margin = new Thickness(0, 7, 0, 0);
        Chrome.Children.Add(Modes);

        // own caption buttons (the overlay covers the system ones)
        MaxButton = Caption(MaxGlyph, () => MaximizeToggle?.Invoke());
        Captions = Ui.Row(0, Caption(Ui.Icon(Ui.G.Min, 10), () => MinimizeRequested?.Invoke()), MaxButton,
            Caption(Ui.Icon(Ui.G.Close, 10), () => CloseRequested?.Invoke(), danger: true));
        Captions.HorizontalAlignment = HorizontalAlignment.Right;
        Captions.VerticalAlignment = VerticalAlignment.Top;
        Captions.FlowDirection = FlowDirection.LeftToRight;
        Chrome.Children.Add(Captions);

        Chip = Ui.Pill(Ui.Row(8, new Ellipse { Width = 8, Height = 8, Fill = Ui.Brand() }, ChipText), overVideo: true, radius: 15, padding: new Thickness(12, 5, 14, 5));
        Chip.HorizontalAlignment = HorizontalAlignment.Left;
        Chip.VerticalAlignment = VerticalAlignment.Top;
        Chip.Margin = new Thickness(12, 52, 0, 0);
        Chrome.Children.Add(Chip);

        EdgePrev = Edge(Ui.G.ChevL, PlayerCommand.PreviousItem, HorizontalAlignment.Left);
        EdgeNext = Edge(Ui.G.ChevR, PlayerCommand.NextItem, HorizontalAlignment.Right);
        Chrome.Children.Add(EdgePrev);
        Chrome.Children.Add(EdgeNext);

        // ---- dock ----
        PlayPause = Ui.PlayButton(54, (_, _) => Command?.Invoke(PlayerCommand.PlayPause), out PlayGlyph);
        _videoLeft = Ui.Row(4, TimeText);
        _imageLeft = Ui.Row(2, Btn(Ui.G.ZoomOut, "Image.ZoomOut", PlayerCommand.ZoomOut),
            Wrap(ZoomText, PlayerCommand.ZoomFit, "Image.ZoomFit"), Btn(Ui.G.ZoomIn, "Image.ZoomIn", PlayerCommand.ZoomIn),
            WrapText("1:1", PlayerCommand.ZoomActual, "Image.ZoomActual"));
        _videoCenter = Ui.Row(2, Btn(Ui.G.Prev, "Bar.Previous", PlayerCommand.PreviousItem), Btn(Ui.G.Back, "Bar.Back10", PlayerCommand.SeekBack10),
            PlayPause, Btn(Ui.G.Fwd, "Bar.Forward10", PlayerCommand.SeekForward10), Btn(Ui.G.Next, "Bar.Next", PlayerCommand.NextItem));
        var speed = new Button { Content = Ui.Row(6, Ui.Icon(Ui.G.Speed, 14), SpeedText), Height = 32, CornerRadius = new CornerRadius(16), Padding = new Thickness(11, 0, 11, 0), Background = Ui.White(0.1), BorderThickness = new Thickness(0), Flyout = SpeedMenu, IsTabStop = false };
        L.SetTip(speed, "Bar.Speed");
        _videoRight = Ui.Row(2, speed, FlyoutBtn(Ui.G.CC, "Bar.Subtitles", SubsMenu), FlyoutBtn(Ui.G.Audio, "Bar.Audio", AudioMenu),
            Btn(VolumeGlyph, "Bar.Mute", PlayerCommand.Mute), Volume);
        _imageRight = Ui.Row(2, Btn(Ui.G.Rotate, "Image.RotateCcw", PlayerCommand.RotateCcw, mirror: true), Btn(Ui.G.Rotate, "Image.RotateCw", PlayerCommand.RotateCw),
            Btn(Ui.G.Crop, "Image.Crop", PlayerCommand.ToggleCrop), Btn(Ui.G.Save, "Image.SaveCopy", PlayerCommand.SaveImage));
        var common = Ui.Row(2, Btn(Ui.G.Info, "Bar.Info", PlayerCommand.ToggleInfoPanel), Btn(FullscreenGlyph, "Bar.Fullscreen", PlayerCommand.ToggleFullscreen),
            FlyoutBtn(Ui.G.More, "Bar.More", MoreMenu));

        var controls = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new Grid(); left.Children.Add(_videoLeft); left.Children.Add(_imageLeft);
        var right = Ui.Row(2, _videoRight, _imageRight, common);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        SetColumn(_videoCenter, 1);
        SetColumn(right, 2);
        controls.Children.Add(left);
        controls.Children.Add(_videoCenter);
        controls.Children.Add(right);

        Dock = Ui.Pill(Ui.Column(0, Seek, controls), overVideo: true, radius: 20, padding: new Thickness(18, 12, 18, 10));
        Dock.FlowDirection = FlowDirection.LeftToRight; // media controls keep the direction of time
        Dock.VerticalAlignment = VerticalAlignment.Bottom;
        Dock.MaxWidth = 1100;
        Dock.Margin = new Thickness(24, 0, 24, 24);
        Chrome.Children.Add(Dock);

        // ---- crop bar ----
        CropBar = Ui.Pill(Ui.Row(12, Ui.LText("Crop.Hint", 13.5, false, 0.85), CropSizeText,
            Ui.PillButton(Ui.G.Save, "Crop.Save", (_, _) => Command?.Invoke(PlayerCommand.ApplyCrop), accent: true),
            Ui.PillButton(null, "Common.Cancel", (_, _) => Command?.Invoke(PlayerCommand.ToggleCrop))), overVideo: true, radius: 22, padding: new Thickness(18, 6, 6, 6));
        CropBar.HorizontalAlignment = HorizontalAlignment.Center;
        CropBar.VerticalAlignment = VerticalAlignment.Bottom;
        CropBar.Margin = new Thickness(0, 0, 0, 150);
        CropBar.Visibility = Visibility.Collapsed;
        Chrome.Children.Add(CropBar);

        // ---- info panel ----
        var infoHead = Ui.Row(8, Ui.Icon(Ui.G.Info, 15), Ui.LText("Panel.Info", 15, true));
        InfoPanel = Ui.Pill(new ScrollViewer { Content = Ui.Column(12, infoHead, InfoRows), VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, overVideo: true, radius: 16, padding: new Thickness(18, 14, 18, 14));
        InfoPanel.Width = 330;
        InfoPanel.HorizontalAlignment = HorizontalAlignment.Right;
        InfoPanel.Margin = new Thickness(0, 56, 16, 170);
        InfoPanel.Visibility = Visibility.Collapsed;
        Chrome.Children.Add(InfoPanel);

        // ---- toast ----
        Toast = Ui.Pill(ToastText, overVideo: true, radius: 17, padding: new Thickness(16, 7, 16, 7));
        Toast.HorizontalAlignment = HorizontalAlignment.Center;
        Toast.VerticalAlignment = VerticalAlignment.Top;
        Toast.Margin = new Thickness(0, 56, 0, 0);
        Toast.Visibility = Visibility.Collapsed;
        Children.Add(Toast);

        SetKind(video: true, animated: false);
    }

    /// <summary>Switch the dock between video transport and image tools.</summary>
    public void SetKind(bool video, bool animated)
    {
        _videoLeft.Visibility = _videoRight.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        _imageLeft.Visibility = _imageRight.Visibility = video ? Visibility.Collapsed : Visibility.Visible;
        Seek.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        PlayPause.Visibility = video || animated ? Visibility.Visible : Visibility.Collapsed;
        foreach (var c in _videoCenter.Children)
            if (c is Button b && b != PlayPause && (string?)b.Tag is "Bar.Back10" or "Bar.Forward10")
                b.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool ChromeVisible => Chrome.Opacity > 0.5;

    public void SetChromeVisible(bool visible)
    {
        Chrome.Opacity = visible ? 1 : 0;
        Chrome.IsHitTestVisible = visible;
        _scrimTop.Opacity = _scrimBottom.Opacity = visible ? 1 : 0;
    }

    public bool PointerOverChrome(Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // True when the pointer is over a floating control rather than the bare video.
        for (var d = e.OriginalSource as DependencyObject; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (ReferenceEquals(d, this) || ReferenceEquals(d, Chrome)) return false;
            if (d is Border or Button or Slider or ModeSwitcher) return true;
        }
        return false;
    }

    private static Rectangle Scrim(bool top)
    {
        var g = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, top ? 0 : 1), EndPoint = new Windows.Foundation.Point(0, top ? 1 : 0) };
        g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(top ? (byte)150 : (byte)175, 0, 0, 0), Offset = 0 });
        g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, 0, 0, 0), Offset = 1 });
        return new Rectangle
        {
            Height = top ? 140 : 280, Fill = g, IsHitTestVisible = false,
            VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(200) },
        };
    }

    private Button Caption(FontIcon glyph, Action click, bool danger = false)
    {
        var b = new Button { Content = glyph, Width = 46, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), IsTabStop = false };
        if (danger) b.Resources["ButtonBackgroundPointerOver"] = Ui.Solid(255, 0xC4, 0x2B, 0x1C);
        b.Click += (_, _) => click();
        return b;
    }

    private Border Edge(string glyph, PlayerCommand cmd, HorizontalAlignment side)
    {
        var b = Ui.IconButton(glyph, side == HorizontalAlignment.Left ? "Bar.Previous" : "Bar.Next", 44, 18, (_, _) => Command?.Invoke(cmd));
        b.Height = 56;
        b.CornerRadius = new CornerRadius(12);
        var edge = Ui.Pill(b, overVideo: true, radius: 12, padding: new Thickness(0));
        edge.HorizontalAlignment = side;
        edge.VerticalAlignment = VerticalAlignment.Center;
        edge.Margin = new Thickness(18, 0, 18, 0);
        return edge;
    }

    private Button Btn(string glyph, string tip, PlayerCommand cmd, bool mirror = false) => Btn(Ui.Icon(glyph, 17), tip, cmd, mirror);

    private Button Btn(FontIcon icon, string tip, PlayerCommand cmd, bool mirror = false)
    {
        if (mirror) icon.RenderTransform = new ScaleTransform { ScaleX = -1, CenterX = 8.5 };
        var b = new Button { Content = icon, Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), IsTabStop = false, Tag = tip };
        L.SetTip(b, tip);
        b.Click += (_, _) => Command?.Invoke(cmd);
        return b;
    }

    private Button FlyoutBtn(string glyph, string tip, MenuFlyout flyout)
    {
        var b = new Button { Content = Ui.Icon(glyph, 17), Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), IsTabStop = false, Flyout = flyout };
        flyout.Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top;
        L.SetTip(b, tip);
        return b;
    }

    private Button Wrap(TextBlock text, PlayerCommand cmd, string tip)
    {
        var b = new Button { Content = text, MinWidth = 58, Height = 34, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), IsTabStop = false };
        L.SetTip(b, tip);
        b.Click += (_, _) => Command?.Invoke(cmd);
        return b;
    }

    private Button WrapText(string text, PlayerCommand cmd, string tip) => Wrap(Ui.Text(text, 13, true), cmd, tip);
}
