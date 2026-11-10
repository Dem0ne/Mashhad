using System.Globalization;
using Mashhad.Core.Media;
using Mashhad.Core.Playback;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = System.IO.Path;
using Windows.Foundation;

namespace Mashhad.App.Views;

public enum SelectionAction { Play, Edit, ShowInExplorer, Delete, Clear }

/// <summary>
/// Folder mode: Library-style browsing (folder pane, breadcrumb, filters, date groups, justified
/// grid, multi-select) in Cinema's visual language (dark wall, floating cards, focus ring).
/// </summary>
public sealed class GalleryView : Grid
{
    public const double Gap = 8;
    public readonly GalleryModel Model = new();
    private readonly ThumbnailCache _thumbs;
    private readonly Func<CultureInfo> _culture;

    private readonly Border _pane;
    private readonly StackPanel _paneItems = new() { Spacing = 1 };
    private readonly Button _pinButton;
    private readonly Grid _content = new();
    private readonly StackPanel _crumbs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly TextBlock _title = Ui.Text("", 34, true), _meta = Ui.Text("", 13.5, false, 0.6);
    private readonly Dictionary<GalleryFilter, Button> _filters = new();
    private readonly TextBox _search = new() { Width = 220, Height = 34, CornerRadius = new CornerRadius(17), VerticalAlignment = VerticalAlignment.Center };
    private readonly DropDownButton _sort = new() { Height = 34, CornerRadius = new CornerRadius(17), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
    private readonly ScrollViewer _scroller = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ItemsRepeater _rows = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyError = Ui.Text("", 13, false, 0.9);
    private readonly Border _selectionBar;
    private readonly TextBlock _selectionText = Ui.Text("", 14, true);
    private readonly Dictionary<int, Tile> _realized = new();
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private string? _folder;
    private int _focus = -1, _anchor = -1;
    private double _layoutWidth;
    private bool _pinned = true, _paneShown = true;
    private DispatcherTimerLike? _relayout;

    public event Action<int>? OpenRequested;
    public event Action<string>? NavigateRequested;
    public event Action? OpenFolderRequested, OpenFilesRequested, SettingsRequested, SlideshowRequested, PinToggled, FilterChanged;
    public event Action<GallerySort, bool>? SortChanged;
    public event Action<SelectionAction>? SelectionCommand;

    public string? Folder => _folder;
    public int FocusIndex => _focus;
    public IReadOnlyCollection<string> SelectedPaths => _selected;
    public bool HasItems => Model.Visible.Count > 0;

    private sealed class Tile
    {
        public required Grid Root;
        public required Image Image;
        public required Border Ring;
        public required Border Check;
        public required Border Badge;
        public required TextBlock BadgeText;
        public required Border Caption;
        public int Index;
    }

    internal GalleryView(ThumbnailCache thumbs, Func<CultureInfo> culture)
    {
        _thumbs = thumbs;
        _culture = culture;

        // ---- folder pane ----
        _pinButton = Ui.IconButton(Ui.G.Pin, "Gallery.PinPane", 32, 13, (_, _) => PinToggled?.Invoke());
        var footer = new Grid { Margin = new Thickness(4, 6, 0, 0) };
        footer.Children.Add(Ui.Row(8, Ui.Icon(Ui.G.Settings, 15), Ui.LText("Menu.Settings", 13.5)));
        var settingsHit = new Button { Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Content = footer, IsTabStop = false };
        settingsHit.Click += (_, _) => SettingsRequested?.Invoke();
        var foot = new Grid { BorderBrush = Ui.White(0.07), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 6, 0, 0) };
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foot.Children.Add(settingsHit);
        SetColumn(_pinButton, 1);
        foot.Children.Add(_pinButton);
        var paneGrid = new Grid();
        paneGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        paneGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paneGrid.Children.Add(new ScrollViewer { Content = _paneItems, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        SetRow(foot, 1);
        paneGrid.Children.Add(foot);
        _pane = Ui.Pill(paneGrid, radius: 16, padding: new Thickness(8));
        _pane.Width = 250;
        _pane.HorizontalAlignment = HorizontalAlignment.Left;
        _pane.Margin = new Thickness(12, 56, 0, 12);
        _pane.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(180) };
        Canvas.SetZIndex(_pane, 5);

        // ---- header ----
        foreach (var (f, key, glyph) in new[] { (GalleryFilter.All, "Gallery.All", ""), (GalleryFilter.Photos, "Gallery.Photos", Ui.G.Pictures), (GalleryFilter.Videos, "Gallery.Videos", Ui.G.Video) })
        {
            var label = Ui.LText(key, 13);
            var b = new Button
            {
                Content = glyph.Length > 0 ? Ui.Row(6, Ui.Icon(glyph, 13), label) : label,
                Height = 30, Padding = new Thickness(13, 0, 13, 0), CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), IsTabStop = false,
            };
            var filter = f;
            b.Click += (_, _) => { Model.SetFilter(filter, _search.Text); UpdateFilterButtons(); Relayout(keepFocus: false); FilterChanged?.Invoke(); };
            _filters[f] = b;
        }
        var seg = Ui.Pill(Ui.Row(2, _filters.Values.ToArray()), radius: 11, padding: new Thickness(3));
        L.SetPlaceholder(_search, "Gallery.Search");
        _search.TextChanged += (_, _) => { Model.SetFilter(Model.Filter, _search.Text); Relayout(keepFocus: false); FilterChanged?.Invoke(); };
        _sort.Content = Ui.Row(8, Ui.Icon(Ui.G.Sort, 14), Ui.LText("Gallery.Sort", 13.5));
        var sortMenu = new MenuFlyout();
        foreach (var (key, sort, desc) in new[] { ("Sort.Newest", GallerySort.DateModified, true), ("Sort.Oldest", GallerySort.DateModified, false), ("Sort.Name", GallerySort.Name, false), ("Sort.Size", GallerySort.Size, true) })
        {
            var item = new MenuFlyoutItem();
            L.SetText(item, key);
            var (s, d) = (sort, desc);
            item.Click += (_, _) => SortChanged?.Invoke(s, d);
            sortMenu.Items.Add(item);
        }
        _sort.Flyout = sortMenu;
        var slideshow = Ui.PillButton(Ui.G.Slideshow, "Bar.Slideshow", (_, _) => SlideshowRequested?.Invoke(), accent: true, height: 36);

        var head = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleBlock = Ui.Column(0, _crumbs, _title, _meta);
        _title.Margin = new Thickness(0, -2, 0, 0);
        head.Children.Add(titleBlock);
        var tools = Ui.Row(10, seg, _search, _sort, slideshow);
        tools.VerticalAlignment = VerticalAlignment.Bottom;
        SetColumn(tools, 1);
        head.Children.Add(tools);

        // ---- grid ----
        _rows.Layout = new StackLayout { Orientation = Orientation.Vertical, Spacing = 0 };
        _rows.ItemTemplate = new RowFactory(this);
        _rows.ElementClearing += (_, e) => Forget(e.Element);
        _scroller.Content = _rows;
        _scroller.Padding = new Thickness(0, 0, 0, 120);
        _scroller.Margin = new Thickness(0, 4, 0, 0);
        _scroller.SizeChanged += (_, e) => { if (Math.Abs(e.NewSize.Width - _layoutWidth) > 1) Relayout(keepFocus: true); };

        // ---- empty state ----
        _empty = Ui.Column(14, Ui.Logo(96), Ui.LText("App.Name", 28, true), Ui.LText("Empty.Hint", 14, false, 0.72),
            Ui.Row(10, Ui.PillButton(Ui.G.OpenFolder, "Menu.OpenFolder", (_, _) => OpenFolderRequested?.Invoke(), accent: true, height: 38),
                Ui.PillButton(Ui.G.Pictures, "Menu.OpenFile", (_, _) => OpenFilesRequested?.Invoke(), height: 38)), _emptyError);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        foreach (var c in _empty.Children.OfType<FrameworkElement>()) c.HorizontalAlignment = HorizontalAlignment.Center;
        ((TextBlock)_empty.Children[2]).TextWrapping = TextWrapping.Wrap;
        ((TextBlock)_empty.Children[2]).TextAlignment = TextAlignment.Center;
        ((TextBlock)_empty.Children[2]).MaxWidth = 480;
        _emptyError.TextWrapping = TextWrapping.Wrap;
        _emptyError.MaxWidth = 520;
        _emptyError.Foreground = Ui.Solid(255, 0xFF, 0xB4, 0xA0);

        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _content.Children.Add(head);
        SetRow(_scroller, 1);
        _content.Children.Add(_scroller);
        SetRowSpan(_empty, 2);
        _content.Children.Add(_empty);
        _content.Margin = new Thickness(278, 52, 24, 0);

        // ---- selection bar ----
        _selectionBar = Ui.Pill(Ui.Row(4, _selectionText,
            Ui.PillButton(Ui.G.Play, "Mode.Play", (_, _) => SelectionCommand?.Invoke(SelectionAction.Play), accent: true),
            Ui.PillButton(Ui.G.Cut, "Mode.Edit", (_, _) => SelectionCommand?.Invoke(SelectionAction.Edit)),
            Ui.PillButton(Ui.G.OpenFolder, "Menu.ShowInExplorer", (_, _) => SelectionCommand?.Invoke(SelectionAction.ShowInExplorer)),
            Ui.PillButton(Ui.G.Delete, "Gallery.Delete", (_, _) => SelectionCommand?.Invoke(SelectionAction.Delete)),
            Ui.IconButton(Ui.G.Close, "Common.Cancel", 36, 12, (_, _) => SelectionCommand?.Invoke(SelectionAction.Clear))), radius: 26, padding: new Thickness(18, 7, 7, 7));
        _selectionText.Margin = new Thickness(0, 0, 10, 0);
        _selectionBar.HorizontalAlignment = HorizontalAlignment.Center;
        _selectionBar.VerticalAlignment = VerticalAlignment.Bottom;
        _selectionBar.Margin = new Thickness(278, 0, 24, 24);
        _selectionBar.Visibility = Visibility.Collapsed;
        Canvas.SetZIndex(_selectionBar, 6);

        var fade = new Rectangle { Height = 120, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        var fb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        fb.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, 11, 12, 15), Offset = 0 });
        fb.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(235, 11, 12, 15), Offset = 1 });
        fade.Fill = fb;

        Children.Add(_content);
        Children.Add(fade);
        Children.Add(_pane);
        Children.Add(_selectionBar);
        PointerMoved += OnPointerMoved;
        UpdateFilterButtons();
        ShowEmpty(null);
    }

    // ---- data ---------------------------------------------------------------------------------

    /// <summary>Shows a folder. <paramref name="focusPath"/> is the item to focus (e.g. the one just played).</summary>
    internal void ShowFolder(string folder, IReadOnlyList<GalleryEntry> entries, string? focusPath)
    {
        _folder = folder;
        _selected.Clear();
        Model.Load(entries);
        Model.SetFilter(GalleryFilter.All, null);
        _search.Text = "";
        UpdateFilterButtons();
        _focus = focusPath is null ? -1 : Model.IndexOf(focusPath);
        _anchor = _focus;
        UpdateHeader();
        BuildPane();
        _empty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _emptyError.Text = entries.Count == 0 ? Loc.T("Gallery.EmptyFolder") : "";
        _emptyError.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _scroller.Visibility = entries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        Relayout(keepFocus: true);
        UpdateSelectionBar();
    }

    internal void ShowEmpty(string? error)
    {
        _folder = null;
        Model.Load(Array.Empty<GalleryEntry>());
        _title.Text = Loc.T("App.Name");
        _meta.Text = "";
        _crumbs.Children.Clear();
        _empty.Visibility = Visibility.Visible;
        _scroller.Visibility = Visibility.Collapsed;
        _emptyError.Text = error ?? "";
        _emptyError.Visibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
        BuildPane();
        _rows.ItemsSource = null;
    }

    /// <summary>Called when header sizes or thumbnails reveal real aspect ratios.</summary>
    internal void AspectsChanged()
    {
        _relayout ??= new DispatcherTimerLike(this, 220, () => Relayout(keepFocus: true));
        _relayout.Kick();
    }

    /// <summary>Re-applies texts after a language change.</summary>
    internal void RefreshTexts()
    {
        UpdateHeader();
        BuildPane();
        Relayout(keepFocus: true);
        UpdateSelectionBar();
    }

    private void UpdateHeader()
    {
        _crumbs.Children.Clear();
        if (_folder is null) return;
        var dir = new DirectoryInfo(_folder);
        _title.Text = string.IsNullOrEmpty(dir.Name) ? _folder : dir.Name;
        var chain = new List<DirectoryInfo>();
        for (var p = dir.Parent; p is not null && chain.Count < 4; p = p.Parent) chain.Insert(0, p);
        foreach (var p in chain)
        {
            var b = new Button { Content = Ui.Text(string.IsNullOrEmpty(p.Name) ? p.FullName : p.Name, 13, false, 0.6), Padding = new Thickness(4, 2, 4, 2), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), IsTabStop = false };
            var path = p.FullName;
            b.Click += (_, _) => NavigateRequested?.Invoke(path);
            _crumbs.Children.Add(b);
            _crumbs.Children.Add(Ui.Text(FlowDirection == FlowDirection.RightToLeft ? "‹" : "›", 13, false, 0.4));
        }
        _meta.Text = Loc.F("Gallery.Meta", Model.TotalCount, Model.PhotoCount, Model.VideoCount, FormatSize(Model.TotalBytes));
    }

    private void UpdateFilterButtons()
    {
        foreach (var (f, b) in _filters)
            b.Background = f == Model.Filter ? Ui.White(0.12) : new SolidColorBrush(Colors.Transparent);
    }

    internal static string FormatSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : "0.#", CultureInfo.InvariantCulture) + " " + u[i];
    }

    // ---- folder pane --------------------------------------------------------------------------

    internal Func<IReadOnlyList<string>>? RecentFolders;

    private void BuildPane()
    {
        _paneItems.Children.Clear();
        void Item(string glyph, string label, string path, int indent = 0, bool selected = false, string? count = null)
        {
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = Ui.Icon(glyph, 15);
            icon.Margin = new Thickness(indent * 16, 0, 11, 0);
            var text = Ui.Text(label, 13.5);
            SetColumn(text, 1);
            content.Children.Add(icon);
            content.Children.Add(text);
            if (count is not null) { var c = Ui.Text(count, 12, false, 0.42); SetColumn(c, 2); content.Children.Add(c); }
            var b = new Button
            {
                Content = content, Height = 36, Padding = new Thickness(11, 0, 11, 0), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(0),
                Background = selected ? Ui.White(0.09) : new SolidColorBrush(Colors.Transparent), IsTabStop = false,
            };
            b.Click += (_, _) => NavigateRequested?.Invoke(path);
            var wrap = new Grid();
            wrap.Children.Add(b);
            if (selected) wrap.Children.Add(new Rectangle { Width = 3, Height = 18, RadiusX = 1.5, RadiusY = 1.5, Fill = Ui.Brand(), HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false });
            _paneItems.Children.Add(wrap);
        }
        void Header(string key)
        {
            var t = Ui.LText(key, 11.5, true, 0.45);
            t.Margin = new Thickness(11, 12, 0, 5);
            _paneItems.Children.Add(t);
        }

        foreach (var (glyph, key, folder) in new[]
                 {
                     (Ui.G.Pictures, "Pane.Pictures", Environment.SpecialFolder.MyPictures), (Ui.G.Video, "Pane.Videos", Environment.SpecialFolder.MyVideos),
                     (Ui.G.Desktop, "Pane.Desktop", Environment.SpecialFolder.DesktopDirectory),
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) Item(glyph, Loc.T(key), path, selected: Same(path, _folder));
        }
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads)) Item(Ui.G.Download, Loc.T("Pane.Downloads"), downloads, selected: Same(downloads, _folder));

        if (_folder is not null)
        {
            Header("Pane.Folders");
            var chain = new List<DirectoryInfo>();
            for (var p = new DirectoryInfo(_folder); p is not null && chain.Count < 4; p = p.Parent) chain.Insert(0, p);
            for (int i = 0; i < chain.Count; i++)
                Item(Ui.G.Folder, string.IsNullOrEmpty(chain[i].Name) ? chain[i].FullName : chain[i].Name, chain[i].FullName, Math.Min(i, 3), i == chain.Count - 1,
                    i == chain.Count - 1 ? Model.TotalCount.ToString(CultureInfo.InvariantCulture) : null);
            try
            {
                foreach (var sub in new DirectoryInfo(_folder).EnumerateDirectories()
                             .Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                             .OrderBy(d => d.Name, NaturalComparer.Instance).Take(24))
                    Item(Ui.G.Folder, sub.Name, sub.FullName, Math.Min(chain.Count, 4));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        var recent = RecentFolders?.Invoke().Where(p => !Same(p, _folder) && Directory.Exists(p)).Take(5).ToList() ?? new List<string>();
        if (recent.Count > 0)
        {
            Header("Pane.Recent");
            foreach (var r in recent) Item(Ui.G.Recent, Path.GetFileName(r.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : r, r);
        }
    }

    private static bool Same(string? a, string? b) => a is not null && b is not null && FolderScanner.PathEquals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b));

    internal void SetPinned(bool pinned)
    {
        _pinned = pinned;
        ((FontIcon)_pinButton.Content).Glyph = pinned ? Ui.G.Unpin : Ui.G.Pin;
        L.SetTip(_pinButton, pinned ? "Gallery.UnpinPane" : "Gallery.PinPane");
        _content.Margin = new Thickness(pinned ? 278 : 24, 52, 24, 0);
        _selectionBar.Margin = new Thickness(pinned ? 278 : 24, 0, 24, 24);
        ShowPane(pinned);
        Relayout(keepFocus: true);
    }

    internal bool PanePinned => _pinned;

    /// <summary>Shows/hides the pane (when unpinned it floats over the grid and auto-hides).</summary>
    internal void ShowPane(bool show)
    {
        _paneShown = show || _pinned;
        _pane.Opacity = _paneShown ? 1 : 0;
        _pane.IsHitTestVisible = _paneShown;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pinned) return;
        double x = e.GetCurrentPoint(this).Position.X;
        bool nearEdge = x < 18;
        bool overPane = _paneShown && x < 270;
        if (nearEdge && !_paneShown) ShowPane(true);
        else if (!overPane && _paneShown) ShowPane(false);
    }

    // ---- layout -------------------------------------------------------------------------------

    private void Relayout(bool keepFocus)
    {
        if (_folder is null) return;
        double width = Math.Max(200, _scroller.ActualWidth > 0 ? _scroller.ActualWidth - 4 : 900);
        _layoutWidth = _scroller.ActualWidth;
        double target = width > 1300 ? 210 : width > 900 ? 185 : 150;
        Model.GroupByDay = SortIsByDate;
        var rows = Model.Layout(width, target, Gap, _culture(), n => Loc.F("Gallery.Count", n));
        _realized.Clear();
        _rows.ItemsSource = rows.ToList();
        if (!keepFocus) _focus = Math.Min(_focus, Model.Visible.Count - 1);
        _meta.Text = _folder is null ? "" : Loc.F("Gallery.Meta", Model.TotalCount, Model.PhotoCount, Model.VideoCount, FormatSize(Model.TotalBytes));
        if (keepFocus && _focus >= 0) _ = BringIntoViewLater(_focus);
    }

    internal bool SortIsByDate { get; set; } = true;

    private sealed class RowFactory : IElementFactory
    {
        private readonly GalleryView _v;
        public RowFactory(GalleryView v) => _v = v;
        public UIElement GetElement(ElementFactoryGetArgs args) => _v.BuildRow((GalleryRow)args.Data);
        public void RecycleElement(ElementFactoryRecycleArgs args) => _v.Forget(args.Element);
    }

    private UIElement BuildRow(GalleryRow row)
    {
        if (row.Kind == GalleryRowKind.Header)
        {
            var h = Ui.Row(10, Ui.Text(row.Title, 15, true), Ui.Text(row.Subtitle, 13, false, 0.5));
            h.Margin = new Thickness(2, row.Group == 0 ? 10 : 22, 0, 10);
            return h;
        }
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap, Margin = new Thickness(0, 0, 0, Gap), Tag = row };
        for (int i = row.Start; i < row.Start + row.Count; i++) panel.Children.Add(BuildTile(i, row.Height));
        return panel;
    }

    private UIElement BuildTile(int index, double height)
    {
        var e = Model.Visible[index];
        double w = JustifiedLayout.ItemWidth(e.Aspect, height);
        var root = new Grid { Width = w, Height = height, CornerRadius = new CornerRadius(10), Background = Ui.White(0.05) };
        var img = new Image { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        root.Children.Add(img);
        var badgeText = Ui.Text("", 11.5, true);
        var badge = new Border { Child = badgeText, Background = Ui.Black(0.55), CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 2, 7, 2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8), Visibility = Visibility.Collapsed };
        if (e.Item.Kind == MediaKind.AnimatedImage) { badgeText.Text = "GIF"; badge.Visibility = Visibility.Visible; }
        root.Children.Add(badge);
        var caption = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(12, 28, 12, 10), Visibility = Visibility.Collapsed,
            Background = Gradient(),
            Child = Ui.Column(0, Ui.Text(e.Item.FileName, 13.5, true), Ui.Text($"{GalleryView.FormatSize(e.Size)} · {e.Modified.ToString("d", _culture())}", 12, false, 0.75)),
        };
        root.Children.Add(caption);
        var check = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Ui.Brand(), Child = Ui.Icon(Ui.G.Check, 11), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8), Visibility = Visibility.Collapsed };
        root.Children.Add(check);
        var ring = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(3), BorderBrush = new SolidColorBrush(Ui.Rose), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        root.Children.Add(ring);

        var tile = new Tile { Root = root, Image = img, Ring = ring, Check = check, Badge = badge, BadgeText = badgeText, Caption = caption, Index = index };
        root.Tag = tile;
        _realized[index] = tile;
        ApplyState(tile);

        root.PointerPressed += (_, args) =>
        {
            var props = args.GetCurrentPoint(root).Properties;
            if (!props.IsLeftButtonPressed) return;
            var mods = args.KeyModifiers;
            Click(tile.Index, (mods & Windows.System.VirtualKeyModifiers.Control) != 0, (mods & Windows.System.VirtualKeyModifiers.Shift) != 0);
        };
        root.DoubleTapped += (_, _) => OpenRequested?.Invoke(tile.Index);
        root.PointerEntered += (_, _) => { if (!IsSelected(tile.Index)) root.Opacity = 0.92; };
        root.PointerExited += (_, _) => root.Opacity = 1;

        bool isVideo = e.Item.Kind == MediaKind.Video;
        _thumbs.Request(e.Item.Path, isVideo, (uint)Math.Clamp(height * 1.6, 160, 512), info =>
        {
            if (!_realized.TryGetValue(tile.Index, out var t) || !ReferenceEquals(t, tile)) return;
            tile.Image.Source = info.Image;
            if (isVideo && info.Duration is TimeSpan d)
            {
                tile.BadgeText.Text = "▶ " + TimeFormat.Clock(d.TotalSeconds);
                tile.Badge.Visibility = Visibility.Visible;
            }
            if (info.Aspect is double a && !e.AspectKnown && Math.Abs(a - e.Aspect) / e.Aspect > 0.05)
            {
                e.Aspect = a;
                e.AspectKnown = true;
                AspectsChanged();
            }
        });
        return root;
    }

    private static LinearGradientBrush Gradient()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        b.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, 0, 0, 0), Offset = 0 });
        b.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(200, 0, 0, 0), Offset = 1 });
        return b;
    }

    private void Forget(UIElement element)
    {
        if (element is StackPanel p)
            foreach (var c in p.Children)
                if (c is Grid { Tag: Tile t } && _realized.TryGetValue(t.Index, out var cur) && ReferenceEquals(cur, t))
                    _realized.Remove(t.Index);
    }

    private bool IsSelected(int index) => index >= 0 && index < Model.Visible.Count && _selected.Contains(Model.Visible[index].Item.Path);

    private void ApplyState(Tile t)
    {
        bool focus = t.Index == _focus, sel = IsSelected(t.Index);
        t.Ring.Visibility = focus || sel ? Visibility.Visible : Visibility.Collapsed;
        t.Ring.BorderBrush = focus ? new SolidColorBrush(Ui.Rose) : new SolidColorBrush(Ui.Crimson);
        t.Ring.BorderThickness = new Thickness(focus ? 3 : 2);
        t.Check.Visibility = sel ? Visibility.Visible : Visibility.Collapsed;
        t.Caption.Visibility = focus && t.Root.Width > 170 ? Visibility.Visible : Visibility.Collapsed;
        t.Root.Scale = focus ? new System.Numerics.Vector3(1.03f, 1.03f, 1) : System.Numerics.Vector3.One;
        t.Root.CenterPoint = new System.Numerics.Vector3((float)t.Root.Width / 2, (float)t.Root.Height / 2, 0);
        Canvas.SetZIndex(t.Root, focus ? 2 : 0);
    }

    private void RefreshStates()
    {
        foreach (var t in _realized.Values) ApplyState(t);
        UpdateSelectionBar();
    }

    // ---- focus & selection --------------------------------------------------------------------

    private void Click(int index, bool ctrl, bool shift)
    {
        if (shift && _anchor >= 0)
        {
            int a = Math.Min(_anchor, index), b = Math.Max(_anchor, index);
            if (!ctrl) _selected.Clear();
            for (int i = a; i <= b; i++) _selected.Add(Model.Visible[i].Item.Path);
        }
        else if (ctrl)
        {
            var p = Model.Visible[index].Item.Path;
            if (!_selected.Remove(p)) _selected.Add(p);
            _anchor = index;
        }
        else
        {
            _selected.Clear();
            _anchor = index;
        }
        _focus = index;
        RefreshStates();
    }

    internal void MoveFocus(int dx, int dy)
    {
        if (Model.Visible.Count == 0) return;
        if (FlowDirection == FlowDirection.RightToLeft) dx = -dx;
        _focus = Model.Move(_focus, dx, dy, Gap);
        _anchor = _focus;
        RefreshStates();
        _ = BringIntoViewLater(_focus);
    }

    internal void FocusIndexAt(int index)
    {
        if (Model.Visible.Count == 0) return;
        _focus = Math.Clamp(index, 0, Model.Visible.Count - 1);
        _anchor = _focus;
        RefreshStates();
        _ = BringIntoViewLater(_focus);
    }

    internal void FocusPath(string path)
    {
        int i = Model.IndexOf(path);
        if (i >= 0) FocusIndexAt(i);
    }

    internal void ToggleSelectionFocused()
    {
        if (_focus < 0) return;
        var p = Model.Visible[_focus].Item.Path;
        if (!_selected.Remove(p)) _selected.Add(p);
        RefreshStates();
    }

    internal void SelectAll()
    {
        foreach (var e in Model.Visible) _selected.Add(e.Item.Path);
        RefreshStates();
    }

    internal void ClearSelection()
    {
        _selected.Clear();
        RefreshStates();
    }

    private void UpdateSelectionBar()
    {
        _selectionBar.Visibility = _selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _selectionText.Text = Loc.F("Gallery.Selected", _selected.Count);
    }

    private async Task BringIntoViewLater(int index)
    {
        int row = Model.RowOf(index);
        if (row < 0) return;
        await Task.Yield();
        var el = _rows.TryGetElement(row) ?? _rows.GetOrCreateElement(row);
        el?.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.4, AnimationDesired = true });
    }

    /// <summary>Bounds of a visible tile relative to <paramref name="relativeTo"/>, for the expand animation.</summary>
    internal (Rect Bounds, ImageSource? Image)? TileBounds(int index, UIElement relativeTo)
    {
        if (!_realized.TryGetValue(index, out var t) || t.Root.XamlRoot is null) return null;
        var r = t.Root.TransformToVisual(relativeTo).TransformBounds(new Rect(0, 0, t.Root.ActualWidth, t.Root.ActualHeight));
        return (r, t.Image.Source);
    }

    internal ImageSource? ThumbnailOf(string path) => _thumbs.TryGet(path, out var info) ? info.Image : null;

    /// <summary>Debounces relayouts (aspect ratios arrive in bursts).</summary>
    private sealed class DispatcherTimerLike
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _t;
        public DispatcherTimerLike(FrameworkElement owner, int ms, Action a)
        {
            _t = owner.DispatcherQueue.CreateTimer();
            _t.Interval = TimeSpan.FromMilliseconds(ms);
            _t.IsRepeating = false;
            _t.Tick += (_, _) => a();
        }
        public void Kick() { _t.Stop(); _t.Start(); }
    }
}
