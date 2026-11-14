using System.Globalization;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Mashhad.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private readonly SegmentList _segments = new();
    /// <summary>Segments marked on other files during this session, so browsing away does not lose them.</summary>
    private readonly Dictionary<string, List<Segment>> _segmentsByPath = new(StringComparer.OrdinalIgnoreCase);
    private string? _segmentsPath, _editDataPath;
    private KeyframeIndex? _keyframes;
    private MediaProbe? _probe;
    private CancellationTokenSource? _exportCts, _editLoadCts;
    private bool _busy;
    private string? _revealPath;

    private void WireEditor()
    {
        _segments.Changed += UpdateSegmentsUi;
        _editor.CommandRequested += Execute;
        _editor.ExportClicked += () => _ = ExportAsync();
        _editor.ExportFrameClicked += () => _ = ExportFrameAsync();
        _editor.ExportFramesClicked += () => _ = ExportFramesAsync();
        _editor.RemuxClicked += ext => _ = RemuxAsync(ext);
        _editor.InvertClicked += () => { if (!_segments.Invert()) ShowEditStatus(Loc.T("Edit.NoSegments")); };
        _editor.ClearClicked += () => _segments.Clear();
        _editor.SegmentClicked += i => { if (_segments.Select(i) && _segments.Current is { } s) SeekAbsolute(s.Start, exact: true); };
        _editor.SegmentDeleteClicked += i => _segments.RemoveAt(i);
        _editor.OptionsChanged += () =>
        {
            Settings.SnapCutsToKeyframes = _editor.Snap.IsChecked == true;
            Settings.MergeSegmentsOnExport = _editor.Merge.IsChecked == true;
            _editor.SnapToggle.IsChecked = Settings.SnapCutsToKeyframes;
            UpdateExportPreview();
        };
        _editor.CancelClicked += () => _exportCts?.Cancel();
        _editor.RevealClicked += () => RevealInExplorer(_revealPath);
        _editor.Timeline.SeekRequested += (t, final) => { SeekAbsolute(t, exact: true); OnPosition(t); };
        _editor.Timeline.SegmentSelected += i => _segments.Select(i);
        _editor.Timeline.SegmentEdgeDragged += (i, start, t, final) =>
        {
            if (!_segments.Select(i)) return;
            var snapped = start && _editor.Snap.IsChecked == true && _keyframes?.AtOrBefore(t) is double k && t - k < 1.0 ? k : t;
            if (start) _segments.SetStart(snapped); else _segments.SetEnd(t);
            if (final) SeekAbsolute(start ? snapped : t, exact: true);
        };
        _editor.Bin.SelectionChanged += (_, _) =>
        {
            if (_editor.Bin.SelectedItem is ListViewItem { Tag: MediaItem m } && !FolderScanner.PathEquals(m.Path, _loadedPath ?? ""))
            {
                int i = _queue.Items.ToList().FindIndex(x => x.Path == m.Path);
                if (i >= 0) JumpTo(i);
            }
        };
        _editor.Snap.IsChecked = _editor.SnapToggle.IsChecked = Settings.SnapCutsToKeyframes;
        _editor.Merge.IsChecked = Settings.MergeSegmentsOnExport;
        _editor.KeepAll.IsChecked = true;
    }

    // ---- entering edit mode ------------------------------------------------------------------

    /// <summary>Fills the editor for the current video: bin, timeline, keyframes, waveform, thumbnails.</summary>
    private void EnterEditData()
    {
        if (_kind != MediaKind.Video || _loadedPath is null) return;
        _mpv?.Set("pause", true);
        var videos = _queue.Items.Where(i => i.Kind == MediaKind.Video).ToList();
        _editor.BinTitle.Text = _gallery.Folder is { } f ? $"{System.IO.Path.GetFileName(f.TrimEnd('\\', '/'))} · {videos.Count}" : videos.Count.ToString(CultureInfo.InvariantCulture);
        _editor.SetBin(videos, videos.FindIndex(v => v.Path == _loadedPath), m => System.IO.Path.GetExtension(m.Path).TrimStart('.').ToUpperInvariant());
        _editor.Timeline.Duration = _duration;
        UpdateSegmentsUi();
        if (_editDataPath == _loadedPath) return;
        _editDataPath = _loadedPath;
        _editor.Timeline.SetKeyframes(Array.Empty<double>());
        _editor.Timeline.SetWaveform(null);
        _editor.Timeline.SetThumbnails(Array.Empty<(double, ImageSource)>());
        _editLoadCts?.Cancel();
        _editLoadCts = new CancellationTokenSource();
        _ = LoadEditDataAsync(_loadedPath, _editLoadCts.Token);
    }

    private async Task LoadEditDataAsync(string path, CancellationToken ct)
    {
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        if (tools is null) { _editor.KeyframeStatus.Text = Loc.T("Error.FfmpegMissing"); return; }
        var runner = new FfmpegRunner(tools);
        await LoadKeyframesAsync();
        if (ct.IsCancellationRequested || path != _loadedPath) return;
        double duration = _probe?.Duration ?? _duration;

        // waveform (first audio track)
        if (_probe?.Streams.Any(s => s.IsAudio) == true && duration > 0)
        {
            try
            {
                var peaks = await Task.Run(() => runner.RunWithOutputAsync(Waveform.Args(path),
                    s => Waveform.ComputeAsync(s, (long)(duration * Waveform.SampleRate), 1600, ct), ct), ct);
                if (!ct.IsCancellationRequested && path == _loadedPath) _editor.Timeline.SetWaveform(peaks);
                Trace("edit: waveform ready");
            }
            catch (Exception e) when (e is not OperationCanceledException) { Log.Write("Waveform failed: " + e.Message); }
        }

        // thumbnail lane: a frame every 1/20 of the video, cached under %TEMP%
        if (duration > 0)
        {
            try
            {
                const int count = 20;
                var dir = System.IO.Path.Combine(ThumbnailCache.CacheDirectory, ThumbnailCache.StableKey(path));
                Directory.CreateDirectory(dir);
                var frames = new List<(double Time, string File)>();
                for (int i = 0; i < count; i++) frames.Add((duration * (i + 0.5) / count, System.IO.Path.Combine(dir, $"{i:00}.jpg")));
                await Task.Run(() => Parallel.ForEachAsync(frames, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct }, async (f, token) =>
                {
                    if (!File.Exists(f.File)) await runner.RunAsync(FfmpegArgs.Thumbnail(path, f.File, f.Time, 72), 0, null, token).ConfigureAwait(false);
                }), ct);
                if (ct.IsCancellationRequested || path != _loadedPath) return;
                var thumbs = frames.Where(f => File.Exists(f.File))
                    .Select(f => (f.Time, (ImageSource)new BitmapImage(new Uri(f.File)))).ToList();
                _editor.Timeline.SetThumbnails(thumbs);
                Trace("edit: thumbnails ready");
            }
            catch (Exception e) when (e is not OperationCanceledException) { Log.Write("Timeline thumbnails failed: " + e.Message); }
        }
    }

    // ---- segments ----------------------------------------------------------------------------

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

    private void UpdateSegmentsUi()
    {
        var rows = _segments.Segments.Select((s, i) => (
            Name: string.IsNullOrEmpty(s.Name) ? Loc.F("Edit.SegmentN", i + 1) : s.Name!,
            In: TimeFormat.Clock(s.Start, true), Out: TimeFormat.Clock(s.EndOr(_duration), true),
            Dur: s.Length(_duration).ToString("0.00", CultureInfo.InvariantCulture) + " s")).ToList();
        _editor.SetSegmentRows(rows, _segments.CurrentIndex);
        _editor.Timeline.SetSegments(_segments.Segments, _segments.CurrentIndex);
        UpdateExportPreview();
    }

    /// <summary>Shows what Export will do: file count, kept duration and the first FFmpeg command.</summary>
    private void UpdateExportPreview()
    {
        var path = _loadedPath;
        var segments = _segments.Resolved();
        if (path is null || segments.Count == 0 || _kind != MediaKind.Video)
        {
            _editor.Summary.Text = Loc.T("Edit.NoSegments");
            _editor.Command.Text = "";
            _editor.ExportButton.IsEnabled = false;
            return;
        }
        try
        {
            bool snap = _editor.Snap.IsChecked == true;
            var plan = ExportPlanner.Plan(path, segments, _duration, snap ? _keyframes : null, ExportOptionsFromUi(snap));
            double kept = plan.EffectiveSegments.Sum(s => s.Length(_duration));
            _editor.Summary.Text = Loc.F("Edit.PlanSummary", plan.Outputs.Count, TimeFormat.Clock(kept, millis: true));
            var first = plan.Steps[0];
            _editor.Command.Text = FfmpegArgs.Display("ffmpeg", first.Arguments.Where(a => a is not ("-progress" or "pipe:1" or "-nostats" or "-hide_banner" or "-nostdin")))
                                   + (plan.Steps.Count > 1 ? "\n" + Loc.F("Edit.MoreSteps", plan.Steps.Count - 1) : "");
            _editor.ExportButton.IsEnabled = !_busy;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
        {
            _editor.Summary.Text = e.Message;
            _editor.Command.Text = "";
        }
    }

    private ExportOptions ExportOptionsFromUi(bool snap) => new()
    {
        OutputDirectory = NullIfEmpty(Settings.ExportDirectory),
        Merge = _editor.Merge.IsChecked == true,
        SnapToKeyframes = snap,
        KeepAllStreams = _editor.KeepAll.IsChecked != false,
        Extension = _editor.Container.SelectedItem is ComboBoxItem { Tag: string { Length: > 0 } ext } ? ext : null,
        SourceFormat = _probe?.FormatName,
    };

    private void SetIn()
    {
        if (_mode != AppMode.Edit) return;
        _segments.SetStart(_position);
        ShowToast(Loc.F("Osd.In", TimeFormat.Clock(_position, true)));
    }

    private void SetOut()
    {
        if (_mode != AppMode.Edit) return;
        _segments.SetEnd(_position);
        ShowToast(Loc.F("Osd.Out", TimeFormat.Clock(_position, true)));
    }

    private void AddSegment()
    {
        if (_mode == AppMode.Edit) _segments.AddAt(_position);
    }

    private void SplitSegment()
    {
        if (_mode == AppMode.Edit && !_segments.SplitAt(_position)) ShowToast(Loc.T("Osd.NoSegmentHere"));
    }

    private void JumpKeyframe(int direction)
    {
        if (_mpv is null || _kind != MediaKind.Video) return;
        var target = direction < 0 ? _keyframes?.Before(_position) : _keyframes?.After(_position);
        if (target is double t) SeekAbsolute(t, exact: true);
        else if (_keyframes is null) _mpv.TryCommand("seek", direction < 0 ? "-0.05" : "0.05", "relative+keyframes");
    }

    /// <summary>Reads stream info and the keyframe index of the current video in the background.</summary>
    private async Task LoadKeyframesAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video) return;
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        if (tools is null) { _editor.KeyframeStatus.Text = Loc.T("Error.FfmpegMissing"); return; }
        if (_keyframes is not null && _probe is not null) return;
        _editor.KeyframeStatus.Text = Loc.T("Edit.KeyframesLoading");
        try
        {
            var runner = new FfmpegRunner(tools);
            var (probe, keys) = await Task.Run(async () =>
            {
                var p = await runner.ProbeAsync(path).ConfigureAwait(false);
                var k = await runner.ProbeKeyframesAsync(path, p.StartTime).ConfigureAwait(false);
                return (p, k);
            });
            if (_closed || path != _loadedPath) return;
            _probe = probe;
            _keyframes = keys;
            _editor.Timeline.SetKeyframes(keys.Times);
            _editor.KeyframeStatus.Text = Loc.F("Edit.KeyframesLoaded", keys.Count);
            UpdateExportPreview();
            Trace($"edit: {keys.Count} keyframes");
        }
        catch (Exception e)
        {
            Log.Write("Keyframe probe failed: " + e.Message);
            if (path == _loadedPath) _editor.KeyframeStatus.Text = Loc.T("Edit.KeyframesFailed");
        }
    }

    // ---- export ------------------------------------------------------------------------------

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private bool EnsureTools(out FfmpegRunner runner)
    {
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        if (tools is null)
        {
            runner = null!;
            ShowToast(Loc.T("Error.FfmpegMissing"));
            return false;
        }
        runner = new FfmpegRunner(tools);
        return true;
    }

    private void ShowEditStatus(string text, string? reveal = null, double? progress = null)
    {
        _editor.Status.Visibility = Visibility.Visible;
        _editor.StatusText.Text = text;
        _editor.Progress.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
        _editor.Progress.IsIndeterminate = progress is <= 0;
        _editor.Progress.Value = progress ?? 0;
        _editor.StatusCancel.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
        _revealPath = reveal;
        _editor.StatusReveal.Visibility = reveal is null ? Visibility.Collapsed : Visibility.Visible;
        if (_mode != AppMode.Edit) ShowToast(text);
        if (_smokeTest) Log.Write("Status: " + text);
    }

    /// <summary>Runs one FFmpeg job with progress, cancel, error reporting and a "show in folder" link.</summary>
    private async Task RunToolAsync(string title, Func<IProgress<double>, CancellationToken, Task> work, string? revealPath)
    {
        if (_busy) { ShowToast(Loc.T("Status.Busy")); return; }
        _busy = true;
        _editor.ExportButton.IsEnabled = false;
        _exportCts = new CancellationTokenSource();
        var cts = _exportCts;
        ShowEditStatus(title, null, 0);
        var progress = new UiProgress(this, value => ShowEditStatus(title, null, value));
        try
        {
            await Task.Run(() => work(progress, cts.Token));
            ShowEditStatus(Loc.F("Status.Done", revealPath is null ? "" : System.IO.Path.GetFileName(revealPath)), revealPath);
        }
        catch (OperationCanceledException) { ShowEditStatus(Loc.T("Status.Cancelled")); }
        catch (FfmpegException e)
        {
            Log.Write("FFmpeg failed: " + e.Message + Environment.NewLine + e.Log);
            ShowEditStatus(Loc.F("Status.Failed", e.Message));
        }
        catch (Exception e)
        {
            Log.Write("Tool failed: " + e);
            ShowEditStatus(Loc.F("Status.Failed", e.Message));
        }
        finally
        {
            _busy = false;
            _exportCts = null;
            cts.Dispose();
            UpdateExportPreview();
        }
    }

    private async Task ExportAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video) return;
        var segments = _segments.Resolved();
        if (segments.Count == 0) { ShowEditStatus(Loc.T("Edit.NoSegments")); return; }
        if (!EnsureTools(out var runner)) return;
        bool snap = _editor.Snap.IsChecked == true;
        if (snap && _keyframes is null) await LoadKeyframesAsync();
        ExportPlan plan;
        try { plan = ExportPlanner.Plan(path, segments, _duration, snap ? _keyframes : null, ExportOptionsFromUi(snap)); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
        {
            ShowEditStatus(Loc.F("Status.Failed", e.Message));
            return;
        }
        foreach (var step in plan.Steps) Log.Write("Export step: " + FfmpegArgs.Display(runner.Tools.Ffmpeg, step.Arguments));
        await RunToolAsync(Loc.F("Status.Exporting", plan.Steps.Count), async (progress, ct) =>
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
        string output = ExportPlanner.DerivedPath(path, "frame-" + TimeFormat.FileStamp(_position), FfmpegArgs.ExtensionFor(format), NullIfEmpty(Settings.ExportDirectory));
        var args = FfmpegArgs.ExtractFrame(path, output, _position, format);
        await RunToolAsync(Loc.T("Status.ExportingFrame"), (progress, ct) => runner.RunAsync(args, 0, progress, ct), output);
    }

    /// <summary>Every frame of the current segment as numbered images in a new folder.</summary>
    private async Task ExportFramesAsync()
    {
        var path = _loadedPath;
        if (path is null || _kind != MediaKind.Video) return;
        if (_segments.Current is not { } segment) { ShowEditStatus(Loc.T("Edit.NoSegments")); return; }
        if (!EnsureTools(out var runner)) return;
        var format = Settings.ScreenshotFormat switch { "jpg" => FrameFormat.Jpeg, "webp" => FrameFormat.Webp, _ => FrameFormat.Png };
        string folder = ExportPlanner.DerivedPath(path, $"frames-{TimeFormat.FileStamp(segment.Start)}-{TimeFormat.FileStamp(segment.EndOr(_duration))}", "",
            NullIfEmpty(Settings.ExportDirectory), p => File.Exists(p) || Directory.Exists(p));
        double length = segment.Length(_duration);
        var args = FfmpegArgs.ExtractFrames(path, System.IO.Path.Combine(folder, "frame_%06d" + FfmpegArgs.ExtensionFor(format)), segment.Start, segment.End, format);
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
        await RunToolAsync(Loc.F("Status.Remuxing", extension.TrimStart('.').ToUpperInvariant()), (progress, ct) => runner.RunAsync(args, duration, progress, ct), output);
    }

    /// <summary>Reports progress on the UI thread (coalesced).</summary>
    private sealed class UiProgress : IProgress<double>
    {
        private readonly MainWindow _window;
        private readonly Action<double> _apply;
        private int _queued;
        private double _latest;

        public UiProgress(MainWindow window, Action<double> apply) { _window = window; _apply = apply; }

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
