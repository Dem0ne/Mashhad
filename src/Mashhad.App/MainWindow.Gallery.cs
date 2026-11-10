using System.Runtime.InteropServices;
using Mashhad.App.Views;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private int _folderLoadId;

    private void WireGallery()
    {
        _gallery.OpenRequested += i => OpenFromGallery(i, AppMode.Play);
        _gallery.NavigateRequested += p => { _ = LoadFolderAsync(p, null); };
        _gallery.OpenFolderRequested += () => _ = PickFolderAsync();
        _gallery.OpenFilesRequested += () => _ = PickFilesAsync();
        _gallery.SettingsRequested += ToggleSettings;
        _gallery.SlideshowRequested += () =>
        {
            if (_gallery.Model.Visible.Count == 0) return;
            OpenFromGallery(Math.Max(0, _gallery.FocusIndex), AppMode.Play);
            StartSlideshow();
        };
        _gallery.PinToggled += TogglePanePin;
        _gallery.FilterChanged += UpdateTitle;
        _gallery.SortChanged += (sort, desc) =>
        {
            Settings.Sort = sort;
            Settings.SortDescending = desc;
            _settingsStore.Save();
            if (_gallery.Folder is { } f) _ = LoadFolderAsync(f, FocusedItem()?.Path);
        };
        _gallery.SelectionCommand += OnSelectionCommand;
    }

    private void TogglePanePin()
    {
        Settings.FolderPanePinned = !Settings.FolderPanePinned;
        _gallery.SetPinned(Settings.FolderPanePinned);
        _settingsStore.Save();
    }

    /// <summary>Scans a folder in the background and shows it; image sizes are read afterwards.</summary>
    private async Task LoadFolderAsync(string folder, string? focusPath)
    {
        int id = ++_folderLoadId;
        var sort = Settings.Sort;
        bool desc = Settings.SortDescending;
        List<GalleryEntry> entries;
        try
        {
            entries = await Task.Run(() =>
            {
                var list = new List<GalleryEntry>();
                foreach (var item in FolderScanner.Scan(folder, sort, desc))
                {
                    try
                    {
                        var fi = new FileInfo(item.Path);
                        list.Add(new GalleryEntry(item, fi.LastWriteTime, fi.Length));
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                }
                return list;
            });
        }
        catch (Exception e)
        {
            Log.Write("Folder scan failed: " + e.Message);
            ShowToast(Loc.F("Error.Open", e.Message));
            return;
        }
        if (id != _folderLoadId || _closed) return;

        _gallery.SortIsByDate = sort == GallerySort.DateModified;
        _gallery.ShowFolder(folder, entries, focusPath ?? _queue.Current?.Path);
        RememberFolder(folder);
        UpdateTitle();
        Trace($"folder: {entries.Count} items");

        // Real aspect ratios from the image headers (cheap), applied in one relayout.
        var sizes = await Task.Run(() =>
        {
            var found = new List<(GalleryEntry Entry, double Aspect)>();
            foreach (var e in entries)
            {
                if (id != _folderLoadId) break;
                if (!e.Item.Kind.IsImageLike()) continue;
                if (ImageHeader.TryRead(e.Item.Path) is { } wh) found.Add((e, (double)wh.Width / wh.Height));
            }
            return found;
        });
        if (id != _folderLoadId || _closed) return;
        foreach (var (e, a) in sizes) { e.Aspect = a; e.AspectKnown = true; }
        if (sizes.Count > 0) _gallery.AspectsChanged();
    }

    private void RememberFolder(string folder)
    {
        var list = Settings.RecentFolders;
        list.RemoveAll(p => SameFolder(p, folder));
        list.Insert(0, folder);
        if (list.Count > 8) list.RemoveRange(8, list.Count - 8);
    }

    /// <summary>Opens a gallery item: the visible (filtered, sorted) items become the queue.</summary>
    private void OpenFromGallery(int index, AppMode mode)
    {
        var visible = _gallery.Model.Visible;
        if (index < 0 || index >= visible.Count) return;
        StopSlideshow();
        _queue.Load(visible.Select(e => e.Item), index, QueueSource.Folder);
        LoadCurrent();
        if (mode == AppMode.Edit && _kind == MediaKind.Video) SetMode(AppMode.Edit);
        else RequestMode(AppMode.Play);
    }

    private void OnSelectionCommand(SelectionAction action)
    {
        var selected = _gallery.Model.Visible.Where(e => _gallery.SelectedPaths.Contains(e.Item.Path)).Select(e => e.Item).ToList();
        switch (action)
        {
            case SelectionAction.Clear:
                _gallery.ClearSelection();
                break;
            case SelectionAction.Play when selected.Count > 0:
                _queue.Load(selected, 0, QueueSource.Playlist);
                LoadCurrent();
                RequestMode(AppMode.Play);
                break;
            case SelectionAction.Edit:
                var video = selected.FirstOrDefault(i => i.Kind == MediaKind.Video);
                if (video is null) { ShowToast(Loc.T("Gallery.NoVideoSelected")); break; }
                _queue.Load(selected.Where(i => i.Kind == MediaKind.Video), 0, QueueSource.Playlist);
                _queue.MoveTo(_queue.Items.ToList().FindIndex(i => i.Path == video.Path));
                LoadCurrent();
                SetMode(AppMode.Edit);
                break;
            case SelectionAction.ShowInExplorer when selected.Count > 0:
                RevealInExplorer(selected[0].Path);
                break;
            case SelectionAction.Delete:
                _ = DeleteSelectionAsync();
                break;
        }
    }

    /// <summary>Moves the selected (or focused) files to the Recycle Bin after confirmation.</summary>
    private async Task DeleteSelectionAsync()
    {
        if (_mode != AppMode.Folder) return;
        var paths = _gallery.SelectedPaths.ToList();
        if (paths.Count == 0 && FocusedItem() is { } f) paths.Add(f.Path);
        if (paths.Count == 0 || Root.XamlRoot is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = Loc.F("Gallery.DeleteTitle", paths.Count),
            Content = Loc.T("Gallery.DeleteBody"),
            PrimaryButtonText = Loc.T("Gallery.Delete"),
            CloseButtonText = Loc.T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            FlowDirection = Root.FlowDirection,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        int failed = 0;
        foreach (var p in paths) if (!RecycleBin.Send(p)) failed++;
        if (failed > 0) ShowToast(Loc.F("Gallery.DeleteFailed", failed));
        if (_gallery.Folder is { } folder) await LoadFolderAsync(folder, null);
    }

    /// <summary>Shell "move to Recycle Bin" (undoable), not a permanent delete.</summary>
    private static class RecycleBin
    {
        public static bool Send(string path)
        {
            var op = new SHFILEOPSTRUCTW
            {
                wFunc = FO_DELETE,
                pFrom = path + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT,
            };
            return SHFileOperationW(ref op) == 0 && !op.fAnyOperationsAborted;
        }

        private const uint FO_DELETE = 3;
        private const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCTW
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperationW(ref SHFILEOPSTRUCTW op);
    }
}
