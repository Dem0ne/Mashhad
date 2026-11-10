using System.Globalization;
using Mashhad.Core.Playback;
using Mashhad.Editing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Mashhad.App.Controls;

/// <summary>
/// Studio-style multi-lane timeline for lossless editing: ruler, segments (draggable edges),
/// video thumbnails, keyframes and the audio waveform. Time runs left → right in every language.
/// </summary>
public sealed class EditTimeline : Grid
{
    private const double HeaderW = 136, RulerH = 26, SegH = 40, VideoH = 56, KeysH = 22;
    private static readonly Windows.UI.Color[] SegColors = { Ui.Rose, Ui.Crimson, Ui.Wine };

    private readonly Canvas _lanes = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Grid _headers = new() { Width = HeaderW, HorizontalAlignment = HorizontalAlignment.Left };
    private double _duration, _position;
    private IReadOnlyList<Segment> _segments = Array.Empty<Segment>();
    private int _current = -1;
    private IReadOnlyList<double> _keyframes = Array.Empty<double>();
    private float[]? _peaks;
    private IReadOnlyList<(double Time, ImageSource Image)> _thumbs = Array.Empty<(double, ImageSource)>();
    private readonly Rectangle _playhead = new() { Width = 2, Fill = new SolidColorBrush(Colors.White) };
    private readonly Polygon _playheadCap = new() { Fill = new SolidColorBrush(Colors.White) };
    private int _dragEdge = -1; // segment index
    private bool _dragStart, _dragSeek;

    public event Action<double, bool>? SeekRequested;
    public event Action<int>? SegmentSelected;
    /// <summary>(segment, isStart, time, isFinal)</summary>
    public event Action<int, bool, double, bool>? SegmentEdgeDragged;

