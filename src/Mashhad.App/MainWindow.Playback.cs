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
    private string? _engineError;

    private bool IsTimed => _kind.IsTimed();

    // ---- engine ------------------------------------------------------------------------------

    private void InitializePlayer()
    {
        if (Environment.GetEnvironmentVariable("MASHHAD_DISABLE_ENGINE") == "1")
        {
            Log.Write("Playback engine disabled by MASHHAD_DISABLE_ENGINE");
            return;
        }
        if (!MpvPlayer.IsAvailable(out var error))
        {
            Log.Write("libmpv not available: " + error);
            _engineError = Loc.F("Error.MpvMissing", System.IO.Path.Combine(AppContext.BaseDirectory, "mpv"));
            return;
        }
        try
        {
            Trace("player: creating video host");
            if (_smokeTest) VideoHost.Trace = Trace;
            _host = new VideoHost(_hwnd);
            // Mouse on the native video window: in Edit mode, and in Play mode when the docked
            // fallback is used (with the overlay window, the overlay receives the input instead).
            _host.MouseDown += Host_MouseDown;
            _host.MouseMove += Host_MouseMove;
            _host.MouseUp += (b, _, _) => { if (b == HostButton.Left) _dragging = false; };
            _host.DoubleClick += (_, _) => SetFullscreen(!_isFullscreen);
            _host.Wheel += Host_Wheel;
            _host.FilesDropped += files => OpenPaths(files);
            Trace("player: video host created");

            string shots = string.IsNullOrWhiteSpace(Settings.ScreenshotDirectory) ? AppPaths.DefaultScreenshotDirectory : Settings.ScreenshotDirectory;
            var options = MpvOptions.Build(Settings, _host.Handle.ToInt64(), shots);
            options.AddRange(MpvOptions.FromEnvironment(Environment.GetEnvironmentVariable("MASHHAD_MPV_OPTIONS")));
            Trace("player: creating mpv");
            _mpv = new MpvPlayer(options, MpvOptions.BuildOptional(embedded: true), _smokeTest ? "v" : "warn");
            Trace("player: mpv initialised");

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
                     })
                _mpv.Observe(name, format);
            _volume = Settings.Volume;
            _muted = Settings.Muted;
            UpdateVolumeUi();
            _host.DisableChildInput();
            Trace("player: child input disabled");
            Log.Write($"libmpv client API {MpvPlayer.ApiVersion} initialised, hwdec={Settings.HardwareDecoding}");
        }
        catch (Exception e)
        {
            Log.Write("Player initialisation failed: " + e);
            _mpv?.Dispose();
            _mpv = null;
            _engineError = Loc.F("Error.PlayerInit", e.Message);
        }
        _host?.SetVisible(false);
        UpdateAbout();
        Trace("player: done");
    }

    /// <summary>mpv event thread: coalesce position updates, forward the rest to the UI thread.</summary>
    private void Mpv_PropertyChanged(string name, object? value)
    {
        if (name == "time-pos")
        {
            if (value is double pos)
            {
                Volatile.Write(ref _pendingPosition, pos);
                if (Interlocked.Exchange(ref _positionQueued, 1) == 0)
                    RunOnUi(() => { Interlocked.Exchange(ref _positionQueued, 0); OnPosition(Volatile.Read(ref _pendingPosition)); });
            }
            return;
        }
        RunOnUi(() => OnProperty(name, value));
    }

    private void OnPosition(double position)
    {
        _position = position;
        if (!IsTimed) return;
        _chrome.Seek.Position = position;
        _chrome.TimeText.Text = $"{TimeFormat.Clock(position, forceHours: _duration >= 3600)} / {TimeFormat.Clock(_duration, forceHours: _duration >= 3600)}";
        if (_mode == AppMode.Edit)
        {
            _editor.Timeline.Position = position;
            _editor.TimeText.Text = TimeFormat.Clock(position, millis: true, forceHours: true);
        }
    }

    private void OnProperty(string name, object? value)
    {
        switch (name)
        {
            case "duration":
                _duration = value as double? ?? 0;
                _chrome.Seek.Duration = _duration;
                _segments.Duration = _duration;
                _editor.Timeline.Duration = _duration;
                _editor.DurationText.Text = "/ " + TimeFormat.Clock(_duration, millis: true, forceHours: true);
                OnPosition(_position);
                break;
            case "pause":
                _paused = value as bool? ?? true;
                _chrome.PlayGlyph.Glyph = _editor.PlayGlyph.Glyph = _paused ? Ui.G.Play : Ui.G.Pause;
                if (!_paused) KickChrome();
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
                _chrome.SpeedText.Text = _editor.SpeedText.Text = _speed.ToString("0.##", CultureInfo.InvariantCulture) + "×";
                break;
            case "ab-loop-a":
                _loopA = value as double?;
                _chrome.Seek.SetLoop(_loopA, _loopB);
                break;
            case "ab-loop-b":
                _loopB = value as double?;
                _chrome.Seek.SetLoop(_loopA, _loopB);
                break;
        }
    }

    // ---- opening -----------------------------------------------------------------------------

    /// <summary>Opens files/folders from the command line, a drop, a picker or Explorer.</summary>
    public void OpenPaths(IReadOnlyList<string> paths)
    {
        if (_closed) return;
        OpenRequest request;
        try { request = OpenRequest.Resolve(paths, Settings.Sort, Settings.SortDescending); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowToast(Loc.F("Error.Open", e.Message));
            return;
        }

        // A single folder: browse it.
        if (paths.Count == 1 && Directory.Exists(paths[0]))
        {
            _ = LoadFolderAsync(paths[0], null);
            if (_mode != AppMode.Folder) SetMode(AppMode.Folder);
            return;
        }
        if (request.Items.Count == 0)
        {
            if (request.Subtitles.Count > 0 && _mpv is not null && _fileReady)
            {
                foreach (var sub in request.Subtitles) _mpv.TryCommand("sub-add", sub, "select");
                ShowToast(Loc.T("Osd.SubtitleLoaded"));
            }
            else ShowToast(Loc.T("Error.NothingToOpen"));
            return;
        }
        StopSlideshow();
        _pendingSubtitles = request.Subtitles;
        _queue.Load(request.Items, request.Index, request.Source);
        var first = _queue.Current!;
        var folder = System.IO.Path.GetDirectoryName(first.Path);
        if (folder is not null && Directory.Exists(folder) && !SameFolder(folder, _gallery.Folder)) _ = LoadFolderAsync(folder, first.Path);
        LoadCurrent();
        if (_mode == AppMode.Folder) SetMode(AppMode.Play);
        else if (_mode == AppMode.Edit && _kind != MediaKind.Video) SetMode(AppMode.Play);
        else if (_mode == AppMode.Edit) EnterEditData();
    }

    private static bool SameFolder(string? a, string? b) =>
        a is not null && b is not null && FolderScanner.PathEquals(System.IO.Path.TrimEndingDirectorySeparator(a), System.IO.Path.TrimEndingDirectorySeparator(b));

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
        _chrome.Seek.Duration = 0;
        _chrome.Seek.Position = 0;
        _chrome.Seek.SetLoop(null, null);
        _chrome.TimeText.Text = "00:00 / 00:00";
        _chrome.SetKind(video: IsTimed, animated: _kind == MediaKind.AnimatedImage);
        RestoreSegments(item.Path);
        UpdateTitle();
        UpdateModeAvailability();
        _gallery.FocusPath(item.Path);

        if (_mpv is not null)
        {
            _mpv.Set("video-zoom", 0.0);
            _mpv.Set("video-pan-x", 0.0);
            _mpv.Set("video-pan-y", 0.0);
            _mpv.Set("video-rotate", "0");
            _mpv.Set("ab-loop-a", "no");
            _mpv.Set("ab-loop-b", "no");
            _mpv.Set("speed", 1.0);
            _mpv.Set("loop-file", _kind == MediaKind.AnimatedImage || _loopFile ? "inf" : "no");
            _mpv.Set("pause", _mode == AppMode.Edit);
            double? resume = IsTimed && Settings.ResumePlayback && !_slideshowOn ? _resume.GetPosition(item.Path) : null;
            _mpv.Set("start", resume is double r ? r.ToString("0.###", CultureInfo.InvariantCulture) : "none");
            if (!_mpv.TryCommand("loadfile", item.Path)) ShowToast(Loc.F("Error.Open", item.FileName));
            _host?.SetVisible(_mode != AppMode.Folder);
        }
        _ = _preloader.Preload(_queue.Peek(1), _queue.Peek(-1));
        if (_chrome.InfoPanel.Visibility == Visibility.Visible) RefreshInfoPanel();
    }

    private void OnFileLoaded()
    {
        if (_mpv is null) return;
        _fileReady = true;
        _mpv.Set("start", "none");
        _host?.DisableChildInput();
        foreach (var sub in _pendingSubtitles) _mpv.TryCommand("sub-add", sub, "select");
        _pendingSubtitles = new List<string>();
        var hw = _mpv.GetString("hwdec-current");
        _chrome.ChipText.Text = IsTimed
            ? (hw is { Length: > 0 } && hw != "no" ? Loc.F("Chip.Hardware", hw.ToUpperInvariant()) : Loc.T("Chip.Software"))
            : $"{_mpv.GetLong("video-params/w")} × {_mpv.GetLong("video-params/h")}";
        if (_kind.IsImageLike()) OnViewportResized();
        if (_mode == AppMode.Edit) EnterEditData();
        if (_chrome.InfoPanel.Visibility == Visibility.Visible) RefreshInfoPanel();
        if (_slideshowOn) ScheduleSlideshow();
    }

    private void OnEndFile(MpvEndFileReason reason)
    {
        if (reason != MpvEndFileReason.Error) return;
        Log.Write("mpv could not play: " + _loadedPath);
        ShowToast(Loc.F("Error.Play", _queue.Current?.FileName ?? ""));
        if (_slideshowOn) SlideshowAdvance();
    }

    private void OnReachedEnd()
    {
        if (!IsTimed || !_fileReady) return;
        _resume.Forget(_loadedPath ?? "");
        if (_slideshowOn) { SlideshowAdvance(); return; }
        if (_queue.Source == QueueSource.Playlist && _queue.PeekIndex(1) > _queue.CurrentIndex) Navigate(1);
    }

    private void SaveResumePosition()
    {
        if (_loadedPath is null || !_fileReady || _kind != MediaKind.Video) return;
        if (Settings.ResumePlayback) _resume.Update(_loadedPath, _position, _duration);
    }

    private void Navigate(int delta)
    {
        var item = delta > 0 ? _queue.Next() : _queue.Previous();
        if (item is null) return;
        LoadCurrent();
        if (_mode == AppMode.Edit && _kind != MediaKind.Video) SetMode(AppMode.Play);
    }

    private void JumpTo(int index)
    {
        if (index == _queue.CurrentIndex) return;
        if (_queue.MoveTo(index) is not null) LoadCurrent();
    }

    // ---- transport ---------------------------------------------------------------------------

    private void TogglePause()
    {
        if (_mpv is null || _queue.Current is null) return;
        if (!IsTimed)
        {
            if (_kind == MediaKind.AnimatedImage) _mpv.TryCommand("cycle", "pause");
            return;
        }
        if (_paused && _mpv.GetFlag("eof-reached") == true) _mpv.TryCommand("seek", "0", "absolute");
        _mpv.Set("pause", !_paused);
    }

    private void SeekRelative(double seconds, bool exact = false)
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("seek", seconds.ToString(CultureInfo.InvariantCulture), exact || _mode == AppMode.Edit ? "relative+exact" : "relative");
        KickChrome();
    }

    private void SeekAbsolute(double seconds, bool exact)
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("seek", Math.Max(0, seconds).ToString("0.######", CultureInfo.InvariantCulture), exact ? "absolute+exact" : "absolute+keyframes");
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
        ShowToast(Loc.F("Osd.Speed", speed.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    private void ChangeVolume(double delta)
    {
        if (_mpv is null) return;
        double v = Math.Clamp(_volume + delta, 0, 150);
        _mpv.Set("volume", v);
        ShowToast(Loc.F("Osd.Volume", Math.Round(v)));
    }

    private void ToggleMute() => _mpv?.Set("mute", !_muted);

    private void UpdateVolumeUi()
    {
        _suppressVolumeEvent = true;
        _chrome.Volume.Value = Math.Min(100, _volume);
        _suppressVolumeEvent = false;
        _chrome.VolumeGlyph.Glyph = _muted || _volume <= 0 ? Ui.G.Mute : Ui.G.Volume;
        Settings.Volume = _volume;
        Settings.Muted = _muted;
    }

    private void CycleAbLoop()
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("ab-loop");
        var a = _mpv.GetDouble("ab-loop-a");
        var b = _mpv.GetDouble("ab-loop-b");
        ShowToast(a is null ? Loc.T("Osd.AbCleared")
            : b is null ? Loc.F("Osd.AbA", TimeFormat.Clock(a.Value, true))
            : Loc.F("Osd.AbB", TimeFormat.Clock(a.Value, true), TimeFormat.Clock(b.Value, true)));
    }

    private bool _loopFile;

    private void ToggleLoopFile()
    {
        _loopFile = !_loopFile;
        _mpv?.Set("loop-file", _loopFile || _kind == MediaKind.AnimatedImage ? "inf" : "no");
        ShowToast(Loc.T(_loopFile ? "Osd.LoopOn" : "Osd.LoopOff"));
    }

    private void TakeScreenshot(bool withSubtitles)
    {
        if (_mpv is null || _queue.Current is null) return;
        string dir = _mpv.GetString("screenshot-directory") ?? AppPaths.DefaultScreenshotDirectory;
        TryRun(() => Directory.CreateDirectory(dir), "screenshot dir");
        bool ok = _mpv.TryCommand("screenshot", withSubtitles ? "subtitles" : "video");
        ShowToast(ok ? Loc.F("Osd.Screenshot", dir) : Loc.T("Osd.ScreenshotFailed"));
    }

    // ---- menus -------------------------------------------------------------------------------

    private void BuildSpeedMenu()
    {
        _chrome.SpeedMenu.Items.Clear();
        foreach (var preset in SpeedSteps.Presets)
        {
            var item = new RadioMenuFlyoutItem { Text = preset.ToString("0.##", CultureInfo.InvariantCulture) + "×", GroupName = "speed", IsChecked = Math.Abs(preset - _speed) < 0.001 };
            double v = preset;
            item.Click += (_, _) => SetSpeed(v);
            _chrome.SpeedMenu.Items.Add(item);
        }
    }

    private void BuildTrackMenu(MenuFlyout menu, string type, string property, bool subtitles)
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
        if (subtitles)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var load = new MenuFlyoutItem { Text = Loc.T("Menu.LoadSubtitle") };
            load.Click += async (_, _) => await LoadSubtitleAsync();
            menu.Items.Add(load);
        }
    }

    private void BuildMoreMenu()
    {
        var m = _chrome.MoreMenu;
        m.Items.Clear();
        void Add(string key, Action a, bool enabled = true) { var i = new MenuFlyoutItem { Text = Loc.T(key), IsEnabled = enabled }; i.Click += (_, _) => a(); m.Items.Add(i); }
        Add("Menu.Screenshot", () => TakeScreenshot(true));
        Add("Menu.AbLoop", CycleAbLoop, IsTimed);
        var loop = new ToggleMenuFlyoutItem { Text = Loc.T("Menu.LoopFile"), IsChecked = _loopFile };
        loop.Click += (_, _) => ToggleLoopFile();
        m.Items.Add(loop);
        Add("Bar.Slideshow", ToggleSlideshow);
        m.Items.Add(new MenuFlyoutSeparator());
        Add("Mode.Edit", () => RequestMode(AppMode.Edit), _kind == MediaKind.Video);
        Add("Menu.ShowInExplorer", () => RevealInExplorer(_queue.Current?.Path));
        Add("Menu.SavePlaylist", () => _ = SavePlaylistAsync());
        m.Items.Add(new MenuFlyoutSeparator());
        var theme = new MenuFlyoutSubItem { Text = Loc.T("Settings.Theme") };
        foreach (var (id, key) in new[] { (UiThemes.Default, "Theme.Default"), (UiThemes.LiquidGlass, "Theme.LiquidGlass") })
        {
            var item = new RadioMenuFlyoutItem { Text = Loc.T(key), GroupName = "theme", IsChecked = Settings.UiTheme == id };
            item.Click += (_, _) => SetTheme(id);
            theme.Items.Add(item);
        }
        m.Items.Add(theme);
    }

    private void CycleTrack(string property, string osdKey)
    {
        if (_mpv is null || !IsTimed) return;
        _mpv.TryCommand("cycle", property);
        string type = property == "aid" ? "audio" : "sub";
        var selected = _mpv.GetTracks().FirstOrDefault(t => t.Type == type && t.Selected);
        ShowToast(Loc.F(osdKey, selected?.DisplayName ?? Loc.T("Menu.TrackOff")));
    }

    private void Host_MouseDown(HostButton button, int x, int y)
    {
        if (_mode == AppMode.Edit)
        {
            if (button == HostButton.Left) TogglePause();
            return;
        }
        KickChrome();
        switch (button)
        {
            case HostButton.Left:
                _dragging = true;
                (_dragX, _dragY) = (x, y);
                if (_cropMode) CropStart(x, y);
                break;
            case HostButton.Right: if (IsTimed) TogglePause(); break;
            case HostButton.Middle: if (_kind.IsImageLike()) ZoomFit(); break;
            case HostButton.Back: Navigate(-1); break;
            case HostButton.Forward: Navigate(1); break;
        }
    }

    private void Host_MouseMove(int x, int y)
    {
        KickChrome();
        if (!_dragging || _mode != AppMode.Play) return;
        if (_cropMode) CropDrag(x, y);
        else if (_kind.IsImageLike())
        {
            PanBy(x - _dragX, y - _dragY);
            (_dragX, _dragY) = (x, y);
        }
    }

    private void Host_Wheel(double notches, int x, int y, bool ctrl)
    {
        if (_mode == AppMode.Edit) { SeekRelative(notches * (ctrl ? 5 : 1), exact: true); return; }
        if (_kind.IsImageLike()) ZoomAt(notches * 0.2, x, y);
        else if (ctrl) SeekRelative(notches * 5);
        else ChangeVolume(notches * 5);
    }

    private void WireChrome()
    {
        _chrome.PreviewKeyDown += (_, e) => OnKey(e);
        _chrome.Command += Execute;
        _chrome.Seek.SeekRequested += (t, final) => { SeekAbsolute(t, exact: final); OnPosition(t); KickChrome(); };
        _chrome.Volume.ValueChanged += (_, e) =>
        {
            if (_suppressVolumeEvent || _mpv is null) return;
            _mpv.Set("volume", e.NewValue);
            if (_muted && e.NewValue > 0) _mpv.Set("mute", false);
        };
        _chrome.SpeedMenu.Opening += (_, _) => BuildSpeedMenu();
        _chrome.SubsMenu.Opening += (_, _) => BuildTrackMenu(_chrome.SubsMenu, "sub", "sid", subtitles: true);
        _chrome.AudioMenu.Opening += (_, _) => BuildTrackMenu(_chrome.AudioMenu, "audio", "aid", subtitles: false);
        _chrome.MoreMenu.Opening += (_, _) => BuildMoreMenu();
        _chrome.Dock.PointerEntered += (_, _) => _pointerOverDock = true;
        _chrome.Dock.PointerExited += (_, _) => _pointerOverDock = false;
        _chrome.PointerMoved += Chrome_PointerMoved;
        _chrome.PointerPressed += Chrome_PointerPressed;
        _chrome.PointerReleased += Chrome_PointerReleased;
        _chrome.PointerWheelChanged += Chrome_PointerWheel;
        _chrome.DoubleTapped += (_, e) => { if (!_chrome.PointerOverChrome(e)) SetFullscreen(!_isFullscreen); };
        _chrome.RightTapped += (_, e) => { if (IsTimed) TogglePause(); };
    }
}
