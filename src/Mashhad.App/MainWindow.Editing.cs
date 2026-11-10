using System.Globalization;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Mashhad.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private readonly SegmentList _segments = new();
    /// <summary>Segments marked on other files during this session, so browsing away does not lose them.</summary>
    private readonly Dictionary<string, List<Segment>> _segmentsByPath = new(StringComparer.OrdinalIgnoreCase);
    private string? _segmentsPath;
    private bool _editMode;
    private KeyframeIndex? _keyframes;
    private MediaProbe? _probe;
    private CancellationTokenSource? _exportCts;
    private bool _busy;
    private bool _syncingSegments;

    // ---- edit mode ------------------------------------------------------------------------

    private void SetEditMode(bool on)
    {
        if (_editMode == on) return;
        _editMode = on && _kind == MediaKind.Video && _queue.Current is not null;
        EditToggle.IsChecked = _editMode;
        EditBar.Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed;
        Timeline.SetSegments(_editMode ? _segments.Segments : Array.Empty<Segment>(), _segments.CurrentIndex);
        if (_editMode)
        {
            SnapCheck.IsChecked = Settings.SnapCutsToKeyframes;
            MergeCheck.IsChecked = Settings.MergeSegmentsOnExport;
            _mpv?.Set("pause", true);
            if (_fileReady && _keyframes is null) _ = LoadKeyframesAsync();
        }
        else if (_panel == SidePanelKind.Segments)
        {
            ShowSidePanel(SidePanelKind.None);
        }
        UpdateSegmentsUi();
        OnPosition(_position);
    }

    private void RememberSegments()
    {
        if (_segmentsPath is null) return;
        if (_segments.Count > 0) _segmentsByPath[_segmentsPath] = _segments.Segments.ToList();
        else _segmentsByPath.Remove(_segmentsPath);
    }

    private void RestoreSegments(string path)
    {
        _segmentsPath = path;
        _segments.Duration = 0;
        _segments.Load(_segmentsByPath.TryGetValue(path, out var saved) ? saved : new List<Segment>());
    }

    private void Segments_Changed() => UpdateSegmentsUi();

    private void UpdateSegmentsUi()
    {
        if (SegmentsView is null) return;
        Timeline.SetSegments(_editMode ? _segments.Segments : Array.Empty<Segment>(), _segments.CurrentIndex);

        double total = _segments.Segments.Sum(s => s.Length(_duration));
        SegmentSummary.Text = _segments.Count == 0
            ? Loc.T("Edit.NoSegments")
            : Loc.F("Edit.Summary", _segments.Count, TimeFormat.Clock(total, millis: true));

        _syncingSegments = true;
        SegmentsView.ItemsSource = _segments.Segments.Select((s, i) =>
            $"{i + 1}.  {TimeFormat.Clock(s.Start, true)}  →  {TimeFormat.Clock(s.EndOr(_duration), true)}   ({TimeFormat.Clock(s.Length(_duration), true)})")
            .ToList();
        SegmentsView.SelectedIndex = _segments.CurrentIndex;
        _syncingSegments = false;
    }

    private void SegmentsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSegments || SegmentsView.SelectedIndex < 0) return;
        if (_segments.Select(SegmentsView.SelectedIndex) && _segments.Current is { } seg) SeekAbsolute(seg.Start, exact: true);
    }

    private void SetIn()
    {
        if (!_editMode) return;
        _segments.SetStart(_position);
        ShowOsd(Loc.F("Osd.In", TimeFormat.Clock(_position, true)));
    }

    private void SetOut()
    {
        if (!_editMode) return;
        _segments.SetEnd(_position);
        ShowOsd(Loc.F("Osd.Out", TimeFormat.Clock(_position, true)));
    }

    private void AddSegment()
    {
        if (_editMode) _segments.AddAt(_position);
    }

    private void SplitSegment()
    {
        if (_editMode && !_segments.SplitAt(_position)) ShowOsd(Loc.T("Osd.NoSegmentHere"));
    }

    private void JumpKeyframe(int direction)
    {
        if (_mpv is null || _kind != MediaKind.Video) return;
        var target = direction < 0 ? _keyframes?.Before(_position) : _keyframes?.After(_position);
        if (target is double t) SeekAbsolute(t, exact: true);
        else if (_keyframes is null)
            // Index not loaded (yet): let mpv find the neighbouring keyframe itself.
            _mpv.TryCommand("seek", direction < 0 ? "-0.05" : "0.05", "relative+keyframes");
    }

    /// <summary>Reads stream info and the keyframe index of the current video in the background.</summary>
    private async Task LoadKeyframesAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video) return;
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        if (tools is null)
        {
            KeyframeStatus.Text = Loc.T("Error.FfmpegMissing");
            return;
        }
        KeyframeStatus.Text = Loc.T("Edit.KeyframesLoading");
        try
        {
            var runner = new FfmpegRunner(tools);
            var (probe, keys) = await Task.Run(async () =>
            {
                var p = await runner.ProbeAsync(path).ConfigureAwait(false);
                var k = await runner.ProbeKeyframesAsync(path, p.StartTime).ConfigureAwait(false);
                return (p, k);
            });
            if (_closed || path != _loadedPath) return; // the user moved on
            _probe = probe;
            _keyframes = keys;
            KeyframeStatus.Text = Loc.F("Edit.KeyframesLoaded", keys.Count);
        }
        catch (Exception e)
        {
            Log.Write("Keyframe probe failed: " + e.Message);
            if (path == _loadedPath) KeyframeStatus.Text = Loc.T("Edit.KeyframesFailed");
        }
    }

    // ---- export ---------------------------------------------------------------------------

    private bool EnsureTools(out FfmpegRunner runner)
    {
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        if (tools is null)
        {
            runner = null!;
            ShowStatus(Loc.T("Error.FfmpegMissing"));
            return false;
        }
        runner = new FfmpegRunner(tools);
        return true;
    }

    /// <summary>Runs one FFmpeg job with progress, cancel, error reporting and a "show in folder" link.</summary>
    private async Task RunToolAsync(string title, Func<IProgress<double>, CancellationToken, Task> work, string? revealPath)
    {
        if (_busy)
        {
            ShowStatus(Loc.T("Status.Busy"));
            return;
        }
        _busy = true;
        _exportCts = new CancellationTokenSource();
        var cts = _exportCts;
        ShowProgress(title, 0);
        var progress = new UiProgress(this, value => ShowProgress(title, value));
        try
        {
            await Task.Run(() => work(progress, cts.Token));
            ShowStatus(Loc.F("Status.Done", revealPath is null ? "" : Path.GetFileName(revealPath)), revealPath);
        }
        catch (OperationCanceledException)
        {
            ShowStatus(Loc.T("Status.Cancelled"));
        }
        catch (FfmpegException e)
        {
            Log.Write("FFmpeg failed: " + e.Message + Environment.NewLine + e.Log);
            ShowStatus(Loc.F("Status.Failed", e.Message));
        }
        catch (Exception e)
        {
            Log.Write("Tool failed: " + e);
            ShowStatus(Loc.F("Status.Failed", e.Message));
        }
        finally
        {
            _busy = false;
            _exportCts = null;
            cts.Dispose();
        }
    }

    private async Task ExportAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video) return;
        var segments = _segments.Resolved();
        if (segments.Count == 0)
        {
            ShowStatus(Loc.T("Edit.NoSegments"));
            return;
        }
        if (!EnsureTools(out var runner)) return;

        bool snap = SnapCheck.IsChecked == true;
        if (snap && _keyframes is null) await LoadKeyframesAsync();

        ExportPlan plan;
        try
        {
            plan = ExportPlanner.Plan(path, segments, _duration, snap ? _keyframes : null, new ExportOptions
            {
                OutputDirectory = NullIfEmpty(Settings.ExportDirectory),
                Merge = MergeCheck.IsChecked == true,
                SnapToKeyframes = snap,
                SourceFormat = _probe?.FormatName,
            });
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
        {
            ShowStatus(Loc.F("Status.Failed", e.Message));
            return;
        }
        foreach (var step in plan.Steps)
            Log.Write("Export step: " + FfmpegArgs.Display(runner.Tools.Ffmpeg, step.Arguments));

        string title = Loc.F("Status.Exporting", plan.Steps.Count);
        await RunToolAsync(title, async (progress, ct) =>
        {
            var stepProgress = new InlineProgress<ExportProgress>(p => progress.Report(p.Overall));
            await new Exporter(runner).RunAsync(plan, stepProgress, ct).ConfigureAwait(false);
        }, plan.Outputs.LastOrDefault());
    }

    private async Task ExportFrameAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video || !EnsureTools(out var runner)) return;
        var format = Settings.ScreenshotFormat switch { "jpg" => FrameFormat.Jpeg, "webp" => FrameFormat.Webp, _ => FrameFormat.Png };
        string output = ExportPlanner.DerivedPath(path, "frame-" + TimeFormat.FileStamp(_position), FfmpegArgs.ExtensionFor(format),
            NullIfEmpty(Settings.ExportDirectory));
        var args = FfmpegArgs.ExtractFrame(path, output, _position, format);
        await RunToolAsync(Loc.T("Status.ExportingFrame"), (progress, ct) => runner.RunAsync(args, 0, progress, ct), output);
    }

    /// <summary>Every frame of the current segment as numbered images in a new folder.</summary>
    private async Task ExportFramesAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video) return;
        if (_segments.Current is not { } segment)
        {
            ShowStatus(Loc.T("Edit.NoSegments"));
            return;
        }
        if (!EnsureTools(out var runner)) return;
        var format = Settings.ScreenshotFormat switch { "jpg" => FrameFormat.Jpeg, "webp" => FrameFormat.Webp, _ => FrameFormat.Png };
        string folder = ExportPlanner.DerivedPath(path,
            $"frames-{TimeFormat.FileStamp(segment.Start)}-{TimeFormat.FileStamp(segment.EndOr(_duration))}", "",
            NullIfEmpty(Settings.ExportDirectory), p => File.Exists(p) || Directory.Exists(p));
        double length = segment.Length(_duration);
        var args = FfmpegArgs.ExtractFrames(path, Path.Combine(folder, "frame_%06d" + FfmpegArgs.ExtensionFor(format)),
            segment.Start, segment.End, format);
        await RunToolAsync(Loc.T("Status.ExportingFrames"), (progress, ct) =>
        {
            Directory.CreateDirectory(folder);
            return runner.RunAsync(args, length, progress, ct);
        }, folder);
    }

    private async Task RemuxAsync(string extension)
    {
        var path = _loadedPath;
        if (path is null || !IsTimed || !EnsureTools(out var runner)) return;
        string output = ExportPlanner.DerivedPath(path, "remux", extension, NullIfEmpty(Settings.ExportDirectory));
        var args = FfmpegArgs.Remux(path, output);
        double duration = _duration;
        await RunToolAsync(Loc.F("Status.Remuxing", extension.TrimStart('.').ToUpperInvariant()),
            (progress, ct) => runner.RunAsync(args, duration, progress, ct), output);
    }

    // ---- handlers -------------------------------------------------------------------------

    private void EditToggle_Click(object sender, RoutedEventArgs e) => SetEditMode(EditToggle.IsChecked == true);
    private void SetIn_Click(object sender, RoutedEventArgs e) => SetIn();
    private void SetOut_Click(object sender, RoutedEventArgs e) => SetOut();
    private void AddSegment_Click(object sender, RoutedEventArgs e) => AddSegment();
    private void SplitSegment_Click(object sender, RoutedEventArgs e) => SplitSegment();
    private void RemoveSegment_Click(object sender, RoutedEventArgs e) => _segments.RemoveCurrent();
    private void PrevKeyframe_Click(object sender, RoutedEventArgs e) => JumpKeyframe(-1);
    private void NextKeyframe_Click(object sender, RoutedEventArgs e) => JumpKeyframe(1);
    private void Export_Click(object sender, RoutedEventArgs e) => _ = ExportAsync();
    private void ExportFrame_Click(object sender, RoutedEventArgs e) => _ = ExportFrameAsync();
    private void ExportFrames_Click(object sender, RoutedEventArgs e) => _ = ExportFramesAsync();
    private void ShowSegments_Click(object sender, RoutedEventArgs e) => ShowSidePanel(SidePanelKind.Segments);
    private void ClearSegments_Click(object sender, RoutedEventArgs e) => _segments.Clear();

    private void InvertSegments_Click(object sender, RoutedEventArgs e)
    {
        if (!_segments.Invert()) ShowStatus(Loc.T("Edit.NoSegments"));
    }

    private void Remux_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string ext }) _ = RemuxAsync(ext);
    }

    private void EditOption_Click(object sender, RoutedEventArgs e)
    {
        Settings.SnapCutsToKeyframes = SnapCheck.IsChecked == true;
        Settings.MergeSegmentsOnExport = MergeCheck.IsChecked == true;
    }

    /// <summary>Reports progress on the UI thread.</summary>
    private sealed class UiProgress : IProgress<double>
    {
        private readonly MainWindow _window;
        private readonly Action<double> _apply;
        private int _queued;
        private double _latest;

        public UiProgress(MainWindow window, Action<double> apply)
        {
            _window = window;
            _apply = apply;
        }

        public void Report(double value)
        {
            Volatile.Write(ref _latest, value);
            if (Interlocked.Exchange(ref _queued, 1) != 0) return;
            _window.RunOnUi(() =>
            {
                Interlocked.Exchange(ref _queued, 0);
                if (_window._busy) _apply(Volatile.Read(ref _latest));
            });
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _action;
        public InlineProgress(Action<T> action) => _action = action;
        public void Report(T value) => _action(value);
    }
}