    public EditTimeline()
    {
        FlowDirection = FlowDirection.LeftToRight;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderW) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Children.Add(_headers);
        SetColumn(_lanes, 1);
        Children.Add(_lanes);
        _lanes.SizeChanged += (_, _) => Redraw();
        _lanes.PointerPressed += OnPressed;
        _lanes.PointerMoved += OnMoved;
        _lanes.PointerReleased += OnReleased;
        _lanes.PointerCaptureLost += (_, _) => { _dragEdge = -1; _dragSeek = false; };
        BuildHeaders();
    }

    public double Duration { get => _duration; set { _duration = double.IsFinite(value) ? Math.Max(0, value) : 0; Redraw(); } }

    public double Position
    {
        get => _position;
        set { _position = double.IsFinite(value) ? value : 0; PlacePlayhead(); }
    }

    public void SetSegments(IReadOnlyList<Segment> segments, int current) { _segments = segments; _current = current; Redraw(); }
    public void SetKeyframes(IReadOnlyList<double> times) { _keyframes = times; Redraw(); }
    public void SetWaveform(float[]? peaks) { _peaks = peaks; Redraw(); }
    public void SetThumbnails(IReadOnlyList<(double Time, ImageSource Image)> thumbs) { _thumbs = thumbs; Redraw(); }

    public void Clear()
    {
        _segments = Array.Empty<Segment>(); _keyframes = Array.Empty<double>(); _peaks = null;
        _thumbs = Array.Empty<(double, ImageSource)>(); _duration = 0; _position = 0;
        Redraw();
    }

    private double LaneW => Math.Max(1, _lanes.ActualWidth);
    private double AudioH => Math.Max(30, _lanes.ActualHeight - RulerH - SegH - VideoH - KeysH);
    private double X(double t) => _duration > 0 ? Math.Clamp(t / _duration, 0, 1) * LaneW : 0;
    private double T(double x) => _duration > 0 ? Math.Clamp(x / LaneW, 0, 1) * _duration : 0;

    private void BuildHeaders()
    {
        _headers.Children.Clear();
        _headers.RowDefinitions.Clear();
        foreach (var h in new[] { RulerH, SegH, VideoH, KeysH })
            _headers.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h) });
        _headers.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var rows = new (string Glyph, string Key, string? Extra)[] { ("", "", null), (Ui.G.Cut, "Timeline.Segments", null), (Ui.G.Video, "Timeline.Video", null), ("◆", "Timeline.Keys", null), (Ui.G.Audio, "Timeline.Audio", null) };
        for (int i = 1; i < rows.Length; i++)
        {
            var (glyph, key, _) = rows[i];
            UIElement icon = glyph == "◆" ? Ui.Text("◆", 11, false, 0.7) : Ui.Icon(glyph, 13);
            var cell = new Border
            {
                Child = Ui.Row(8, icon, Ui.LText(key, 12, true, 0.85)),
                Padding = new Thickness(12, 0, 8, 0),
                Background = Ui.White(0.025),
                BorderBrush = Ui.White(0.05),
                BorderThickness = new Thickness(0, 0, 1, 1),
            };
            SetRow(cell, i);
            _headers.Children.Add(cell);
        }
    }

    private void Redraw()
    {
        _lanes.Children.Clear();
        double w = LaneW;
        double segTop = RulerH, vidTop = segTop + SegH, keyTop = vidTop + VideoH, audTop = keyTop + KeysH, audH = AudioH;

        // lane backgrounds
        foreach (var (top, h) in new[] { (segTop, SegH), (vidTop, VideoH), (keyTop, KeysH), (audTop, audH) })
            Add(new Rectangle { Width = w, Height = h, Fill = Ui.White(0.02), Stroke = Ui.White(0.04), StrokeThickness = 0.5 }, 0, top);

        if (_duration > 0)
        {
            DrawRuler(w);
            // thumbnails
            if (_thumbs.Count > 0)
            {
                double tw = w / _thumbs.Count;
                for (int i = 0; i < _thumbs.Count; i++)
                    Add(new Image { Source = _thumbs[i].Image, Width = tw + 0.5, Height = VideoH - 2, Stretch = Stretch.UniformToFill }, i * tw, vidTop + 1);
            }
            // keyframes as one path of diamonds
            if (_keyframes.Count > 0)
            {
                var g = new PathGeometry();
                double last = -10, cy = keyTop + KeysH / 2;
                foreach (var t in _keyframes)
                {
                    double x = X(t);
                    if (x - last < 3) continue; // too dense to tell apart
                    last = x;
                    var f = new PathFigure { StartPoint = new Windows.Foundation.Point(x, cy - 4), IsClosed = true, IsFilled = true };
                    f.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x + 4, cy) });
                    f.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, cy + 4) });
                    f.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x - 4, cy) });
                    g.Figures.Add(f);
                }
                Add(new Microsoft.UI.Xaml.Shapes.Path { Data = g, Fill = Ui.White(0.6) }, 0, 0);
            }
            // waveform
            if (_peaks is { Length: > 0 } peaks)
            {
                var g = new PathGeometry();
                double mid = audTop + audH / 2, half = audH / 2 - 3;
                int cols = (int)Math.Max(1, w / 2);
                for (int c = 0; c < cols; c++)
                {
                    int b0 = c * peaks.Length / cols, b1 = Math.Max(b0 + 1, (c + 1) * peaks.Length / cols);
                    float p = 0;
                    for (int b = b0; b < b1 && b < peaks.Length; b++) p = Math.Max(p, peaks[b]);
                    double a = Math.Max(0.5, p * half), x = c * 2 + 0.5;
                    var f = new PathFigure { StartPoint = new Windows.Foundation.Point(x, mid - a) };
                    f.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, mid + a) });
                    g.Figures.Add(f);
                }
                Add(new Microsoft.UI.Xaml.Shapes.Path { Data = g, Stroke = Ui.Solid(0xB0, 0xFF, 0x7E, 0x96), StrokeThickness = 1.2 }, 0, 0);
            }
            // dim what is not kept
            if (_segments.Count > 0)
            {
                var kept = _segments.Select(s => (A: X(s.Start), B: X(s.EndOr(_duration)))).OrderBy(s => s.A).ToList();
                double cursor = 0;
                foreach (var (a, b) in kept.Append((w, w)))
                {
                    if (a > cursor)
                        Add(new Rectangle { Width = a - cursor, Height = VideoH + KeysH + audH, Fill = Ui.Black(0.55) }, cursor, vidTop);
                    cursor = Math.Max(cursor, b);
                }
            }
            // segments
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                double a = X(s.Start), b = X(s.EndOr(_duration));
                bool cur = i == _current;
                var block = new Border
                {
                    Width = Math.Max(4, b - a),
                    Height = SegH - 10,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(SegColors[i % SegColors.Length]),
                    BorderBrush = cur ? new SolidColorBrush(Colors.White) : null,
                    BorderThickness = new Thickness(cur ? 2 : 0),
                    Child = new TextBlock
                    {
                        Text = $"{i + 1}" + (string.IsNullOrEmpty(s.Name) ? "" : " · " + s.Name),
                        FontSize = 11.5, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(8, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                };
                Add(block, a, segTop + 5);
                if (cur)
                {
                    Add(new Rectangle { Width = 6, Height = SegH - 6, RadiusX = 3, RadiusY = 3, Fill = new SolidColorBrush(Colors.White) }, a - 3, segTop + 3);
                    Add(new Rectangle { Width = 6, Height = SegH - 6, RadiusX = 3, RadiusY = 3, Fill = new SolidColorBrush(Colors.White) }, b - 3, segTop + 3);
                }
            }
        }
        _lanes.Children.Add(_playhead);
        _lanes.Children.Add(_playheadCap);
        PlacePlayhead();
    }

    private void DrawRuler(double w)
    {
        double[] steps = { 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
        double step = steps.FirstOrDefault(s => _duration / s <= Math.Max(4, w / 90), 3600);
        for (double t = 0; t <= _duration + 1e-6; t += step / 5)
        {
            bool major = Math.Abs(t / step - Math.Round(t / step)) < 1e-6;
            double x = X(t);
            Add(new Rectangle { Width = 1, Height = major ? 9 : 4, Fill = Ui.White(major ? 0.35 : 0.18) }, x, RulerH - (major ? 9 : 4));
            if (major && x < w - 30)
                Add(new TextBlock { Text = TimeFormat.Clock(t, millis: step < 1), FontSize = 10.5, Opacity = 0.55, FontFamily = new FontFamily("Consolas") }, x + 3, 3);
        }
    }

    private void PlacePlayhead()
    {
        if (_lanes.ActualHeight <= 0) return;
        double x = X(_position);
        _playhead.Height = _lanes.ActualHeight - 4;
        Canvas.SetLeft(_playhead, x - 1);
        Canvas.SetTop(_playhead, 4);
        _playheadCap.Points = new PointCollection { new(x - 6, 0), new(x + 6, 0), new(x + 6, 6), new(x, 12), new(x - 6, 6) };
        _playhead.Visibility = _playheadCap.Visibility = _duration > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add(FrameworkElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        e.IsHitTestVisible = false;
        _lanes.Children.Add(e);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_duration <= 0) return;
        var p = e.GetCurrentPoint(_lanes).Position;
        if (p.Y >= RulerH && p.Y < RulerH + SegH)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                double a = X(_segments[i].Start), b = X(_segments[i].EndOr(_duration));
                if (Math.Abs(p.X - a) <= 6 || Math.Abs(p.X - b) <= 6)
                {
                    _dragEdge = i;
                    _dragStart = Math.Abs(p.X - a) <= Math.Abs(p.X - b);
                    _lanes.CapturePointer(e.Pointer);
                    SegmentSelected?.Invoke(i);
                    e.Handled = true;
                    return;
                }
                if (p.X > a && p.X < b) SegmentSelected?.Invoke(i);
            }
        }
        _dragSeek = _lanes.CapturePointer(e.Pointer);
        SeekTo(p.X, !_dragSeek);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(_lanes).Position;
        if (_dragEdge >= 0) SegmentEdgeDragged?.Invoke(_dragEdge, _dragStart, T(p.X), false);
        else if (_dragSeek) SeekTo(p.X, false);
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(_lanes).Position;
        if (_dragEdge >= 0) SegmentEdgeDragged?.Invoke(_dragEdge, _dragStart, T(p.X), true);
        else if (_dragSeek) SeekTo(p.X, true);
        _dragEdge = -1;
        _dragSeek = false;
        _lanes.ReleasePointerCapture(e.Pointer);
    }

    private void SeekTo(double x, bool final)
    {
        _position = T(x);
        PlacePlayhead();
        SeekRequested?.Invoke(_position, final);
    }
}
