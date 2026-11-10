using System.Diagnostics;
using System.Globalization;
using Mashhad.Core.Imaging;
using Mashhad.Core.Media;
using Mashhad.Core.Mpv;
using Mashhad.Core.Playback;
using Mashhad.Core.Settings;
using Mashhad.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private bool _slideshowOn;

    // ---- toasts ------------------------------------------------------------------------------

    private void ShowToast(string text)
    {
        if (_mode == AppMode.Play)
        {
            _chrome.ToastText.Text = text;
            _chrome.Toast.Visibility = Visibility.Visible;
        }
        else
        {
            _toastText.Text = text;
            _toast.Visibility = Visibility.Visible;
        }
        _toastTimer?.Stop();
        _toastTimer?.Start();
        if (_smokeTest) Log.Write("Toast: " + text);
    }

    // ---- info panel (Play) -------------------------------------------------------------------

    private void ToggleInfoPanel()
    {
        if (_mode != AppMode.Play) return;
        bool show = _chrome.InfoPanel.Visibility != Visibility.Visible;
        _chrome.InfoPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) RefreshInfoPanel();
    }

    private async void RefreshInfoPanel()
    {
        try
        {
            var rows = _chrome.InfoRows;
            rows.Children.Clear();
            var item = _queue.Current;
            if (item is null) return;
            string path = item.Path;
            AddInfo("Info.Name", item.FileName);
            AddInfo("Info.Folder", System.IO.Path.GetDirectoryName(path));
            try
            {
                var fi = new FileInfo(path);
                if (fi.Exists)
                {
                    AddInfo("Info.Size", Views.GalleryView.FormatSize(fi.Length));
                    AddInfo("Info.Modified", Views.GalleryView.FormatDate(fi.LastWriteTime, Loc.Current.Culture, "g"));
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
                    if (_mpv.GetDouble("container-fps") is double fps && fps > 0) AddInfo("Info.FrameRate", fps.ToString("0.###", CultureInfo.InvariantCulture) + " fps");
                    var hw = _mpv.GetString("hwdec-current");
                    AddInfo("Info.Decoder", hw is { Length: > 0 } && hw != "no" ? Loc.F("Info.DecoderHardware", hw) : Loc.T("Info.DecoderSoftware"));
                    AddInfo("Info.AudioCodec", _mpv.GetString("audio-codec-name"));
                    if (_mpv.GetLong("audio-params/channel-count") is long ch && ch > 0) AddInfo("Info.Channels", ch.ToString(CultureInfo.InvariantCulture));
                }
            }
            if (item.Kind.IsImageLike())
            {
                var meta = await Task.Run(() => ImageMetadataReader.Read(path));
                if (_closed || _queue.Current?.Path != path) return;
                foreach (var r in meta) AddInfo(r.Key, r.Value);
            }
        }
        catch (Exception e) { Log.Write("Info panel: " + e); }
    }

    private void AddInfo(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var label = Ui.Text(Loc.T(key), 12, false, 0.6);
        var text = Ui.Text(value, 13.5);
        text.TextWrapping = TextWrapping.Wrap;
        text.IsTextSelectionEnabled = true;
        _chrome.InfoRows.Children.Add(Ui.Column(1, label, text));
    }

    // ---- settings ----------------------------------------------------------------------------

    private void ToggleSettings()
    {
        if (_mode != AppMode.Folder) SetMode(AppMode.Folder);
        bool show = _settingsSheet.Visibility != Visibility.Visible;
        if (show) { _settingsSheet.Load(Settings); UpdateAbout(); }
        _settingsSheet.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WireSettings()
    {
        _settingsSheet.CloseRequested += () => _settingsSheet.Visibility = Visibility.Collapsed;
        _settingsSheet.Changed += () =>
        {
            string oldLanguage = Settings.Language, oldHwdec = Settings.HardwareDecoding;
            _settingsSheet.Save(Settings);
            _queue.Wrap = Settings.WrapGallery;
            _slideshow.Loop = Settings.SlideshowLoop;
            _slideshow.ImageDuration = TimeSpan.FromSeconds(Settings.SlideshowSeconds);
            _mpv?.Set("screenshot-format", Settings.ScreenshotFormat);
            if (oldHwdec != Settings.HardwareDecoding) _mpv?.Set("hwdec", Settings.HardwareDecoding);
            if (oldLanguage != Settings.Language) ApplyLanguage();
            _settingsStore.Save();
        };
        _settingsSheet.ChooseScreenshotFolder += async () =>
        {
            if (await PickFolderPathAsync() is not { Length: > 0 } p) return;
            Settings.ScreenshotDirectory = p;
            _mpv?.Set("screenshot-directory", p);
            _settingsStore.Save();
            UpdateAbout();
        };
        _settingsSheet.ChooseExportFolder += async () =>
        {
            if (await PickFolderPathAsync() is not { Length: > 0 } p) return;
            Settings.ExportDirectory = p;
            _settingsStore.Save();
            UpdateAbout();
        };
        _settingsSheet.ChooseFfmpegFolder += async () =>
        {
            if (await PickFolderPathAsync() is not { Length: > 0 } p) return;
            Settings.FfmpegDirectory = p;
            _settingsStore.Save();
            UpdateAbout();
        };
        _settingsSheet.DefaultApps += async () =>
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:defaultapps")); }
            catch (Exception ex) { Log.Write("Default apps: " + ex.Message); }
        };
    }

    private void UpdateAbout()
    {
        var tools = FfmpegLocator.Locate(NullIfEmpty(Settings.FfmpegDirectory));
        string shots = string.IsNullOrWhiteSpace(Settings.ScreenshotDirectory) ? AppPaths.DefaultScreenshotDirectory : Settings.ScreenshotDirectory;
        string export = string.IsNullOrWhiteSpace(Settings.ExportDirectory) ? Loc.T("Settings.NextToSource") : Settings.ExportDirectory;
        _settingsSheet.Folders.Text = $"{Loc.T("Settings.ScreenshotFolder")}: {shots}\n{Loc.T("Settings.ExportFolder")}: {export}\nFFmpeg: {tools?.Ffmpeg ?? Loc.T("Settings.NotFound")}";
        string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        string engine = _mpv is not null ? "libmpv (client API " + MpvPlayer.ApiVersion + ")" : "libmpv: " + Loc.T("Settings.NotFound");
        _settingsSheet.About.Text = $"{Loc.T("App.Name")} {version}\n{engine}\n{Loc.T("Settings.AboutLine")}";
    }

    // ---- files -------------------------------------------------------------------------------

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
        catch (Exception e) { Log.Write("Explorer: " + e.Message); }
    }

    private async Task SavePlaylistAsync()
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
            ShowToast(Loc.F("Status.Done", file.Name));
        }
        catch (Exception e) { Log.Write("Save playlist: " + e); ShowToast(Loc.F("Status.Failed", e.Message)); }
    }

    // ---- slideshow (images and videos mixed) -------------------------------------------------

    private void ToggleSlideshow()
    {
        if (_slideshowOn) StopSlideshow();
        else if (_mode == AppMode.Folder && _gallery.Model.Visible.Count > 0)
        {
            OpenFromGallery(Math.Max(0, _gallery.FocusIndex), AppMode.Play);
            StartSlideshow();
        }
        else StartSlideshow();
    }

    private void StartSlideshow()
    {
        if (_queue.Count < 2 || _mpv is null) { ShowToast(Loc.T("Slideshow.NeedItems")); return; }
        _slideshowOn = true;
        _slideshow.ImageDuration = TimeSpan.FromSeconds(Settings.SlideshowSeconds);
        _slideshow.Loop = Settings.SlideshowLoop;
        if (Settings.SlideshowShuffle) _queue.Shuffle = true;
        ShowToast(Loc.T("Osd.SlideshowOn"));
        if (_mode != AppMode.Play) RequestMode(AppMode.Play);
        ScheduleSlideshow();
    }

    private void StopSlideshow()
    {
        if (!_slideshowOn) return;
        _slideshowOn = false;
        _slideshowTimer?.Stop();
        _queue.Shuffle = false;
        ShowToast(Loc.T("Osd.SlideshowOff"));
    }

    private void ScheduleSlideshow()
    {
        if (!_slideshowOn || _slideshowTimer is null) return;
        _slideshowTimer.Stop();
        var (mode, delay) = _slideshow.For(_kind);
        if (mode == Core.Playback.SlideshowAdvance.AtEndOfFile)
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
        if (_slideshow.Advance(_queue) is null) { StopSlideshow(); return; }
        LoadCurrent();
    }

    // ---- self-test: a tour of every mode, with screenshots taken by CI ------------------------

    private void StartSmokeTest()
    {
        Log.Write($"SMOKE: window loaded, mpv={_mpv is not null}, overlayEnv={Environment.GetEnvironmentVariable("MASHHAD_NO_OVERLAY") ?? "-"}, files={_startupFiles.Count}, lang={Loc.Current.Language}");
        _ = SmokeTourAsync();
    }

    private async Task SmokeTourAsync()
    {
        var started = DateTime.UtcNow;
        string originalLanguage = Settings.Language;
        async Task<bool> Until(Func<bool> condition, double seconds)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until && !_closed) { if (condition()) return true; await Task.Delay(100); }
            return condition();
        }
        async Task Snap(string name)
        {
            Log.Write("SNAP " + name);
            await Task.Delay(1800); // CI captures the screen meanwhile
        }
        async Task Language(string lang)
        {
            Settings.Language = lang;
            ApplyLanguage();
            Log.Write($"SMOKE: language {lang} applied, rtl={Root.FlowDirection == FlowDirection.RightToLeft}");
            await Task.Delay(500);
        }
        try
        {
            bool folderTour = _startupFiles.Count == 1 && Directory.Exists(_startupFiles[0]);
            if (folderTour && _mpv is not null)
            {
                if (!await Until(() => _gallery.Model.Visible.Count > 0, 20)) throw new InvalidOperationException("folder did not load");
                await Task.Delay(2500); // thumbnails
                await Snap("folder");

                int video = _gallery.Model.Visible.ToList().FindIndex(e => e.Item.Kind == MediaKind.Video);
                if (video >= 0)
                {
                    _gallery.FocusIndexAt(video);
                    OpenFromGallery(video, AppMode.Play);
                    if (!await Until(() => _mode == AppMode.Play && _fileReady && _position > 1.0, 25)) throw new InvalidOperationException($"video did not play (mode={_mode}, ready={_fileReady}, pos={_position:0.##})");
                    Log.Write($"SMOKE: playing kind={_kind} duration={_duration:0.###} position={_position:0.###} vo={_mpv.GetString("current-vo")} hwdec={_mpv.GetString("hwdec-current")} overlay={_overlay is not null}");
                    _mpv.Set("pause", true);
                    KickChrome();
                    await Task.Delay(300);
                    Log.Write($"SMOKE: chrome {_chrome.ActualWidth:0}x{_chrome.ActualHeight:0}, loaded={_chrome.IsLoaded}, visible={_overlay?.Visible}, root={(_chrome.XamlRoot is { } xr ? $"{xr.Size.Width:0}x{xr.Size.Height:0}" : "none")}");
                    await Snap("play");

                    Execute(Core.Input.PlayerCommand.ToggleEditMode);
                    if (!await Until(() => _mode == AppMode.Edit, 5)) throw new InvalidOperationException("edit mode not entered");
                    _segments.SetStart(0.5); _segments.SetEnd(2.0); _segments.AddAt(3.0); _segments.SetEnd(4.5);
                    await Until(() => _keyframes is not null, 15);
                    await Task.Delay(3000); // waveform + thumbnails
                    Log.Write($"SMOKE: edit mode, segments={_segments.Count}, keyframes={_keyframes?.Count ?? -1}, command={_editor.Command.Text.Split('\n')[0]}");
                    await Snap("edit");

                    await Language("ar");
                    await Snap("edit-ar");
                    RequestMode(AppMode.Play);
                    await Until(() => _mode == AppMode.Play && !_anim.IsRunning, 5);
                    KickChrome();
                    await Snap("play-ar");
                    RequestMode(AppMode.Folder);
                    await Until(() => _mode == AppMode.Folder && !_anim.IsRunning, 5);
                    await Snap("folder-ar");
                    await Language(originalLanguage);
                }

                int image = _gallery.Model.Visible.ToList().FindIndex(e => e.Item.Kind.IsImageLike());
                if (image >= 0)
                {
                    OpenFromGallery(image, AppMode.Play);
                    if (!await Until(() => _mode == AppMode.Play && _fileReady && (_mpv.GetLong("video-params/w") ?? 0) > 0, 15)) throw new InvalidOperationException("image did not show");
                    ToggleInfoPanel();
                    KickChrome();
                    await Task.Delay(800);
                    await Snap("image");
                }
                RequestMode(AppMode.Folder);
                await Until(() => _mode == AppMode.Folder && !_anim.IsRunning, 5);
            }
            else if (_startupFiles.Count > 0 && _mpv is not null)
            {
                bool ok = await Until(() => _fileReady && (_kind.IsImageLike() ? (_mpv.GetLong("video-params/w") ?? 0) > 0 : _position > 1.0), 30);
                if (!ok) throw new InvalidOperationException($"media did not play (ready={_fileReady}, pos={_position:0.##})");
                Log.Write($"SMOKE: kind={_kind} duration={_duration:0.###} position={_position:0.###} size={_mpv.GetLong("video-params/w")}x{_mpv.GetLong("video-params/h")} " +
                          $"hwdec-current={_mpv.GetString("hwdec-current")} vo={_mpv.GetString("current-vo")} overlay={_overlay is not null} mode={_mode}");
            }
            else
            {
                await Task.Delay(2500);
                await Language("ar");
                await Language(originalLanguage);
            }
            Log.Write($"SMOKE RESULT: PASS ({(DateTime.UtcNow - started).TotalSeconds:0.0} s)");
            Environment.ExitCode = 0;
        }
        catch (Exception e)
        {
            Log.Write("SMOKE RESULT: FAIL (" + e.Message + ")");
            Environment.ExitCode = 1;
        }
        Settings.Language = originalLanguage;
        Close();
    }
}
