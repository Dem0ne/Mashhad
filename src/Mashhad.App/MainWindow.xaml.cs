using Mashhad.App.Native;
using Mashhad.Core.Imaging;
using Mashhad.Core.Input;
using Mashhad.Core.Media;
using Mashhad.Core.Mpv;
using Mashhad.Core.Playback;
using Mashhad.Core.Settings;
using Mashhad.Editing;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace Mashhad.App;

/// <summary>
/// The single application window. Split into partial files by concern:
/// Playback (mpv), Input (keyboard/mouse/drop), Images, Editing (FFmpeg), Panels (side panels, settings, status).
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new(AppPaths.SettingsFile);
    private readonly ResumeStore _resume = new(AppPaths.ResumeFile);
    private readonly MediaQueue _queue = new();
    private readonly Preloader _preloader = new();
    private readonly ShortcutMap _shortcuts = ShortcutMap.CreateDefault();
    private readonly SlideshowPolicy _slideshow = new();
    private readonly IntPtr _hwnd;
    private readonly bool _smokeTest;
    private readonly IReadOnlyList<string> _startupFiles;

    /// <summary>
    /// The UI thread's dispatcher, captured once on that thread. Reading <c>Window.DispatcherQueue</c>
    /// from libmpv's event thread or a worker is itself a cross-thread XAML access, so callbacks from
    /// other threads must only ever use this field.
    /// </summary>
    private readonly DispatcherQueue _ui;

    private VideoHost? _host;
    private MpvPlayer? _mpv;
    private bool _closed;
    private bool _isFullscreen;
    private bool _pointerOverChrome;
    private DispatcherQueueTimer? _chromeTimer;
    private DispatcherQueueTimer? _slideshowTimer;

    private AppSettings Settings => _settingsStore.Current;

    public MainWindow(IReadOnlyList<string> startupFiles, bool smokeTest)
    {
        _startupFiles = startupFiles;
        _smokeTest = smokeTest;
        Trace("ctor: start");
        Loc.SetLanguage(Settings.Language);

        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        Trace("ctor: XAML loaded");
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        Title = "Mashhad";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
            Root.Background = new SolidColorBrush(Colors.Transparent);
        }

        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        TryRun(() => AppWindow.SetIcon(Path.Combine(assets, "Mashhad.ico")), "icon");
        TryRun(() =>
        {
            TitleLogo.Source = new BitmapImage(new Uri(Path.Combine(assets, "Logo32.png")));
            EmptyLogo.Source = new BitmapImage(new Uri(Path.Combine(assets, "Logo256.png")));
        }, "logo");
        TryRun(() => AppWindow.Resize(new SizeInt32(Settings.WindowWidth, Settings.WindowHeight)), "resize");

        _queue.Wrap = Settings.WrapGallery;
        Timeline.SeekRequested += Timeline_SeekRequested;
        _segments.Changed += Segments_Changed;
        Root.PreviewKeyDown += Root_PreviewKeyDown;
        Root.Loaded += Root_Loaded;
        Root.ActualThemeChanged += (_, _) => UpdateCaptionButtonColors();
        VideoArea.LayoutUpdated += (_, _) => SyncVideoHostBounds();
        Closed += MainWindow_Closed;

        _chromeTimer = _ui.CreateTimer();
        _chromeTimer.Interval = TimeSpan.FromSeconds(2.5);
        _chromeTimer.IsRepeating = false;
        _chromeTimer.Tick += (_, _) => HideFullscreenChrome();

        _slideshowTimer = _ui.CreateTimer();
        _slideshowTimer.IsRepeating = false;
        _slideshowTimer.Tick += (_, _) => SlideshowAdvance();

        ApplyTheme();
        ApplyLanguage();
        LoadSettingsIntoUi();
        UpdateModeUi();
        Trace("ctor: done");
    }

    /// <summary>Start-up breadcrumbs (smoke test only): the log is written synchronously, so it survives a crash.</summary>
    private void Trace(string step)
    {
        if (_smokeTest) Log.Write("TRACE " + step);
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        Trace("loaded: start");
        InitializePlayer();
        Trace("loaded: player initialised");
        PlayButton.Focus(FocusState.Programmatic);
        if (_startupFiles.Count > 0) OpenPaths(_startupFiles);
        Trace("loaded: files opened");
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

    // ---- appearance -----------------------------------------------------------------------

    private void ApplyTheme()
    {
        Root.RequestedTheme = Settings.Theme switch
        {
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.Light => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
        UpdateCaptionButtonColors();
    }

    private void UpdateCaptionButtonColors()
    {
        TryRun(() =>
        {
            var titleBar = AppWindow.TitleBar;
            bool dark = Root.ActualTheme == ElementTheme.Dark;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverForegroundColor = dark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverBackgroundColor = dark ? ColorHelper.FromArgb(0x22, 0xFF, 0xFF, 0xFF) : ColorHelper.FromArgb(0x18, 0, 0, 0);
        }, "caption colors");
    }

    private void ApplyLanguage()
    {
        Loc.SetLanguage(Settings.Language);
        Root.FlowDirection = Loc.Current.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        UpdateTitle();
        UpdateSegmentsUi();
        RefreshSidePanel();
        UpdateAbout();
    }

    private void UpdateTitle()
    {
        var item = _queue.Current;
        string text = item is null ? Loc.T("App.Name") : $"{item.FileName} — {Loc.T("App.Name")}";
        TitleText.Text = text;
        Title = text;
        IndexText.Text = _queue.Count > 1 ? $"{_queue.CurrentIndex + 1} / {_queue.Count}" : "";
    }

    // ---- video host layout ----------------------------------------------------------------

    private void VideoArea_SizeChanged(object sender, SizeChangedEventArgs e) => SyncVideoHostBounds();

    /// <summary>Keeps the native video window exactly over the VideoArea placeholder (physical pixels).</summary>
    private void SyncVideoHostBounds()
    {
        if (_host is null || _closed || VideoArea.XamlRoot is null) return;
        if (VideoArea.ActualWidth < 1 || VideoArea.ActualHeight < 1) return;
        double scale = VideoArea.XamlRoot.RasterizationScale;
        // TransformBounds gives an axis-aligned rectangle even when the layout is mirrored (RTL).
        var rect = VideoArea.TransformToVisual(null)
            .TransformBounds(new Windows.Foundation.Rect(0, 0, VideoArea.ActualWidth, VideoArea.ActualHeight));
        int oldW = _host.Width, oldH = _host.Height;
        _host.SetBounds((int)Math.Round(rect.X * scale), (int)Math.Round(rect.Y * scale),
            (int)Math.Round(rect.Width * scale), (int)Math.Round(rect.Height * scale));
        if ((oldW != _host.Width || oldH != _host.Height) && _kind.IsImageLike()) OnViewportResized();
    }

    private void UpdateVideoHostVisibility() => _host?.SetVisible(_mpv is not null && _queue.Current is not null);

    // ---- fullscreen -----------------------------------------------------------------------

    private void SetFullscreen(bool on)
    {
        if (_isFullscreen == on) return;
        _isFullscreen = on;
        TryRun(() => AppWindow.SetPresenter(on ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default), "presenter");
        TitleRow.Height = new GridLength(on ? 0 : 40);
        TitleBarArea.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        FullscreenIcon.Glyph = on ? "" : "";
        if (on)
        {
            _savedPanel = _panel;
            ShowSidePanel(SidePanelKind.None);
            BottomBar.Visibility = Visibility.Collapsed;
            if (_host is not null) _host.HideCursor = true;
        }
        else
        {
            _chromeTimer?.Stop();
            BottomBar.Visibility = Visibility.Visible;
            if (_host is not null) _host.HideCursor = false;
            ShowSidePanel(_savedPanel);
        }
    }

    private void ShowFullscreenChrome()
    {
        if (!_isFullscreen) return;
        BottomBar.Visibility = Visibility.Visible;
        if (_host is not null) _host.HideCursor = false;
        _chromeTimer?.Stop();
        _chromeTimer?.Start();
    }

    private void HideFullscreenChrome()
    {
        if (!_isFullscreen) return;
        if (_pointerOverChrome || _cropMode)
        {
            _chromeTimer?.Start(); // still in use; check again later
            return;
        }
        BottomBar.Visibility = Visibility.Collapsed;
        if (_host is not null) _host.HideCursor = true;
    }

    private void Chrome_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => _pointerOverChrome = true;

    private void Chrome_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _pointerOverChrome = false;
        if (_isFullscreen) { _chromeTimer?.Stop(); _chromeTimer?.Start(); }
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => SetFullscreen(!_isFullscreen);

    // ---- shutdown -------------------------------------------------------------------------

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_closed) return;
        _closed = true;
        try
        {
            _exportCts?.Cancel();
            SaveResumePosition();
            _resume.Save();
            if (!_isFullscreen)
            {
                var size = AppWindow.Size;
                if (size.Width >= 480 && size.Height >= 320)
                {
                    Settings.WindowWidth = size.Width;
                    Settings.WindowHeight = size.Height;
                }
            }
            _settingsStore.Save();
        }
        catch (Exception e)
        {
            Log.Write("Shutdown: " + e);
        }
        _slideshowTimer?.Stop();
        _chromeTimer?.Stop();
        _preloader.Dispose();
        _mpv?.Dispose();
        _mpv = null;
        _host?.Dispose();
        _host = null;
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
}
