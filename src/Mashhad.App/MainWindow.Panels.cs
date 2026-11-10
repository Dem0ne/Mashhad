using System.Diagnostics;
using System.Globalization;
using Mashhad.Core.Imaging;
using Mashhad.Core.Media;
using Mashhad.Core.Mpv;
using Mashhad.Core.Playback;
using Mashhad.Core.Settings;
using Mashhad.Editing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private enum SidePanelKind { None, Info, Playlist, Segments, Settings }

    private SidePanelKind _panel = SidePanelKind.None;
    private SidePanelKind _savedPanel = SidePanelKind.None;
    private bool _loadingSettings;
    private bool _syncingPlaylist;
    private string? _revealPath;
    private bool _slideshowOn;

    // ---- side panel -----------------------------------------------------------------------

    private void ToggleSidePanel(SidePanelKind kind) => ShowSidePanel(_panel == kind ? SidePanelKind.None : kind);

    private void ShowSidePanel(SidePanelKind kind)
    {
        if (kind == SidePanelKind.Segments && !_editMode) kind = SidePanelKind.None;
        _panel = kind;
        SidePanel.Visibility = kind == SidePanelKind.None ? Visibility.Collapsed : Visibility.Visible;
        InfoPanel.Visibility = kind == SidePanelKind.Info ? Visibility.Visible : Visibility.Collapsed;
        PlaylistView.Visibility = kind == SidePanelKind.Playlist ? Visibility.Visible : Visibility.Collapsed;
        SegmentsPanel.Visibility = kind == SidePanelKind.Segments ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = kind == SidePanelKind.Settings ? Visibility.Visible : Visibility.Collapsed;
        RefreshSidePanel();
    }

    private void RefreshSidePanel()
    {
        if (SidePanelTitle is null) return;
        SidePanelTitle.Text = _panel switch
        {
            SidePanelKind.Info => Loc.T("Panel.Info"),
            SidePanelKind.Playlist => Loc.T(_queue.Source == QueueSource.Playlist ? "Panel.Playlist" : "Panel.Folder"),
            SidePanelKind.Segments => Loc.T("Panel.Segments"),
            SidePanelKind.Settings => Loc.T("Panel.Settings"),
            _ => "",
        };
        switch (_panel)
        {
            case SidePanelKind.Info: RefreshInfoPanel(); break;
            case SidePanelKind.Playlist: RefreshPlaylistPanel(); break;
            case SidePanelKind.Settings: UpdateAbout(); break;
        }
    }

    private void CloseSidePanel_Click(object sender, RoutedEventArgs e) => ShowSidePanel(SidePanelKind.None);
    private void ToggleInfo_Click(object sender, RoutedEventArgs e) => ToggleSidePanel(SidePanelKind.Info);
    private void TogglePlaylist_Click(object sender, RoutedEventArgs e) => ToggleSidePanel(SidePanelKind.Playlist);
    private void Settings_Click(object sender, RoutedEventArgs e) => ToggleSidePanel(SidePanelKind.Settings);

    // ---- playlist / gallery list ----------------------------------------------------------

    private void RefreshPlaylistPanel()
    {
        if (_panel != SidePanelKind.Playlist) return;
        _syncingPlaylist = true;
        PlaylistView.ItemsSource = _queue.Items.Select(i => i.FileName).ToList();
        _syncingPlaylist = false;
        SyncPlaylistSelection();
    }

    private void SyncPlaylistSelection()
    {
        if (_panel != SidePanelKind.Playlist) return;
        _syncingPlaylist = true;
        PlaylistView.SelectedIndex = _queue.CurrentIndex;
        if (PlaylistView.SelectedItem is not null) PlaylistView.ScrollIntoView(PlaylistView.SelectedItem);
        _syncingPlaylist = false;
    }

    private void PlaylistView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPlaylist || PlaylistView.SelectedIndex < 0) return;
        JumpTo(PlaylistView.SelectedIndex);
    }

    // ---- info / EXIF ----------------------------------------------------------------------

    private async void RefreshInfoPanel()
    {
        try
        {
            InfoRows.Children.Clear();
            var item = _queue.Current;
            if (item is null) return;
            string path = item.Path;

            AddInfo("Info.Name", item.FileName);
            AddInfo("Info.Folder", Path.GetDirectoryName(path));
            try
            {
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    AddInfo("Info.Size", FormatSize(info.Length));
                    AddInfo("Info.Modified", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }

            if (_mpv is not null && _fileReady)
            {
                long w = _mpv.GetLong("video-params/w") ?? 0, h = _mpv.GetLong("video-params/h") ?? 0;
                if (w > 0 && h > 0) AddInfo("Info.Dimensions", $"{w} × {h}");
                AddInfo("Info.Format", _mpv.GetString("file-format"));
                if (IsTimed)
                {
                    AddInfo("Info.Duration", TimeFormat.Clock(_duration, millis: true));
                    AddInfo("Info.VideoCodec", _mpv.GetString("video-codec"));
                    if (_mpv.GetDouble("container-fps") is double fps && fps > 0)
                        AddInfo("Info.FrameRate", fps.ToString("0.###", CultureInfo.InvariantCulture) + " fps");
                    AddInfo("Info.Decoder", _mpv.GetString("hwdec-current") is { Length: > 0 } hw && hw != "no"
                        ? Loc.F("Info.DecoderHardware", hw)
                        : Loc.T("Info.DecoderSoftware"));
                    AddInfo("Info.AudioCodec", _mpv.GetString("audio-codec-name"));
                    if (_mpv.GetLong("audio-params/channel-count") is long ch && ch > 0)
                        AddInfo("Info.Channels", ch.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (item.Kind.IsImageLike())
            {
                var rows = await Task.Run(() => ImageMetadataReader.Read(path));
                if (_closed || _queue.Current?.Path != path || _panel != SidePanelKind.Info) return;
                foreach (var row in rows) AddInfo(row.Key, row.Value);
            }
        }
        catch (Exception e)
        {
            Log.Write("Info panel: " + e);
        }
    }

    private void AddInfo(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var block = new StackPanel { Spacing = 1 };
        block.Children.Add(new TextBlock
        {
            Text = Loc.T(key),
            Opacity = 0.65,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });
        block.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        InfoRows.Children.Add(block);
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    // ---- status strip ---------------------------------------------------------------------

    private void ShowStatus(string text, string? revealPath = null)
    {
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = text;
        StatusProgress.Visibility = Visibility.Collapsed;
        StatusCancel.Visibility = Visibility.Collapsed;
        _revealPath = revealPath;
        StatusReveal.Visibility = revealPath is null ? Visibility.Collapsed : Visibility.Visible;
        if (_smokeTest) Log.Write("Status: " + text);
    }

    private void ShowProgress(string text, double fraction)
    {
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = text;
        StatusProgress.Visibility = Visibility.Visible;
        StatusProgress.IsIndeterminate = fraction <= 0;
        StatusProgress.Value = Math.Clamp(fraction, 0, 1);
        StatusCancel.Visibility = Visibility.Visible;
        StatusReveal.Visibility = Visibility.Collapsed;
    }

    private void StatusCancel_Click(object sender, RoutedEventArgs e) => _exportCts?.Cancel();
    private void StatusClose_Click(object sender, RoutedEventArgs e) => StatusBar.Visibility = Visibility.Collapsed;
    private void StatusReveal_Click(object sender, RoutedEventArgs e) => RevealInExplorer(_revealPath);
    private void ShowInExplorer_Click(object sender, RoutedEventArgs e) => RevealInExplorer(_queue.Current?.Path);

    private static void RevealInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            if (Directory.Exists(path)) psi.ArgumentList.Add(path);
            else psi.Arguments = "/select,\"" + path + "\"";
            Process.Start(psi)?.Dispose();
        }
        catch (Exception e)
        {
            Log.Write("Explorer: " + e.Message);
        }
    }

    private async void SavePlaylist_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_queue.Count == 0) return;
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.VideosLibrary, SuggestedFileName = "playlist" };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeChoices.Add("M3U8", new List<string> { ".m3u8" });
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            PlaylistFile.Write(file.Path, _queue.Items);
            ShowStatus(Loc.F("Status.Done", file.Name), file.Path);
        }
        catch (Exception ex)
        {
            Log.Write("Save playlist: " + ex);
            ShowStatus(Loc.F("Status.Failed", ex.Message));
        }
    }

    // ---- slideshow (images and videos mixed) ----------------------------------------------

    private void ToggleSlideshow()
    {
        if (_slideshowOn) StopSlideshow();
        else StartSlideshow();
    }

    private void StartSlideshow()
    {
        if (_queue.Count < 2 || _mpv is null)
        {
            SlideshowToggle.IsChecked = false;
            ShowStatus(Loc.T("Slideshow.NeedItems"));
            return;
        }
        _slideshowOn = true;
        SlideshowToggle.IsChecked = true;
        _slideshow.ImageDuration = TimeSpan.FromSeconds(Settings.SlideshowSeconds);
        _slideshow.Loop = Settings.SlideshowLoop;
        if (Settings.SlideshowShuffle) _queue.Shuffle = true;
        ShowOsd(Loc.T("Osd.SlideshowOn"));
        ScheduleSlideshow();
    }

    private void StopSlideshow()
    {
        if (!_slideshowOn) return;
        _slideshowOn = false;
        _slideshowTimer?.Stop();
        SlideshowToggle.IsChecked = false;
        _queue.Shuffle = ShuffleItem.IsChecked;
        ShowOsd(Loc.T("Osd.SlideshowOff"));
    }

    /// <summary>Arms the advance for the item now showing: a timer for images, end-of-file for videos.</summary>
    private void ScheduleSlideshow()
    {
        if (!_slideshowOn || _slideshowTimer is null) return;
        _slideshowTimer.Stop();
        var (mode, delay) = _slideshow.For(_kind);
        if (mode == Mashhad.Core.Playback.SlideshowAdvance.AtEndOfFile)
        {
            _mpv?.Set("loop-file", "no");
            _mpv?.Set("pause", false);
            return;
        }
        _slideshowTimer.Interval = delay;
        _slideshowTimer.Start();
    }

    private void SlideshowAdvance()
    {
        if (!_slideshowOn) return;
        if (_slideshow.Advance(_queue) is null)
        {
            StopSlideshow();
            return;
        }
        LoadCurrent();
    }

    private void SlideshowToggle_Click(object sender, RoutedEventArgs e) => ToggleSlideshow();

    // ---- settings -------------------------------------------------------------------------

    private void LoadSettingsIntoUi()
    {
        _loadingSettings = true;
        SelectByTag(LanguageBox, Settings.Language);
        SelectByTag(ThemeBox, Settings.Theme.ToString());
        SelectByTag(ScreenshotFormatBox, Settings.ScreenshotFormat);
        HwdecSwitch.IsOn = Settings.HardwareDecoding != "no";
        ResumeSwitch.IsOn = Settings.ResumePlayback;
        WrapSwitch.IsOn = Settings.WrapGallery;
        SingleInstanceSwitch.IsOn = Settings.SingleInstance;
        SlideshowSecondsBox.Value = Settings.SlideshowSeconds;
        SlideshowLoopSwitch.IsOn = Settings.SlideshowLoop;
        SlideshowShuffleSwitch.IsOn = Settings.SlideshowShuffle;
        _loadingSettings = false;
        UpdateAbout();
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        foreach (var o in box.Items)
            if (o is ComboBoxItem { Tag: string t } item && t == tag) { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox box, string fallback) =>
        box.SelectedItem is ComboBoxItem { Tag: string t } ? t : fallback;

    private void Setting_Changed(object sender, object e)
    {
        if (_loadingSettings) return;
        string oldLanguage = Settings.Language;
        var oldTheme = Settings.Theme;
        string oldHwdec = Settings.HardwareDecoding;

        Settings.Language = TagOf(LanguageBox, "system");
        Settings.Theme = Enum.TryParse<AppTheme>(TagOf(ThemeBox, "System"), out var theme) ? theme : AppTheme.System;
        Settings.ScreenshotFormat = TagOf(ScreenshotFormatBox, "png");
        Settings.HardwareDecoding = HwdecSwitch.IsOn ? "d3d11va" : "no";
        Settings.ResumePlayback = ResumeSwitch.IsOn;
        Settings.WrapGallery = WrapSwitch.IsOn;
        Settings.SingleInstance = SingleInstanceSwitch.IsOn;
        Settings.SlideshowLoop = SlideshowLoopSwitch.IsOn;
        Settings.SlideshowShuffle = SlideshowShuffleSwitch.IsOn;
        SettingsStore.Sanitize(Settings);

        _queue.Wrap = Settings.WrapGallery;
        _slideshow.Loop = Settings.SlideshowLoop;
        _mpv?.Set("screenshot-format", Settings.ScreenshotFormat);
        if (oldHwdec != Settings.HardwareDecoding) _mpv?.Set("hwdec", Settings.HardwareDecoding);
        if (oldTheme != Settings.Theme) ApplyTheme();
        if (oldLanguage != Settings.Language) ApplyLanguage();
        _settingsStore.Save();
    }

    private void SlideshowSeconds_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loadingSettings || double.IsNaN(args.NewValue)) return;
        Settings.SlideshowSeconds = Math.Clamp(args.NewValue, 1, 3600);
        _slideshow.ImageDuration = TimeSpan.FromSeconds(Settings.SlideshowSeconds);
        _settingsStore.Save();
    }

    private async void ChooseScreenshotFolder_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderPathAsync() is not { Length: > 0 } path) return;
        Settings.ScreenshotDirectory = path;
        _mpv?.Set("screenshot-directory", path);
        _settingsStore.Save();
        UpdateAbout();
    }

    private async void ChooseExportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderPathAsync() is not { Length: > 0 } path) return;
        Settings.ExportDirectory = path;
        _settingsStore.Save();
        UpdateAbout();
    }

    private async void ChooseFfmpegFolder_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderPathAsync() is not { Length: > 0 } path) return;
        Settings.FfmpegDirectory = path;
        _settingsStore.Save();
        UpdateAbout();
    }

    private async void DefaultApps_Click(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:defaultapps")); }
        catch (Exception ex) { Log.Write("Default apps: " + ex.Message); }
    }

    private void UpdateAbout()
    {
        if (AboutText is null) return;
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        string shots = string.IsNullOrWhiteSpace(Settings.ScreenshotDirectory) ? AppPaths.DefaultScreenshotDirectory : Settings.ScreenshotDirectory;
        string export = string.IsNullOrWhiteSpace(Settings.ExportDirectory) ? Loc.T("Settings.NextToSource") : Settings.ExportDirectory;
        FoldersSummary.Text =
            $"{Loc.T("Settings.ScreenshotFolder")}: {shots}\n" +
            $"{Loc.T("Settings.ExportFolder")}: {export}\n" +
            $"FFmpeg: {tools?.Ffmpeg ?? Loc.T("Settings.NotFound")}";

        string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        string engine = _mpv is not null ? "libmpv (client API " + MpvPlayer.ApiVersion + ")" : "libmpv: " + Loc.T("Settings.NotFound");
        AboutText.Text = $"{Loc.T("App.Name")} {version}\n{engine}\n{Loc.T("Settings.AboutLine")}";
    }

    // ---- self-test (used by CI: Mashhad.exe --smoke-test [file] --smoke-log <path>) ---------

    private void StartSmokeTest()
    {
        Log.Write($"SMOKE: window loaded, mpv={( _mpv is not null)}, files={_startupFiles.Count}, lang={Loc.Current.Language}");
        var started = DateTime.UtcNow;
        bool exercised = false;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.IsRepeating = true;
        timer.Tick += (_, _) =>
        {
            double elapsed = (DateTime.UtcNow - started).TotalSeconds;
            try
            {
                if (!exercised && elapsed > 1)
                {
                    exercised = true;
                    ExerciseUiForSmokeTest();
                }

                bool wantsMedia = _startupFiles.Count > 0;
                bool ready = !wantsMedia
                    ? elapsed > 3
                    : _fileReady && (_kind.IsImageLike()
                        ? (_mpv?.GetLong("video-params/w") ?? 0) > 0 && elapsed > 3
                        : _position > 1.0);
                if (ready)
                {
                    timer.Stop();
                    if (_mpv is not null)
                    {
                        Log.Write($"SMOKE: kind={_kind} duration={_duration:0.###} position={_position:0.###} " +
                                  $"size={_mpv.GetLong("video-params/w")}x{_mpv.GetLong("video-params/h")} " +
                                  $"hwdec-current={_mpv.GetString("hwdec-current")} vo={_mpv.GetString("current-vo")} " +
                                  $"gpu-context={_mpv.GetString("current-gpu-context")} host={_host?.Width}x{_host?.Height} " +
                                  $"queue={_queue.Count} tracks={_mpv.GetTracks().Count}");
                    }
                    bool pass = !wantsMedia || _mpv is not null;
                    Log.Write("SMOKE RESULT: " + (pass ? "PASS" : "FAIL (no player)"));
                    Environment.ExitCode = pass ? 0 : 1;
                    Close();
                }
                else if (elapsed > 40)
                {
                    timer.Stop();
                    Log.Write($"SMOKE RESULT: FAIL (timeout; fileReady={_fileReady} position={_position:0.###} mpv={_mpv is not null})");
                    Environment.ExitCode = 1;
                    Close();
                }
            }
            catch (Exception e)
            {
                timer.Stop();
                Log.Write("SMOKE RESULT: FAIL (" + e + ")");
                Environment.ExitCode = 1;
                Close();
            }
        };
        timer.Start();
    }

    /// <summary>Touches the UI paths that only fail at run time (panels, language switch, edit mode).</summary>
    private void ExerciseUiForSmokeTest()
    {
        foreach (var kind in new[] { SidePanelKind.Info, SidePanelKind.Playlist, SidePanelKind.Settings, SidePanelKind.None })
            ShowSidePanel(kind);
        string original = Settings.Language;
        foreach (var language in new[] { "ar", "en", original })
        {
            Settings.Language = language;
            ApplyLanguage();
            Log.Write($"SMOKE: language {language} applied, rtl={Root.FlowDirection == FlowDirection.RightToLeft}");
        }
        if (_kind == MediaKind.Video && _queue.Current is not null)
        {
            SetEditMode(true);
            ShowSidePanel(SidePanelKind.Segments);
            _segments.SetStart(0.2);
            _segments.SetEnd(0.8);
            _segments.AddAt(1.0);
            Log.Write($"SMOKE: edit mode on, segments={_segments.Count}");
            _segments.Clear();
            SetEditMode(false);
            _mpv?.Set("pause", false);
        }
        ShowStatus("smoke");
        StatusBar.Visibility = Visibility.Collapsed;
        Log.Write("SMOKE: UI exercised");
    }
}
