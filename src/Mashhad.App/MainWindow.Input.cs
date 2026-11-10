using Mashhad.App.Native;
using Mashhad.Core.Input;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
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
    // ---- keyboard -------------------------------------------------------------------------

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Let text entry and list navigation keep their keys.
        var focused = Root.XamlRoot is null ? null : FocusManager.GetFocusedElement(Root.XamlRoot);
        if (focused is TextBox or NumberBox or ComboBox or ComboBoxItem or AutoSuggestBox or PasswordBox) return;
        if (focused is ListViewItem && e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End
                or VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Enter) return;

        var name = KeyNames.FromVirtualKey((int)e.Key);
        if (name is null) return;
        var gesture = new KeyGesture(name, IsDown(VirtualKey.Control), IsDown(VirtualKey.Shift), IsDown(VirtualKey.Menu));
        var command = _shortcuts.Resolve(gesture, ActiveContexts());
        if (command == PlayerCommand.None) return;
        // Escape only belongs to us while there is something to leave.
        if (command == PlayerCommand.ExitFullscreen && !_isFullscreen && !_slideshowOn) return;

        e.Handled = true;
        Execute(command);
    }

    private ShortcutContext[] ActiveContexts()
    {
        var list = new List<ShortcutContext>(3);
        if (_cropMode) list.Add(ShortcutContext.Crop);
        if (_editMode) list.Add(ShortcutContext.Edit);
        if (_queue.Current is not null) list.Add(_kind.IsImageLike() ? ShortcutContext.Image : ShortcutContext.Timed);
        return list.ToArray();
    }

    /// <summary>Single entry point for every user command (keyboard, mouse, buttons).</summary>
    private void Execute(PlayerCommand command)
    {
        switch (command)
        {
            case PlayerCommand.OpenFile: _ = PickFilesAsync(); break;
            case PlayerCommand.OpenFolder: _ = PickFolderAsync(); break;
            case PlayerCommand.ToggleFullscreen: SetFullscreen(!_isFullscreen); break;
            case PlayerCommand.ExitFullscreen:
                if (_slideshowOn) StopSlideshow();
                SetFullscreen(false);
                break;
            case PlayerCommand.ToggleInfoPanel: ToggleSidePanel(SidePanelKind.Info); break;
            case PlayerCommand.TogglePlaylistPanel: ToggleSidePanel(SidePanelKind.Playlist); break;
            case PlayerCommand.ShowSettings: ToggleSidePanel(SidePanelKind.Settings); break;
            case PlayerCommand.ToggleEditMode: if (_kind == MediaKind.Video) SetEditMode(!_editMode); break;
            case PlayerCommand.ToggleSlideshow: ToggleSlideshow(); break;
            case PlayerCommand.Quit: Close(); break;

            case PlayerCommand.NextItem: Navigate(1); break;
            case PlayerCommand.PreviousItem: Navigate(-1); break;
            case PlayerCommand.FirstItem: JumpTo(0); break;
            case PlayerCommand.LastItem: JumpTo(_queue.Count - 1); break;

            case PlayerCommand.PlayPause: TogglePause(); break;
            case PlayerCommand.Stop:
                _mpv?.Set("pause", true);
                SeekAbsolute(0, exact: false);
                break;
            case PlayerCommand.SeekBack: SeekRelative(-5); break;
            case PlayerCommand.SeekForward: SeekRelative(5); break;
            case PlayerCommand.SeekBackFine: SeekRelative(-1, exact: true); break;
            case PlayerCommand.SeekForwardFine: SeekRelative(1, exact: true); break;
            case PlayerCommand.SeekBackLarge: SeekRelative(-60); break;
            case PlayerCommand.SeekForwardLarge: SeekRelative(60); break;
            case PlayerCommand.SeekStart: SeekAbsolute(0, exact: false); break;
            case PlayerCommand.FrameStep: FrameStep(true); break;
            case PlayerCommand.FrameBackStep: FrameStep(false); break;
            case PlayerCommand.SpeedUp: SetSpeed(SpeedSteps.Multiply(_speed, 1.1)); break;
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
            case PlayerCommand.ToggleLoopFile:
                LoopFileItem.IsChecked = !LoopFileItem.IsChecked;
                LoopFile_Click(this, new RoutedEventArgs());
                break;
            case PlayerCommand.Screenshot: TakeScreenshot(true); break;
            case PlayerCommand.ScreenshotNoSubs: TakeScreenshot(false); break;

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

    // ---- mouse on the video surface (delivered by the native host) ------------------------

    private bool _dragging;
    private int _dragX, _dragY;

    private void Host_MouseDown(HostButton button, int x, int y)
    {
        // Clicking the native surface must not strand keyboard focus.
        PlayButton.Focus(FocusState.Programmatic);
        switch (button)
        {
            case HostButton.Left:
                _dragging = true;
                _dragX = x;
                _dragY = y;
                if (_cropMode) CropStart(x, y);
                break;
            case HostButton.Right:
                if (IsTimed) TogglePause();
                break;
            case HostButton.Middle:
                if (_kind.IsImageLike()) ZoomFit();
                break;
            case HostButton.Back: Navigate(-1); break;
            case HostButton.Forward: Navigate(1); break;
        }
    }

    private void Host_MouseMove(int x, int y)
    {
        if (_isFullscreen)
        {
            // Reveal the controls when the pointer approaches the bottom edge.
            if (_host is not null && y >= _host.Height - 110) ShowFullscreenChrome();
            else if (_host is not null) _host.HideCursor = false;
            _chromeTimer?.Stop();
            _chromeTimer?.Start();
        }
        if (!_dragging) return;
        if (_cropMode) CropDrag(x, y);
        else if (_kind.IsImageLike())
        {
            PanBy(x - _dragX, y - _dragY);
            _dragX = x;
            _dragY = y;
        }
    }

    private void Host_MouseUp(HostButton button, int x, int y)
    {
        if (button != HostButton.Left) return;
        _dragging = false;
    }

    private void Host_Wheel(double notches, int x, int y, bool ctrl)
    {
        if (_queue.Current is null) return;
        if (_kind.IsImageLike()) ZoomAt(notches * 0.2, x, y);
        else if (ctrl) SeekRelative(notches * 5);
        else ChangeVolume(notches * 5);
    }

    // ---- drag & drop onto the XAML parts of the window ------------------------------------

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
            e.AcceptedOperation = DataPackageOperation.Copy;
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
        catch (Exception ex)
        {
            Log.Write("Drop: " + ex);
        }
    }

    // ---- pickers --------------------------------------------------------------------------

    private async Task PickFilesAsync()
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary, ViewMode = PickerViewMode.Thumbnail };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeFilter.Add("*");
            var files = await picker.PickMultipleFilesAsync();
            if (files is { Count: > 0 }) OpenPaths(files.Select(f => f.Path).ToList());
        }
        catch (Exception e)
        {
            Log.Write("File picker: " + e);
            ShowStatus(Loc.F("Error.Open", e.Message));
        }
    }

    private async Task<string?> PickFolderPathAsync()
    {
        try
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception e)
        {
            Log.Write("Folder picker: " + e);
            return null;
        }
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
            if (file is null || _mpv is null) return;
            if (_mpv.TryCommand("sub-add", file.Path, "select")) ShowOsd(Loc.T("Osd.SubtitleLoaded"));
        }
        catch (Exception e)
        {
            Log.Write("Subtitle picker: " + e);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e) => _ = PickFilesAsync();
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _ = PickFolderAsync();
}
