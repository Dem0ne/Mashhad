using Mashhad.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Mashhad.App.Controls;

/// <summary>Seek bar that also draws edit segments and the A-B loop markers.</summary>
public sealed partial class TimelineControl : UserControl
{
    private const double Inset = 8;

    private double _duration, _position;
    private double? _loopA, _loopB;
    private IReadOnlyList<Segment> _segments = Array.Empty<Segment>();
    private int _currentSegment = -1;
    private bool _dragging;

    /// <summary>(seconds, isFinal) — isFinal is false while the user is still dragging.</summary>
    public event Action<double, bool>? SeekRequested;

    public bool IsDragging => _dragging;

    public TimelineControl() => InitializeComponent();

    public double Duration
    {
        get => _duration;
        set { _duration = double.IsNaN(value) ? 0 : Math.Max(0, value); RedrawMarks(); UpdateThumb(); }
    }

    public double Position
    {
        get => _position;
        set { _position = double.IsNaN(value) ? 0 : value; UpdateThumb(); }
    }

    public void SetSegments(IReadOnlyList<Segment> segments, int currentIndex)
    {
        _segments = segments;
        _currentSegment = currentIndex;
        RedrawMarks();
    }

    public void SetLoop(double? a, double? b)
    {
        _loopA = a;
        _loopB = b;
        RedrawMarks();
    }

    private double TrackWidth => Math.Max(0, Surface.ActualWidth - 2 * Inset);

    private double XFor(double time) =>
        Inset + (_duration > 0 ? Math.Clamp(time / _duration, 0, 1) : 0) * TrackWidth;

    private double TimeFor(double x) =>
        TrackWidth <= 0 || _duration <= 0 ? 0 : Math.Clamp((x - Inset) / TrackWidth, 0, 1) * _duration;

    private void UpdateThumb()
    {
        double x = XFor(_position);
        Played.Width = Math.Max(0, x - Inset);
        ThumbShift.X = x - Thumb.Width / 2;
    }

    private void RedrawMarks()
    {
        Marks.Children.Clear();
        double height = Surface.ActualHeight;
        if (height <= 0 || TrackWidth <= 0 || _duration <= 0) return;

        var normal = (Brush)Application.Current.Resources["SegmentBrush"];
        var current = (Brush)Application.Current.Resources["SegmentCurrentBrush"];
        for (int i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            double x1 = XFor(s.Start), x2 = XFor(s.EndOr(_duration));
            var rect = new Rectangle
            {
                Width = Math.Max(2, x2 - x1),
                Height = 12,
                RadiusX = 2,
                RadiusY = 2,
                Fill = i == _currentSegment ? current : normal,
            };
            Canvas.SetLeft(rect, x1);
            Canvas.SetTop(rect, (height - 12) / 2);
            Marks.Children.Add(rect);
        }

        var loop = (Brush)Application.Current.Resources["LoopMarkerBrush"];
        foreach (var t in new[] { _loopA, _loopB })
        {
            if (t is not double time) continue;
            var marker = new Rectangle { Width = 2, Height = 18, Fill = loop };
            Canvas.SetLeft(marker, XFor(time) - 1);
            Canvas.SetTop(marker, (height - 18) / 2);
            Marks.Children.Add(marker);
        }
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawMarks();
        UpdateThumb();
    }

    private void Surface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_duration <= 0) return;
        _dragging = Surface.CapturePointer(e.Pointer);
        Seek(e, final: !_dragging);
        e.Handled = true;
    }

    private void Surface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging) Seek(e, final: false);
    }

    private void Surface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Seek(e, final: true);
        Surface.ReleasePointerCapture(e.Pointer);
    }

    private void Surface_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => _dragging = false;

    private void Seek(PointerRoutedEventArgs e, bool final)
    {
        double time = TimeFor(e.GetCurrentPoint(Surface).Position.X);
        _position = time;
        UpdateThumb();
        SeekRequested?.Invoke(time, final);
    }
}
