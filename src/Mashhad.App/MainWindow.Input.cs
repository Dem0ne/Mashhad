using Mashhad.Core.Input;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Mashhad.App.Views;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    // ---- keyboard (main window and overlay window) -------------------------------------------

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void OnKey(KeyRoutedEventArgs e)
    {
        var xamlRoot = (e.OriginalSource as UIElement)?.XamlRoot ?? Root.XamlRoot;
        var focused = xamlRoot is null ? null : FocusManager.GetFocusedElement(xamlRoot);
        if (focused is TextBox or NumberBox or ComboBox or ComboBoxItem or AutoSuggestBox or PasswordBox) return;
        if (focused is ListViewItem && e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End or VirtualKey.Enter) return;

        var name = KeyNames.FromVirtualKey((int)e.Key);
        if (name is null) return;
        var gesture = new KeyGesture(name, IsDown(VirtualKey.Control), IsDown(VirtualKey.Shift), IsDown(VirtualKey.Menu));
        var command = _shortcuts.Resolve(gesture, ActiveContexts());
        if (command == PlayerCommand.None) return;
        e.Handled = true;
        Execute(command);
    }

    private ShortcutContext[] ActiveContexts()
    {
        var list = new List<ShortcutContext>(4);
        if (_cropMode) list.Add(ShortcutContext.Crop);
        if (_mode == AppMode.Folder) list.Add(ShortcutContext.Folder);
        if (_mode == AppMode.Edit) list.Add(ShortcutContext.Edit);
        if (_queue.Current is not null && _mode != AppMode.Folder) list.Add(_kind.IsImageLike() ? ShortcutContext.Image : ShortcutContext.Timed);
        return list.ToArray();
    }

    /// <summary>Single entry point for every user command (keys, buttons, menus).</summary>
    private void Execute(PlayerCommand command)
    {
        KickChrome();
        switch (command)
        {
            case PlayerCommand.OpenFile: _ = PickFilesAsync(); break;
            case PlayerCommand.OpenFolder: _ = PickFolderAsync(); break;
            case PlayerCommand.ToggleFullscreen: SetFullscreen(!_isFullscreen); break;
            case PlayerCommand.ExitFullscreen: SetFullscreen(false); break;
            case PlayerCommand.Back: Back(); break;
            case PlayerCommand.ShowFolder: RequestMode(AppMode.Folder); break;
            case PlayerCommand.ShowPlayMode: RequestMode(AppMode.Play); break;
            case PlayerCommand.ToggleFolderPane: TogglePanePin(); break;
            case PlayerCommand.ToggleInfoPanel: ToggleInfoPanel(); break;
            case PlayerCommand.TogglePlaylistPanel: RequestMode(AppMode.Folder); break;
            case PlayerCommand.ShowSettings: ToggleSettings(); break;
            case PlayerCommand.ToggleEditMode:
                if (_mode == AppMode.Edit) RequestMode(AppMode.Play);
                else if (_mode == AppMode.Folder && FocusedItem() is { Kind: MediaKind.Video }) { OpenFromGallery(_gallery.FocusIndex, AppMode.Edit); }
                else if (_kind == MediaKind.Video) RequestMode(AppMode.Edit);
                break;
            case PlayerCommand.ToggleSlideshow: ToggleSlideshow(); break;
            case PlayerCommand.Quit: Close(); break;

            case PlayerCommand.GalleryLeft: _gallery.MoveFocus(-1, 0); break;
            case PlayerCommand.GalleryRight: _gallery.MoveFocus(1, 0); break;
            case PlayerCommand.GalleryUp: _gallery.MoveFocus(0, -1); break;
            case PlayerCommand.GalleryDown: _gallery.MoveFocus(0, 1); break;
            case PlayerCommand.OpenFocused: OpenFromGallery(_gallery.FocusIndex, AppMode.Play); break;
            case PlayerCommand.ToggleSelection: _gallery.ToggleSelectionFocused(); break;
            case PlayerCommand.SelectAll: _gallery.SelectAll(); break;
            case PlayerCommand.DeleteSelection: _ = DeleteSelectionAsync(); break;

            case PlayerCommand.NextItem: Navigate(1); break;
            case PlayerCommand.PreviousItem: Navigate(-1); break;
            case PlayerCommand.FirstItem: if (_mode == AppMode.Folder) _gallery.FocusIndexAt(0); else JumpTo(0); break;
            case PlayerCommand.LastItem: if (_mode == AppMode.Folder) _gallery.FocusIndexAt(int.MaxValue); else JumpTo(_queue.Count - 1); break;

            case PlayerCommand.PlayPause:
                if (_mode == AppMode.Folder) OpenFromGallery(_gallery.FocusIndex, AppMode.Play);
                else TogglePause();
                break;
            case PlayerCommand.Stop: _mpv?.Set("pause", true); SeekAbsolute(0, false); break;
            case PlayerCommand.SeekBack: SeekRelative(-5); break;
            case PlayerCommand.SeekForward: SeekRelative(5); break;
            case PlayerCommand.SeekBack10: SeekRelative(-10); break;
            case PlayerCommand.SeekForward10: SeekRelative(10); break;
            case PlayerCommand.SeekBackFine: SeekRelative(-1, exact: true); break;
            case PlayerCommand.SeekForwardFine: SeekRelative(1, exact: true); break;
            case PlayerCommand.SeekBackLarge: SeekRelative(-60); break;
            case PlayerCommand.SeekForwardLarge: SeekRelative(60); break;
            case PlayerCommand.SeekStart: SeekAbsolute(0, false); break;
            case PlayerCommand.FrameStep: FrameStep(true); break;
            case PlayerCommand.FrameBackStep: FrameStep(false); break;
            case PlayerCommand.SpeedUp: SetSpeed(_mode == AppMode.Edit ? NextSpeedPreset() : SpeedSteps.Multiply(_speed, 1.1)); break;
            case PlayerCommand.SpeedDown: SetSpeed(SpeedSteps.Multiply(_speed, 1 / 1.1)); break;
            case PlayerCommand.SpeedReset: SetSpeed(1); break;
            case PlayerCommand.VolumeUp: ChangeVolume(5); break;
            case PlayerCommand.VolumeDown: ChangeVolume(-5); break;
            case PlayerCommand.Mute: ToggleMute(); break;
            case PlayerCommand.CycleAudio: CycleTrack("aid", "Osd.Audio"); break;
            case PlayerCommand.CycleSubtitle: CycleTrack("sid", "Osd.Subtitle"); break;
            case PlayerCommand.ToggleSubtitles: _mpv?.TryCommand("cycle", "sub-visibility"); break;
            case PlayerCommand.SubDelayPlus: _mpv?.TryCommand("add", "sub-delay", "0.1"); break;
            case PlayerCommand.SubDelayMinus: _mpv?.TryCommand("add", "sub-delay", "-0.1"); break;
            case PlayerCommand.AbLoop: CycleAbLoop(); break;
            case PlayerCommand.ToggleLoopFile: ToggleLoopFile(); break;
            case PlayerCommand.Screenshot: TakeScreenshot(true); break;
            case PlayerCommand.ScreenshotNoSubs: TakeScreenshot(false); break;
            case PlayerCommand.ShowInExplorer: RevealInExplorer(_queue.Current?.Path); break;

            case PlayerCommand.ZoomIn: ZoomBy(0.25); break;
            case PlayerCommand.ZoomOut: ZoomBy(-0.25); break;
            case PlayerCommand.ZoomFit: ZoomFit(); break;
            case PlayerCommand.ZoomActual: ZoomActual(); break;
            case PlayerCommand.RotateCw: Rotate(90); break;
            case PlayerCommand.RotateCcw: Rotate(-90); break;
            case PlayerCommand.PanLeft: PanBy(80, 0); break;
            case PlayerCommand.PanRight: PanBy(-80, 0); break;
            case PlayerCommand.PanUp: PanBy(0, 80); break;
            case PlayerCommand.PanDown: PanBy(0, -80); break;
            case PlayerCommand.ToggleCrop: if (_cropMode) EndCrop(); else BeginCrop(); break;
            case PlayerCommand.ApplyCrop: _ = SaveImageAsync(); break;
            case PlayerCommand.SaveImage: _ = SaveImageAsync(); break;

            case PlayerCommand.SetIn: SetIn(); break;
            case PlayerCommand.SetOut: SetOut(); break;
            case PlayerCommand.AddSegment: AddSegment(); break;
            case PlayerCommand.SplitSegment: SplitSegment(); break;
            case PlayerCommand.RemoveSegment: _segments.RemoveCurrent(); break;
            case PlayerCommand.PreviousKeyframe: JumpKeyframe(-1); break;
            case PlayerCommand.NextKeyframe: JumpKeyframe(1); break;
            case PlayerCommand.JumpIn: if (_segments.Current is { } s1) SeekAbsolute(s1.Start, true); break;
            case PlayerCommand.JumpOut: if (_segments.Current is { } s2) SeekAbsolute(s2.EndOr(_duration), true); break;
            case PlayerCommand.Export: _ = ExportAsync(); break;
        }
    }

    /// <summary>The editor's speed button steps through the presets and wraps around.</summary>
    private double NextSpeedPreset()
    {
        double next = SpeedSteps.StepPreset(_speed, 1);
        return Math.Abs(next - _speed) < 1e-6 ? SpeedSteps.Presets[0] : next;
    }

    /// <summary>Escape: leave full screen / slideshow / panels first, then go back a mode.</summary>
    private void Back()
    {
        if (_isFullscreen) { SetFullscreen(false); return; }
        if (_slideshowOn) { StopSlideshow(); return; }
        if (_settingsSheet.Visibility == Visibility.Visible) { _settingsSheet.Visibility = Visibility.Collapsed; return; }
        if (_chrome.InfoPanel.Visibility == Visibility.Visible) { _chrome.InfoPanel.Visibility = Visibility.Collapsed; return; }
        if (_mode == AppMode.Folder) { _gallery.ClearSelection(); return; }
        RequestMode(AppModes.Back(_mode));
    }

    private MediaItem? FocusedItem() =>
        _gallery.FocusIndex >= 0 && _gallery.FocusIndex < _gallery.Model.Visible.Count ? _gallery.Model.Visible[_gallery.FocusIndex].Item : null;

    // ---- pointer on the Play surface ---------------------------------------------------------

    private bool _dragging;
    private double _dragX, _dragY;

    /// <summary>Converts a pointer position on the chrome to physical pixels in the video window.</summary>
    private (double X, double Y) ToHost(PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(_chrome).Position;
        double s = _chrome.XamlRoot?.RasterizationScale ?? 1;
        double x = _chrome.FlowDirection == FlowDirection.RightToLeft ? _chrome.ActualWidth - p.X : p.X;
        double top = _overlay is null ? 52 : 0;
        return (x * s, (p.Y - top) * s);
    }

    private void Chrome_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        KickChrome();
        if (!_dragging) return;
        var (x, y) = ToHost(e);
        if (_cropMode) CropDrag((int)x, (int)y);
        else if (_kind.IsImageLike())
        {
            PanBy(x - _dragX, y - _dragY);
            _dragX = x;
            _dragY = y;
        }
    }

    private void Chrome_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_chrome.PointerOverChrome(e)) return;
        var props = e.GetCurrentPoint(_chrome).Properties;
        if (props.IsXButton1Pressed) { Navigate(-1); return; }
        if (props.IsXButton2Pressed) { Navigate(1); return; }
        if (props.IsMiddleButtonPressed) { if (_kind.IsImageLike()) ZoomFit(); return; }
        if (!props.IsLeftButtonPressed) return;
        (_dragX, _dragY) = ToHost(e);
        _dragging = _chrome.CapturePointer(e.Pointer);
        if (_cropMode) CropStart((int)_dragX, (int)_dragY);
    }

    private void Chrome_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _chrome.ReleasePointerCapture(e.Pointer);
    }

    private void Chrome_PointerWheel(object sender, PointerRoutedEventArgs e)
    {
        if (_chrome.PointerOverChrome(e) || _queue.Current is null) return;
        var pt = e.GetCurrentPoint(_chrome);
        double notches = pt.Properties.MouseWheelDelta / 120.0;
        bool ctrl = (e.KeyModifiers & VirtualKeyModifiers.Control) != 0;
        if (_kind.IsImageLike())
        {
            var (x, y) = ToHost(e);
            ZoomAt(notches * 0.2, (int)x, (int)y);
        }
        else if (ctrl) SeekRelative(notches * 5);
        else ChangeVolume(notches * 5);
        e.Handled = true;
    }

    // ---- drag & drop -------------------------------------------------------------------------

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems)) e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (paths.Count > 0) OpenPaths(paths);
        }
        catch (Exception ex) { Log.Write("Drop: " + ex); }
    }

    // ---- pickers -----------------------------------------------------------------------------

    private async Task PickFilesAsync()
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeFilter.Add("*");
            var files = await picker.PickMultipleFilesAsync();
            if (files is { Count: > 0 }) OpenPaths(files.Select(f => f.Path).ToList());
        }
        catch (Exception e) { Log.Write("File picker: " + e); ShowToast(Loc.F("Error.Open", e.Message)); }
    }

    private async Task<string?> PickFolderPathAsync()
    {
        try
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeFilter.Add("*");
            return (await picker.PickSingleFolderAsync())?.Path;
        }
        catch (Exception e) { Log.Write("Folder picker: " + e); return null; }
    }

    private async Task PickFolderAsync()
    {
        if (await PickFolderPathAsync() is { Length: > 0 } path) OpenPaths(new[] { path });
    }

    private async Task LoadSubtitleAsync()
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            foreach (var ext in MediaTypes.SubtitleExtensions) picker.FileTypeFilter.Add(ext);
            var file = await picker.PickSingleFileAsync();
            if (file is not null && _mpv is not null && _mpv.TryCommand("sub-add", file.Path, "select")) ShowToast(Loc.T("Osd.SubtitleLoaded"));
        }
        catch (Exception e) { Log.Write("Subtitle picker: " + e); }
    }
}
