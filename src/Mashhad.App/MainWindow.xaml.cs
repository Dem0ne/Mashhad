using Mashhad.App.Controls;
using Mashhad.App.Native;
using Mashhad.App.Views;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Mashhad.Core.Settings;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;

namespace Mashhad.App;

/// <summary>
/// The application window: one dark "wall" with three modes — Folder (gallery), Play (Cinema) and
/// Edit (Studio). Split into partial files: Playback, Gallery, Input, Images, Editing, Panels.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new(AppPaths.SettingsFile);
    private readonly ResumeStore _resume = new(AppPaths.ResumeFile);
    private readonly MediaQueue _queue = new();
    private readonly Preloader _preloader = new();
    private readonly Core.Input.ShortcutMap _shortcuts = Core.Input.ShortcutMap.CreateDefault();
    private readonly SlideshowPolicy _slideshow = new();
    private readonly IntPtr _hwnd;
    private readonly bool _smokeTest;
    private readonly IReadOnlyList<string> _startupFiles;
    private readonly DispatcherQueue _ui;
    private readonly Animator _anim;
    private readonly ThumbnailCache _thumbs;

    // views
    private readonly GalleryView _gallery;
    private readonly EditView _editor;
    private readonly PlayChrome _chrome;
    private readonly SettingsSheet _settingsSheet;
    private readonly ModeSwitcher _modes = new(overVideo: false);
    private readonly Border _titlePill, _toast, _dragRegion, _playBackdrop;
    private readonly TextBlock _titleText = Ui.Text("", 13.5, true), _titleContext = Ui.Text("", 13, false, 0.6), _toastText = Ui.Text("", 13.5);
    private readonly Canvas _transitionLayer = new() { IsHitTestVisible = false };
    private readonly Grid _wall = new();
    private PlayOverlay? _overlay;
    private bool _overlayFailed;

    private VideoHost? _host;
    private Core.Mpv.MpvPlayer? _mpv;
    private AppMode _mode = AppMode.Folder;
    private bool _closed, _isFullscreen;
    private Rect? _hostOverride;
    private DispatcherQueueTimer? _chromeTimer, _slideshowTimer, _toastTimer;

    private AppSettings Settings => _settingsStore.Current;

    public MainWindow(IReadOnlyList<string> startupFiles, bool smokeTest)
    {
        _startupFiles = startupFiles;
        _smokeTest = smokeTest;
        Trace("ctor: start");
        Loc.SetLanguage(Settings.Language);
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        _anim = new Animator(_ui);
        _thumbs = new ThumbnailCache(_ui);
        _thumbs.Tools = () => Mashhad.Editing.FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Trace("ctor: XAML loaded");

        Title = "Mashhad";
        TryRun(() => AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Mashhad.ico")), "icon");
        TryRun(() =>
        {
            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            var tb = AppWindow.TitleBar;
            tb.ButtonBackgroundColor = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonForegroundColor = Colors.White;
            tb.ButtonHoverForegroundColor = Colors.White;
            tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
            tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF);
        }, "title bar");
        TryRun(() => AppWindow.Resize(new SizeInt32(Settings.WindowWidth, Settings.WindowHeight)), "resize");
        if (smokeTest) TryRun(() => (AppWindow.Presenter as OverlappedPresenter)?.Maximize(), "maximize"); // fill the CI screen for screenshots
        Root.RequestedTheme = ElementTheme.Dark;
        Root.Background = new SolidColorBrush(Ui.Wall);

        // ---- wall ----
        _wall.IsHitTestVisible = false;
        _wall.Children.Add(Glow(Ui.Crimson, 0.10, 0.0, 0.55, 0.45, 0.22));
        _wall.Children.Add(Glow(Ui.Wine, 0.92, 0.0, 0.6, 0.55, 0.30));
        Root.Children.Add(_wall);

        // ---- modes ----
        _gallery = new GalleryView(_thumbs, () => Loc.Current.Culture) { RecentFolders = () => Settings.RecentFolders };
        _editor = new EditView { Visibility = Visibility.Collapsed };
        _playBackdrop = new Border { Background = new SolidColorBrush(Colors.Black), Visibility = Visibility.Collapsed };
        _chrome = new PlayChrome();
        Root.Children.Add(_gallery);
        Root.Children.Add(_editor);
        Root.Children.Add(_playBackdrop);
        Root.Children.Add(_transitionLayer);

        // ---- constant chrome: title pill + mode switcher (+ toast) ----
        _dragRegion = new Border { Height = 52, VerticalAlignment = VerticalAlignment.Top, Background = new SolidColorBrush(Colors.Transparent) };
        Root.Children.Add(_dragRegion);
        _titlePill = Ui.Pill(Ui.Row(10, Ui.Logo(18), _titleText, _titleContext), radius: 17, padding: new Thickness(10, 0, 14, 0));
        _titlePill.Height = 34;
        _titlePill.HorizontalAlignment = HorizontalAlignment.Left;
        _titlePill.VerticalAlignment = VerticalAlignment.Top;
        _titlePill.MaxWidth = 520;
        Root.Children.Add(_titlePill);
        _modes.HorizontalAlignment = HorizontalAlignment.Center;
        _modes.VerticalAlignment = VerticalAlignment.Top;
        _modes.Margin = new Thickness(0, 7, 0, 0);
        Root.Children.Add(_modes);
        _toast = Ui.Pill(_toastText, radius: 17, padding: new Thickness(16, 7, 16, 7));
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.VerticalAlignment = VerticalAlignment.Top;
        _toast.Margin = new Thickness(0, 54, 0, 0);
        _toast.Visibility = Visibility.Collapsed;
        Root.Children.Add(_toast);
        _settingsSheet = new SettingsSheet();
        Root.Children.Add(_settingsSheet);

        _queue.Wrap = Settings.WrapGallery;
        _gallery.SetPinned(Settings.FolderPanePinned);
        WireGallery();
        WireChrome();
        WireEditor();
        WireSettings();
        _modes.ModeRequested += m => RequestMode(m);
        _chrome.Modes.ModeRequested += m => RequestMode(m);

        Root.PreviewKeyDown += (_, e) => OnKey(e);
        Root.AllowDrop = true;
        Root.DragOver += Root_DragOver;
        Root.Drop += Root_Drop;
        Root.Loaded += Root_Loaded;
        Root.SizeChanged += (_, _) => { SyncVideoHost(); SyncOverlay(); UpdateTitleBarRegions(); };
        _editor.LayoutUpdated += (_, _) => { if (_mode == AppMode.Edit) SyncVideoHost(); };
        AppWindow.Changed += AppWindow_Changed;
        Closed += MainWindow_Closed;

        _chromeTimer = _ui.CreateTimer();
        _chromeTimer.Interval = TimeSpan.FromSeconds(2.5);
        _chromeTimer.IsRepeating = false;
        _chromeTimer.Tick += (_, _) => AutoHideChrome();
        _slideshowTimer = _ui.CreateTimer();
        _slideshowTimer.IsRepeating = false;
        _slideshowTimer.Tick += (_, _) => SlideshowAdvance();
        _toastTimer = _ui.CreateTimer();
        _toastTimer.Interval = TimeSpan.FromSeconds(2.2);
        _toastTimer.IsRepeating = false;
        _toastTimer.Tick += (_, _) => { _toast.Visibility = Visibility.Collapsed; _chrome.Toast.Visibility = Visibility.Collapsed; };

        ApplyLanguage();
        SetModeUi(AppMode.Folder);
        Trace("ctor: done");
    }

    private static Rectangle Glow(Windows.UI.Color color, double cx, double cy, double rx, double ry, double opacity)
    {
        var b = new RadialGradientBrush { Center = new Point(cx, cy), GradientOrigin = new Point(cx, cy), RadiusX = rx, RadiusY = ry };
        b.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb((byte)(255 * opacity), color.R, color.G, color.B), Offset = 0 });
        b.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, color.R, color.G, color.B), Offset = 1 });
        return new Rectangle { Fill = b };
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        Trace("loaded: start");
        TryRun(() => SetTitleBar(_dragRegion), "set title bar");
        UpdateTitleBarRegions();
        InitializePlayer();
        Trace("loaded: player initialised");
        if (_startupFiles.Count > 0) OpenPaths(_startupFiles);
        else _gallery.ShowEmpty(_engineError);
        Trace("loaded: files opened");
        _ = Fonts.AppFonts.VerifyAsync(Root);
        if (_smokeTest) StartSmokeTest();
    }

    /// <summary>Files handed over by a second process (Explorer double-click while we are running).</summary>
    public void OpenFromActivation(IReadOnlyList<string> files)
    {
        if (_closed) return;
        if (Win32.IsIconic(_hwnd)) Win32.ShowWindow(_hwnd, 9 /* SW_RESTORE */);
        Win32.SetForegroundWindow(_hwnd);
        Activate();
        if (files.Count > 0) OpenPaths(files);
    }

    // ---- language ----------------------------------------------------------------------------

    private void ApplyLanguage()
    {
        Loc.SetLanguage(Settings.Language);
        bool rtl = Loc.Current.IsRightToLeft;
        var dir = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Root.FlowDirection = dir;
        _chrome.FlowDirection = dir;
        // System caption buttons stay on the right, so in Arabic the title pill moves left of them.
        _titlePill.Margin = new Thickness(rtl ? 150 : 12, 9, 0, 0);
        _chrome.TitlePill.Margin = new Thickness(rtl ? 150 : 12, 9, 0, 0);
        _chrome.Captions.HorizontalAlignment = rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        UpdateTitle();
        _gallery.RefreshTexts();
        _editor.RefreshTexts();
        if (_keyframes is not null) _editor.KeyframeStatus.Text = Loc.F("Edit.KeyframesLoaded", _keyframes.Count);
        UpdateSegmentsUi();
        UpdateAbout();
        if (_chrome.InfoPanel.Visibility == Visibility.Visible) RefreshInfoPanel();
        _ui.TryEnqueue(UpdateTitleBarRegions);
    }

    private void UpdateTitle()
    {
        var item = _queue.Current;
        string name = _mode == AppMode.Folder ? (_gallery.Folder is { } f ? System.IO.Path.GetFileName(f.TrimEnd('\\', '/')) : Loc.T("App.Name")) : item?.FileName ?? Loc.T("App.Name");
        string ctx = _mode switch
        {
            AppMode.Folder => _gallery.Folder is null ? "" : Loc.F("Gallery.Count", _gallery.Model.TotalCount),
            AppMode.Edit => Loc.T("Edit.Lossless"),
            _ => _queue.Count > 1 ? Loc.F("Title.Position", _queue.CurrentIndex + 1, _queue.Count) : "",
        };
        _titleText.Text = name;
        _titleContext.Text = ctx;
        _chrome.TitleText.Text = name;
        _chrome.ContextText.Text = ctx;
        Title = name == Loc.T("App.Name") ? name : $"{name} — {Loc.T("App.Name")}";
    }

    // ---- title bar ---------------------------------------------------------------------------

    /// <summary>The top band drags the window; the pills inside it must stay clickable.</summary>
    private void UpdateTitleBarRegions()
    {
        if (Root.XamlRoot is null) return;
        TryRun(() =>
        {
            double s = Root.XamlRoot.RasterizationScale;
            var rects = new List<RectInt32>();
            foreach (var el in new FrameworkElement[] { _titlePill, _modes })
            {
                if (el.Visibility != Visibility.Visible || el.ActualWidth <= 0) continue;
                var r = el.TransformToVisual(null).TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                rects.Add(new RectInt32((int)(r.X * s), (int)(r.Y * s), (int)(r.Width * s), (int)(r.Height * s)));
            }
            InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(NonClientRegionKind.Passthrough, rects.ToArray());
        }, "title bar regions");
    }

    // ---- modes -------------------------------------------------------------------------------

    /// <summary>A mode chosen by the user (switcher, keyboard). Animated.</summary>
    private void RequestMode(AppMode target)
    {
        if (target == _mode || _anim.IsRunning) return;
        if (!AppModes.CanEnter(target, _queue.Current)) return;
        if (target == AppMode.Folder && _gallery.Folder is null && _queue.Current is { } cur)
            _ = LoadFolderAsync(System.IO.Path.GetDirectoryName(cur.Path)!, cur.Path);
        var from = _mode;
        if (from == AppMode.Folder && target == AppMode.Play) { EnterPlayFromGallery(); return; }
        if (from == AppMode.Play && target == AppMode.Folder) { ReturnToGallery(); return; }
        if (from == AppMode.Play && target == AppMode.Edit) { MorphPlayEdit(toEdit: true); return; }
        if (from == AppMode.Edit && target == AppMode.Play) { MorphPlayEdit(toEdit: false); return; }
        SetMode(target); // Folder ⇄ Edit: cross-fade
    }

    /// <summary>Switches mode immediately (no geometry animation).</summary>
    private void SetMode(AppMode mode)
    {
        var from = _mode;
        _mode = mode;
        if (from == AppMode.Edit && mode != AppMode.Edit) RememberSegments();
        SetModeUi(mode);
        if (mode == AppMode.Edit) EnterEditData();
        if (mode == AppMode.Folder && _queue.Current is { } cur) _gallery.FocusPath(cur.Path);
        UpdateTitle();
        Trace($"mode: {mode}");
    }

    private void SetModeUi(AppMode mode)
    {
        _gallery.Visibility = mode == AppMode.Folder ? Visibility.Visible : Visibility.Collapsed;
        _gallery.Opacity = 1;
        _editor.Visibility = mode == AppMode.Edit ? Visibility.Visible : Visibility.Collapsed;
        _editor.SetEntrance(0);
        _playBackdrop.Visibility = mode == AppMode.Play ? Visibility.Visible : Visibility.Collapsed;
        _playBackdrop.Opacity = 1;
        _settingsSheet.Visibility = mode == AppMode.Folder && _settingsSheet.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        _modes.SetMode(mode);
        _chrome.Modes.SetMode(mode);
        UpdateModeAvailability();
        bool usesOverlay = mode == AppMode.Play;
        ShowPlayChrome(usesOverlay);
        _titlePill.Visibility = _modes.Visibility = mode == AppMode.Play && _overlay is not null ? Visibility.Collapsed : Visibility.Visible;
        _host?.SetVisible(mode != AppMode.Folder && _mpv is not null && _queue.Current is not null);
        SyncVideoHost();
        _ui.TryEnqueue(UpdateTitleBarRegions);
        if (mode == AppMode.Folder) _ui.TryEnqueue(() => { if (_mode == AppMode.Folder && _gallery.Visibility == Visibility.Visible) _gallery.FocusGrid(); });
        if (mode != AppMode.Play) _chromeTimer?.Stop();
        else KickChrome();
    }

    private void UpdateModeAvailability()
    {
        bool play = AppModes.CanEnter(AppMode.Play, _queue.Current);
        bool edit = AppModes.CanEnter(AppMode.Edit, _queue.Current);
        _modes.SetAvailable(play, edit);
        _chrome.Modes.SetAvailable(play, edit);
    }

    /// <summary>Folder → Play: the tile expands into the window while the item loads.</summary>
    private void EnterPlayFromGallery()
    {
        var item = _queue.Current;
        if (item is null) return;
        var tile = _gallery.TileBounds(_gallery.Model.IndexOf(item.Path), Root);
        var target = new Rect(0, 0, Root.ActualWidth, Root.ActualHeight);
        _playBackdrop.Visibility = Visibility.Visible;
        _playBackdrop.Opacity = 0;
        Image? flyer = null;
        if (tile is { Image: not null } t)
        {
            flyer = new Image { Source = t.Image, Stretch = Stretch.Uniform };
            _transitionLayer.Children.Add(flyer);
        }
        Rect from = tile?.Bounds ?? new Rect(target.Width / 2 - 80, target.Height / 2 - 50, 160, 100);
        _anim.Run(300, k =>
        {
            _gallery.Opacity = 1 - k;
            _playBackdrop.Opacity = k;
            if (flyer is not null) PlaceRect(flyer, Lerp(from, target, k));
        }, () =>
        {
            SetMode(AppMode.Play);
            _ui.TryEnqueue(DispatcherQueuePriority.Low, () => _transitionLayer.Children.Clear());
        });
    }

    /// <summary>Play → Folder: the picture shrinks back into its tile.</summary>
    private void ReturnToGallery()
    {
        var item = _queue.Current;
        StopSlideshow();
        if (_cropMode) EndCrop();
        SaveResumePosition();
        if (_mpv is not null && IsTimed) _mpv.Set("pause", true);
        ShowPlayChrome(false);
        _host?.SetVisible(false);
        _mode = AppMode.Folder;
        _gallery.Visibility = Visibility.Visible;
        _gallery.Opacity = 0;
        if (item is not null) _gallery.FocusPath(item.Path);
        _gallery.UpdateLayout();
        var src = item is null ? null : _gallery.ThumbnailOf(item.Path);
        Image? flyer = src is null ? null : new Image { Source = src, Stretch = Stretch.Uniform };
        if (flyer is not null) _transitionLayer.Children.Add(flyer);
        var full = new Rect(0, 0, Root.ActualWidth, Root.ActualHeight);
        _anim.Run(280, k =>
        {
            _gallery.Opacity = k;
            _playBackdrop.Opacity = 1 - k;
            if (flyer is not null && item is not null)
            {
                var tile = _gallery.TileBounds(_gallery.Model.IndexOf(item.Path), Root);
                if (tile is { } t) PlaceRect(flyer, Lerp(full, t.Bounds, k));
                else flyer.Opacity = 1 - k;
            }
        }, () =>
        {
            _transitionLayer.Children.Clear();
            SetMode(AppMode.Folder);
        });
    }

    /// <summary>Play ⇄ Edit: the video window glides between full screen and the editor preview while the panels slide.</summary>
    private void MorphPlayEdit(bool toEdit)
    {
        ShowPlayChrome(false);
        _editor.Visibility = Visibility.Visible;
        _editor.SetEntrance(toEdit ? 1 : 0);
        _editor.ArrangeFor(Root.ActualWidth, Root.ActualHeight);
        _editor.UpdateLayout();
        var full = PlayHostRect();
        var preview = EditHostRect() ?? full;
        if (toEdit)
        {
            _mode = AppMode.Edit;
            _modes.SetMode(AppMode.Edit);
            _chrome.Modes.SetMode(AppMode.Edit);
            _titlePill.Visibility = _modes.Visibility = Visibility.Visible;
            _playBackdrop.Visibility = Visibility.Visible;
        }
        _anim.Run(350, k =>
        {
            double t = toEdit ? k : 1 - k;
            _editor.SetEntrance(1 - t);
            _playBackdrop.Opacity = 1 - t;
            _hostOverride = Lerp(full, preview, t);
            SyncVideoHost();
        }, () =>
        {
            _hostOverride = null;
            SetMode(toEdit ? AppMode.Edit : AppMode.Play);
        });
    }

    private static Rect Lerp(Rect a, Rect b, double t) =>
        new(Animator.Lerp(a.X, b.X, t), Animator.Lerp(a.Y, b.Y, t), Math.Max(1, Animator.Lerp(a.Width, b.Width, t)), Math.Max(1, Animator.Lerp(a.Height, b.Height, t)));

    private static void PlaceRect(FrameworkElement e, Rect r)
    {
        Canvas.SetLeft(e, r.X);
        Canvas.SetTop(e, r.Y);
        e.Width = r.Width;
        e.Height = r.Height;
    }

    // ---- native video window -----------------------------------------------------------------

    private Rect PlayHostRect()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        // Without the overlay window the chrome cannot float over the video: keep it clear of the dock.
        return _overlay is null ? new Rect(0, 52, w, Math.Max(1, h - 52 - 150)) : new Rect(0, 0, w, h);
    }

    private Rect? EditHostRect()
    {
        if (_editor.Preview.ActualWidth < 1 || _editor.Preview.XamlRoot is null) return null;
        // Relative to the window (not the possibly mirrored Root): the native window needs physical coordinates.
        return _editor.Preview.TransformToVisual(null).TransformBounds(new Rect(0, 0, _editor.Preview.ActualWidth, _editor.Preview.ActualHeight));
    }

    /// <summary>Keeps the native video window over the area the current mode gives it (physical pixels).</summary>
    private void SyncVideoHost()
    {
        if (_host is null || _closed || Root.XamlRoot is null) return;
        Rect? r = _hostOverride ?? (_mode switch { AppMode.Play => PlayHostRect(), AppMode.Edit => EditHostRect(), _ => null });
        if (r is not Rect rect) return;
        double s = Root.XamlRoot.RasterizationScale;
        double x = rect.X;
        int oldW = _host.Width, oldH = _host.Height;
        _host.SetBounds((int)Math.Round(x * s), (int)Math.Round(rect.Y * s), (int)Math.Round(rect.Width * s), (int)Math.Round(rect.Height * s));
        if ((oldW != _host.Width || oldH != _host.Height) && _kind.IsImageLike()) OnViewportResized();
    }

    // ---- overlay (Play chrome) ---------------------------------------------------------------

    private void ShowPlayChrome(bool show)
    {
        if (show && _overlay is null && !_overlayFailed && Environment.GetEnvironmentVariable("MASHHAD_NO_OVERLAY") != "1")
        {
            Root.Children.Remove(_chrome);
            _overlay = PlayOverlay.TryCreate(_hwnd, _chrome, out var error);
            if (_overlay is null)
            {
                _overlayFailed = true;
                Log.Write("Overlay window unavailable, using the docked fallback: " + error);
            }
            else Trace("overlay: created");
        }
        if (_overlay is not null)
        {
            if (show)
            {
                _chrome.Visibility = Visibility.Visible; // it may have been collapsed while docked
                SyncOverlay();
                _overlay.Show(activate: true);
                _chrome.PlayPause.Focus(FocusState.Programmatic);
            }
            else if (_overlay.Visible)
            {
                _overlay.Hide();
                Activate();
            }
            _chrome.Captions.Visibility = Visibility.Visible;
        }
        else
        {
            if (show && _chrome.Parent is null) Root.Children.Add(_chrome);
            _chrome.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _chrome.Captions.Visibility = Visibility.Collapsed; // the system caption buttons are visible
            _chrome.TitlePill.Visibility = _chrome.Modes.Visibility = Visibility.Collapsed; // main window shows its own
        }
        if (show) _chrome.SetChromeVisible(true);
    }

    /// <summary>Keeps the overlay window exactly over the main window's client area.</summary>
    private void SyncOverlay()
    {
        if (_overlay is null || _closed) return;
        var origin = new Win32.POINT();
        Win32.ClientToScreen(_hwnd, ref origin);
        Win32.GetClientRect(_hwnd, out var rc);
        _overlay.SetBounds(new RectInt32(origin.X, origin.Y, rc.Right - rc.Left, rc.Bottom - rc.Top));
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange || args.DidPresenterChange) SyncOverlay();
        if (args.DidPresenterChange && sender.Presenter is OverlappedPresenter p)
            _chrome.MaxGlyph.Glyph = p.State == OverlappedPresenterState.Maximized ? Ui.G.Restore : Ui.G.Max;
    }

    private void DragFromOverlay()
    {
        Win32.ReleaseCapture();
        Win32.SendMessageW(_hwnd, Win32.WM_NCLBUTTONDOWN, (IntPtr)Win32.HTCAPTION, IntPtr.Zero);
    }

    private void ToggleMaximize()
    {
        if (_isFullscreen) { SetFullscreen(false); return; }
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            if (p.State == OverlappedPresenterState.Maximized) p.Restore(); else p.Maximize();
        }
    }

    // ---- chrome auto-hide (Play) -------------------------------------------------------------

    private void KickChrome()
    {
        if (_mode != AppMode.Play) return;
        if (!_chrome.ChromeVisible) _chrome.SetChromeVisible(true);
        _chromeTimer?.Stop();
        _chromeTimer?.Start();
    }

    private void AutoHideChrome()
    {
        if (_mode != AppMode.Play) return;
        bool keep = _cropMode || _chrome.InfoPanel.Visibility == Visibility.Visible || (IsTimed && _paused && !_slideshowOn) || _pointerOverDock;
        if (keep) { _chromeTimer?.Start(); return; }
        _chrome.SetChromeVisible(false);
    }

    private bool _pointerOverDock;

    // ---- fullscreen --------------------------------------------------------------------------

    private void SetFullscreen(bool on)
    {
        if (_isFullscreen == on) return;
        _isFullscreen = on;
        TryRun(() => AppWindow.SetPresenter(on ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default), "presenter");
        _chrome.FullscreenGlyph.Glyph = on ? Ui.G.Unfull : Ui.G.Full;
        _ui.TryEnqueue(() => { SyncOverlay(); SyncVideoHost(); });
    }

    // ---- shutdown ----------------------------------------------------------------------------

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_closed) return;
        _closed = true;
        try
        {
            _exportCts?.Cancel();
            SaveResumePosition();
            RememberSegments();
            _resume.Save();
            if (!_isFullscreen && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            {
                var size = AppWindow.Size;
                if (size.Width >= 480 && size.Height >= 320) { Settings.WindowWidth = size.Width; Settings.WindowHeight = size.Height; }
            }
            _settingsStore.Save();
        }
        catch (Exception e) { Log.Write("Shutdown: " + e); }
        _anim.Stop();
        Fonts.AppFonts.Cleanup();
        _slideshowTimer?.Stop();
        _chromeTimer?.Stop();
        _preloader.Dispose();
        _overlay?.Close();
        _mpv?.Dispose();
        _mpv = null;
        _host?.Dispose();
        _host = null;
        Log.Flush();
    }

    private static void TryRun(Action action, string what)
    {
        try { action(); }
        catch (Exception e) { Log.Write($"{what}: {e.Message}"); }
    }

    private void RunOnUi(Action action)
    {
        if (_closed) return;
        _ui.TryEnqueue(() =>
        {
            if (_closed) return;
            try { action(); }
            catch (Exception e) { Log.Write("UI callback: " + e); }
        });
    }

    private void Trace(string step)
    {
        if (_smokeTest) Log.Write("TRACE " + step);
    }
}
