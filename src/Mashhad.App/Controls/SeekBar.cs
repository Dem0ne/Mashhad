using Mashhad.Core.Playback;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Mashhad.App.Controls;

/// <summary>
/// Cinema seek bar: thin rail, brand-gradient progress, A-B loop band, white knob with a wine-red
/// centre, and a time bubble while hovering or dragging. Always left-to-right (time direction).
/// </summary>
public sealed class SeekBar : Grid
{
    private const double Inset = 7;
    private readonly Rectangle _rail, _played, _ab;
    private readonly Grid _knob;
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private readonly Canvas _canvas;
    private double _duration, _position;
    private double? _a, _b;
    private bool _dragging;

    /// <summary>(seconds, isFinal)</summary>
    public event Action<double, bool>? SeekRequested;
    public bool IsDragging => _dragging;

    public SeekBar()
    {
        Height = 22;
        FlowDirection = FlowDirection.LeftToRight;
        Background = new SolidColorBrush(Colors.Transparent);
        _canvas = new Canvas();
        _rail = new Rectangle { Height = 4, RadiusX = 2, RadiusY = 2, Fill = Ui.White(0.18) };
        _ab = new Rectangle { Height = 8, RadiusX = 2, RadiusY = 2, Fill = Ui.Solid(0x48, 0xF2, 0xB8, 0x4B), Stroke = new SolidColorBrush(Ui.Amber), StrokeThickness = 1.5, Visibility = Visibility.Collapsed };
        _played = new Rectangle { Height = 4, RadiusX = 2, RadiusY = 2, Fill = Ui.Brand() };
        _knob = new Grid { Width = 16, Height = 16 };
        _knob.Children.Add(new Ellipse { Fill = new SolidColorBrush(Colors.White) });
        _knob.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Ui.Wine) });
        _bubbleText = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontFamily = Fonts.AppFonts.Mono, Foreground = new SolidColorBrush(Ui.Wall) };
        _bubble = new Border { Child = _bubbleText, Background = new SolidColorBrush(Colors.White), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2), Visibility = Visibility.Collapsed };
        foreach (var e in new UIElement[] { _rail, _ab, _played, _knob, _bubble }) _canvas.Children.Add(e);
        Children.Add(_canvas);

        SizeChanged += (_, _) => Redraw();
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerExited += (_, _) => { if (!_dragging) _bubble.Visibility = Visibility.Collapsed; };
        PointerCaptureLost += (_, _) => _dragging = false;
    }

    public double Duration { get => _duration; set { _duration = double.IsFinite(value) ? Math.Max(0, value) : 0; Redraw(); } }
    public double Position { get => _position; set { if (_dragging) return; _position = double.IsFinite(value) ? value : 0; Redraw(); } }

    public void SetLoop(double? a, double? b) { _a = a; _b = b; Redraw(); }

    private double W => Math.Max(0, ActualWidth - 2 * Inset);
    private double X(double t) => Inset + (_duration > 0 ? Math.Clamp(t / _duration, 0, 1) : 0) * W;
    private double T(double x) => _duration <= 0 || W <= 0 ? 0 : Math.Clamp((x - Inset) / W, 0, 1) * _duration;

    private void Redraw()
    {
        double mid = ActualHeight / 2;
        _rail.Width = W; Canvas.SetLeft(_rail, Inset); Canvas.SetTop(_rail, mid - 2);
        double x = X(_position);
        _played.Width = Math.Max(0, x - Inset); Canvas.SetLeft(_played, Inset); Canvas.SetTop(_played, mid - 2);
        Canvas.SetLeft(_knob, x - 8); Canvas.SetTop(_knob, mid - 8);
        if (_a is double a)
        {
            double xb = _b is double b ? X(b) : X(a) + 2;
            _ab.Visibility = Visibility.Visible;
            _ab.Width = Math.Max(3, xb - X(a)); Canvas.SetLeft(_ab, X(a)); Canvas.SetTop(_ab, mid - 4);
        }
        else _ab.Visibility = Visibility.Collapsed;
    }

    private void ShowBubble(double x, double t)
    {
        _bubbleText.Text = TimeFormat.Clock(t, forceHours: _duration >= 3600);
        _bubble.Visibility = Visibility.Visible;
        _bubble.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_bubble, Math.Clamp(x - _bubble.DesiredSize.Width / 2, 0, Math.Max(0, ActualWidth - _bubble.DesiredSize.Width)));
        Canvas.SetTop(_bubble, -_bubble.DesiredSize.Height - 6);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_duration <= 0) return;
        _dragging = CapturePointer(e.Pointer);
        Seek(e, !_dragging);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        double x = e.GetCurrentPoint(this).Position.X;
        if (_duration > 0) ShowBubble(x, T(x));
        if (_dragging) Seek(e, false);
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Seek(e, true);
        ReleasePointerCapture(e.Pointer);
        _bubble.Visibility = Visibility.Collapsed;
    }

    private void Seek(PointerRoutedEventArgs e, bool final)
    {
        double t = T(e.GetCurrentPoint(this).Position.X);
        _position = t;
        Redraw();
        SeekRequested?.Invoke(t, final);
    }
}
