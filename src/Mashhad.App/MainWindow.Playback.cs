using System.Globalization;
using Mashhad.App.Native;
using Mashhad.Core.Imaging;
using Mashhad.Core.Media;
using Mashhad.Core.Mpv;
using Mashhad.Core.Playback;
using Mashhad.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private MediaKind _kind = MediaKind.Unknown;
    private string? _loadedPath;
    private double _duration, _position;
    private bool _paused = true, _muted, _fileReady;
    private double _volume = 100, _speed = 1;
    private double? _loopA, _loopB;
    private int _positionQueued;
    private double _pendingPosition;
    private List<string> _pendingSubtitles = new();
    private bool _suppressVolumeEvent;

    private bool IsTimed => _kind.IsTimed();

    // ---- engine ---------------------------------------------------------------------------

    private void InitializePlayer()
    {
        if (!MpvPlayer.IsAvailable(out var error))
        {
            Log.Write("libmpv not available: " + error);
            EmptyError.Text = Loc.F("Error.MpvMissing", Path.Combine(AppContext.BaseDirectory, "mpv"));
            EmptyError.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            _host = new VideoHost(_hwnd);
            _host.MouseMove += Host_MouseMove;
            _host.MouseDown += Host_MouseDown;
            _host.MouseUp += Host_MouseUp;
            _host.DoubleClick += (_, _) => SetFullscreen(!_isFullscreen);
            _host.Wheel += Host_Wheel;
            _host.FilesDropped += files => OpenPaths(files);
            SyncVideoHostBounds();

            string shots = string.IsNullOrWhiteSpace(Settings.ScreenshotDirectory)
                ? AppPaths.DefaultScreenshotDirectory
                : Settings.ScreenshotDirectory;
            var options = MpvOptions.Build(Settings, _host.Handle.ToInt64(), shots);
            _mpv = new MpvPlayer(options, MpvOptions.BuildOptional(embedded: true), _smokeTest ? "v" : "warn");

            _mpv.PropertyChanged += Mpv_PropertyChanged;
            _mpv.FileLoaded += () => RunOnUi(OnFileLoaded);
            _mpv.EndFile += reason => RunOnUi(() => OnEndFile(reason));
            _mpv.VideoReconfig += () => RunOnUi(() => { _host?.DisableChildInput(); if (_kind.IsImageLike()) OnViewportResized(); });
            _mpv.LogMessage += (level, prefix, text) =>
            {
                if (_smokeTest || level is "error" or "fatal") Log.Write($"mpv[{level}] {prefix}: {text}");
            };

            foreach (var (name, format) in new (string, MpvFormat)[]
                     {
                         ("time-pos", MpvFormat.Double), ("duration", MpvFormat.Double), ("pause", MpvFormat.Flag),
                         ("eof-reached", MpvFormat.Flag), ("volume", MpvFormat.Double), ("mute", MpvFormat.Flag),
                         ("speed", MpvFormat.Double), ("ab-loop-a", MpvFormat.Double), ("ab-loop-b", MpvFormat.Double),
                         ("path", MpvFormat.String),
                     })
                _mpv.Observe(name, format);

            _volume = Settings.Volume;
            _muted = Settings.Muted;
            UpdateVolumeUi();
            _host.DisableChildInput();
            Log.Write($"libmpv client API {MpvPlayer.ApiVersion} initialised, hwdec={Settings.HardwareDecoding}");
        }
        catch (Exception e)
        {
            Log.Write("Player initialisation failed: " + e);
            _mpv?.Dispose();
            _mpv = null;
            EmptyError.Text = Loc.F("Error.PlayerInit", e.Message);
            EmptyError.Visibility = Visibility.Visible;
        }
        UpdateVideoHostVisibility();
        UpdateAbout();
    }

    /// <summary>mpv event thread: coalesce the high-frequency position updates, forward the rest.</summary>
    private void Mpv_PropertyChanged(string name, object? value)
    {
        if (name == "time-pos")
        {
            if (value is double pos)
            {
                Volatile.Write(ref _pendingPosition, pos);
                if (Interlocked.Exchange(ref _positionQueued, 1) == 0)
                    RunOnUi(() =>
                    {
                        Interlocked.Exchange(ref _positionQueued, 0);
                        OnPosition(Volatile.Read(ref _pendingPosition));
                    });
            }
            return;
        }
        RunOnUi(() => OnProperty(name, value));
    }

    private void OnPosition(double position)
    {
        _position = position;
        if (!IsTimed) return;
        if (!Timeline.IsDragging) Timeline.Position = position;
        PositionText.Text = TimeFormat.Clock(position, millis: _editMode, forceHours: _duration >= 3600);
    }

    private void OnProperty(string name, object? value)
    {
        switch (name)
        {
            case "duration":
                _duration = value as double? ?? 0;
                Timeline.Duration = _duration;
                _segments.Duration = _duration;
                DurationText.Text = TimeFormat.Clock(_duration, forceHours: _duration >= 3600);
                break;
            case "pause":
                _paused = value as bool? ?? true;
                PlayIcon.Glyph = _paused ? "" : "";
                break;
            case "eof-reached":
                if (value is true) OnReachedEnd();
                break;
            case "volume":
                _volume = value as double? ?? _volume;
                UpdateVolumeUi();
                break;
            case "mute":
                _muted = value as bool? ?? false;
                UpdateVolumeUi();
                break;
            case "speed":
                _speed = value as double? ?? 1;
                SpeedButton.Content = _speed.ToString("0.##", CultureInfo.InvariantCulture) + "×";
                break;
            case "ab-loop-a":
                _loopA = value as double?;
                Timeline.SetLoop(_loopA, _loopB);
                break;
            case "ab-loop-b":
                _loopB = value as double?;
                Timeline.SetLoop(_loopA, _loopB);
                break;
            case "path":
                // Something other than us changed the file (e.g. a drop handled inside mpv): follow it.
                if (value is string path && _loadedPath is not null && !FolderScanner.PathEquals(path, _loadedPath) && File.Exists(path))
                    OpenPaths(new[] { path });
                break;
        }
    }

    // ---- opening --------------------------------------------------------------------------

    public void OpenPaths(IReadOnlyList<string> paths)
    {
        if (_closed) return;
        OpenRequest request;
        try
        {
            request = OpenRequest.Resolve(paths, Settings.Sort, Settings.SortDescending);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowStatus(Loc.F("Error.Open", e.Message));
            return;
        }

        if (request.Items.Count == 0)
        {
            // Only subtitles were dropped on a playing video: attach them.
            if (request.Subtitles.Count > 0 && _mpv is not null && _fileReady)
            {
                foreach (var sub in request.Subtitles) _mpv.TryCommand("sub-add", sub, "select");
                ShowOsd(Loc.T("Osd.SubtitleLoaded"));
            }
            else
            {
                ShowStatus(Loc.T("Error.NothingToOpen"));
            }
            return;
        }

        StopSlideshow();
        _pendingSubtitles = request.Subtitles;
        _queue.Load(request.Items, request.Index, request.Source);
        if (_queue.Shuffle != ShuffleItem.IsChecked) _queue.Shuffle = ShuffleItem.IsChecked;
        LoadCurrent();
        RefreshPlaylistPanel();
    }

    /// <summary>Shows the queue's current item.</summary>
    private void LoadCurrent()
    {
        var item = _queue.Current;
        if (item is null) return;

        SaveResumePosition();
        RememberSegments();
        if (_cropMode) EndCrop();

        _kind = item.Kind == MediaKind.Unknown ? MediaKind.Video : item.Kind;
        _loadedPath = item.Path;
        _fileReady = false;
        _duration = 0;
        _position = 0;
        _view = ImageView.Fit;
        _userRotation = 0;
        _keyframes = null;
        _probe = null;
        Timeline.Duration = 0;
        Timeline.Position = 0;
        Timeline.SetLoop(null, null);
        PositionText.Text = DurationText.Text = "00:00";
        RestoreSegments(item.Path);

        UpdateTitle();
        UpdateModeUi();
        UpdateVideoHostVisibility();
        SyncPlaylistSelection();

        if (_mpv is not null)
        {
            _mpv.Set("video-zoom", 0.0);
            _mpv.Set("video-pan-x", 0.0);
            _mpv.Set("video-pan-y", 0.0);
            _mpv.Set("video-rotate", "0");
            _mpv.Set("ab-loop-a", "no");
            _mpv.Set("ab-loop-b", "no");
            _mpv.Set("speed", 1.0);
            _mpv.Set("loop-file", _kind == MediaKind.AnimatedImage || LoopFileItem.IsChecked ? "inf" : "no");
            _mpv.Set("pause", false);

            double? resume = IsTimed && Settings.ResumePlayback && !_slideshowOn ? _resume.GetPosition(item.Path) : null;
            _mpv.Set("start", resume is double r ? r.ToString("0.###", CultureInfo.InvariantCulture) : "none");
            if (!_mpv.TryCommand("loadfile", item.Path))
                ShowStatus(Loc.F("Error.Open", item.FileName));
        }

        // Warm the neighbours so the next arrow press opens without disk latency.
        _ = _preloader.Preload(_queue.Peek(1), _queue.Peek(-1));
        if (_panel == SidePanelKind.Info) RefreshInfoPanel();
    }

    private void OnFileLoaded()
    {
        if (_mpv is null) return;
        _fileReady = true;
        _mpv.Set("start", "none");
        _host?.DisableChildInput();
        foreach (var sub in _pendingSubtitles) _mpv.TryCommand("sub-add", sub, "select");
        _pendingSubtitles = new List<string>();

        if (_kind.IsImageLike()) OnViewportResized();
        if (_editMode && _kind == MediaKind.Video) _ = LoadKeyframesAsync();
        if (_panel == SidePanelKind.Info) RefreshInfoPanel();
        if (_slideshowOn) ScheduleSlideshow();
    }

    private void OnEndFile(MpvEndFileReason reason)
    {
        if (reason != MpvEndFileReason.Error) return;
        var name = _queue.Current?.FileName ?? "";
        Log.Write("mpv could not play: " + _loadedPath);
        ShowStatus(Loc.F("Error.Play", name));
        if (_slideshowOn) SlideshowAdvance();
    }

    /// <summary>keep-open stopped on the last frame.</summary>
    private void OnReachedEnd()
    {
        if (!IsTimed || !_fileReady) return;
        _resume.Forget(_loadedPath ?? "");
        if (_slideshowOn) { SlideshowAdvance(); return; }
        // A playlist plays through; a folder gallery stays on the finished video.
        if (_queue.Source == QueueSource.Playlist && _queue.PeekIndex(1) > _queue.CurrentIndex) Navigate(1);
    }

    private void SaveResumePosition()
    {
        if (_loadedPath is null || !IsTimed || !_fileReady || _kind != MediaKind.Video) return;
        if (Settings.ResumePlayback) _resume.Update(_loadedPath, _position, _duration);
    }

    private void Navigate(int delta)
    {
        var item = delta > 0 ? _queue.Next() : _queue.Previous();
        if (item is not null) LoadCurrent();
    }

    private void JumpTo(int index)
    {
        if (index == _queue.CurrentIndex) return;
        if (_queue.MoveTo(index) is not null) LoadCurrent();
    }

    // ---- transport ------------------------------------------------------------------------

    private void TogglePause()
    {
        if (_mpv is null || _queue.Current is null) return;
        if (!IsTimed)
        {
            if (_kind == MediaKind.AnimatedImage) _mpv.TryCommand("cycle", "pause");
            return;
        }
        // At the end of the file "play" means "play again".
        if (_paused && _mpv.GetFlag("eof-reached") == true) _mpv.TryCommand("seek", "0", "absolute");
        _mpv.Set("pause", !_paused);
    }

    private void SeekRelative(double seconds, bool exact = false)
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("seek", seconds.ToString(CultureInfo.InvariantCulture), exact || _editMode ? "relative+exact" : "relative");
    }

    private void SeekAbsolute(double seconds, bool exact)
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("seek", Math.Max(0, seconds).ToString("0.######", CultureInfo.InvariantCulture),
            exact ? "absolute+exact" : "absolute+keyframes");
    }

    private void Timeline_SeekRequested(double seconds, bool final)
    {
        // Scrub on keyframes for speed, land exactly on release (always exact while editing).
        SeekAbsolute(seconds, exact: final || _editMode);
        OnPosition(seconds);
    }

    private void FrameStep(bool forward)
    {
        if (_mpv is null || _kind != MediaKind.Video) return;
        _mpv.TryCommand(forward ? "frame-step" : "frame-back-step");
    }

    private void SetSpeed(double speed)
    {
        if (_mpv is null || !IsTimed) return;
        speed = SpeedSteps.Clamp(speed);
        _mpv.Set("speed", speed);
        ShowOsd(Loc.F("Osd.Speed", speed.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    private void ChangeVolume(double delta)
    {
        if (_mpv is null) return;
        double v = Math.Clamp(_volume + delta, 0, 150);
        _mpv.Set("volume", v);
        ShowOsd(Loc.F("Osd.Volume", Math.Round(v)));
    }

    private void ToggleMute()
    {
        if (_mpv is null) return;
        _mpv.Set("mute", !_muted);
    }

    private void UpdateVolumeUi()
    {
        _suppressVolumeEvent = true;
        VolumeSlider.Value = Math.Min(100, _volume);
        _suppressVolumeEvent = false;
        VolumeIcon.Glyph = _muted || _volume <= 0 ? "" : "";
        Settings.Volume = _volume;
        Settings.Muted = _muted;
    }

    private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppressVolumeEvent || _mpv is null) return;
        _mpv.Set("volume", e.NewValue);
        if (_muted && e.NewValue > 0) _mpv.Set("mute", false);
    }

    private void CycleAbLoop()
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("ab-loop");
        var a = _mpv.GetDouble("ab-loop-a");
        var b = _mpv.GetDouble("ab-loop-b");
        ShowOsd(a is null ? Loc.T("Osd.AbCleared")
            : b is null ? Loc.F("Osd.AbA", TimeFormat.Clock(a.Value, true))
            : Loc.F("Osd.AbB", TimeFormat.Clock(a.Value, true), TimeFormat.Clock(b.Value, true)));
    }

    private void TakeScreenshot(bool withSubtitles)
    {
        if (_mpv is null || _queue.Current is null) return;
        string dir = _mpv.GetString("screenshot-directory") ?? AppPaths.DefaultScreenshotDirectory;
        TryRun(() => Directory.CreateDirectory(dir), "screenshot dir");
        bool ok = _mpv.TryCommand("screenshot", withSubtitles ? "subtitles" : "video");
        ShowOsd(ok ? Loc.F("Osd.Screenshot", dir) : Loc.T("Osd.ScreenshotFailed"));
    }

    private void ShowOsd(string text) => _mpv?.TryCommand("show-text", text, "1500");

    // ---- tracks ---------------------------------------------------------------------------

    private void SpeedMenu_Opening(object? sender, object e)
    {
        SpeedMenu.Items.Clear();
        foreach (var preset in SpeedSteps.Presets)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = preset.ToString("0.##", CultureInfo.InvariantCulture) + "×",
                GroupName = "speed",
                IsChecked = Math.Abs(preset - _speed) < 0.001,
            };
            double value = preset;
            item.Click += (_, _) => SetSpeed(value);
            SpeedMenu.Items.Add(item);
        }
    }

    private void AudioMenu_Opening(object? sender, object e) => BuildTrackMenu(AudioMenu, "audio", "aid");

    private void SubtitleMenu_Opening(object? sender, object e)
    {
        BuildTrackMenu(SubtitleMenu, "sub", "sid");
        SubtitleMenu.Items.Add(new MenuFlyoutSeparator());
        var load = new MenuFlyoutItem { Text = Loc.T("Menu.LoadSubtitle") };
        load.Click += async (_, _) => await LoadSubtitleAsync();
        SubtitleMenu.Items.Add(load);
    }

    private void BuildTrackMenu(MenuFlyout menu, string type, string property)
    {
        menu.Items.Clear();
        var tracks = _mpv?.GetTracks().Where(t => t.Type == type).ToList() ?? new List<MpvTrack>();

        var off = new RadioMenuFlyoutItem { Text = Loc.T("Menu.TrackOff"), GroupName = type, IsChecked = !tracks.Any(t => t.Selected) };
        off.Click += (_, _) => _mpv?.Set(property, "no");
        menu.Items.Add(off);

        foreach (var track in tracks)
        {
            var item = new RadioMenuFlyoutItem { Text = track.DisplayName, GroupName = type, IsChecked = track.Selected };
            long id = track.Id;
            item.Click += (_, _) => _mpv?.Set(property, id.ToString(CultureInfo.InvariantCulture));
            menu.Items.Add(item);
        }
    }

    private void CycleTrack(string property, string osdKey)
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("cycle", property);
        string type = property == "aid" ? "audio" : "sub";
        var selected = _mpv.GetTracks().FirstOrDefault(t => t.Type == type && t.Selected);
        ShowOsd(Loc.F(osdKey, selected?.DisplayName ?? Loc.T("Menu.TrackOff")));
    }

    // ---- button handlers ------------------------------------------------------------------

    private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePause();
    private void Previous_Click(object sender, RoutedEventArgs e) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Navigate(1);
    private void FrameBack_Click(object sender, RoutedEventArgs e) => FrameStep(false);
    private void FrameForward_Click(object sender, RoutedEventArgs e) => FrameStep(true);
    private void Mute_Click(object sender, RoutedEventArgs e) => ToggleMute();
    private void Screenshot_Click(object sender, RoutedEventArgs e) => TakeScreenshot(true);
    private void AbLoop_Click(object sender, RoutedEventArgs e) => CycleAbLoop();

    private void LoopFile_Click(object sender, RoutedEventArgs e) =>
        _mpv?.Set("loop-file", LoopFileItem.IsChecked || _kind == MediaKind.AnimatedImage ? "inf" : "no");

    private void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        _queue.Shuffle = ShuffleItem.IsChecked;
        _ = _preloader.Preload(_queue.Peek(1), _queue.Peek(-1));
    }

    /// <summary>Shows the controls that make sense for the current kind of media.</summary>
    private void UpdateModeUi()
    {
        bool has = _queue.Current is not null;
        bool timed = has && IsTimed;
        bool image = has && _kind.IsImageLike();
        bool video = has && _kind == MediaKind.Video;

        if (!video && _editMode) SetEditMode(false);
        EmptyState.Visibility = has && _mpv is not null ? Visibility.Collapsed : Visibility.Visible;
        TimelineRow.Visibility = timed ? Visibility.Visible : Visibility.Collapsed;
        VideoTools.Visibility = timed ? Visibility.Visible : Visibility.Collapsed;
        VideoTransport.Visibility = VideoTransport2.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        ImageTools.Visibility = _kind == MediaKind.Image && has ? Visibility.Visible : Visibility.Collapsed;
        ZoomTools.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
        PlayButton.Visibility = timed || (has && _kind == MediaKind.AnimatedImage) ? Visibility.Visible : Visibility.Collapsed;
        EditToggle.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        AbLoopItem.IsEnabled = timed;
        EditBar.Visibility = _editMode && video ? Visibility.Visible : Visibility.Collapsed;
        SaveImageButton.IsEnabled = false;
        UpdateZoomText();
    }
}
