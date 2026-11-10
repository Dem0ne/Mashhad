using Mashhad.App.Controls;
using Mashhad.Core.Input;
using Mashhad.Core.Media;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Mashhad.App.Views;

/// <summary>
/// Edit mode ("Studio"): media bin, preview (the native video window sits over <see cref="Preview"/>),
/// transport, inspector (segments, output, FFmpeg command preview, export) and the multi-lane
/// timeline — on floating cards over the same dark wall as the other modes.
/// </summary>
public sealed class EditView : Grid
{
    public const double TopInset = 56, Pad = 10, BinW = 236, InspW = 352, TimelineH = 292;

    public readonly Border Preview = new() { Background = new SolidColorBrush(Colors.Black), CornerRadius = new CornerRadius(14) };
    public readonly EditTimeline Timeline = new();
    public readonly ListView Bin = new() { SelectionMode = ListViewSelectionMode.Single, Padding = new Thickness(0, 0, 0, 8) };
    public readonly TextBlock BinTitle = Ui.Text("", 12, true, 0.75), TimeText = Ui.Time("00:00:00.000", 20, true), DurationText = Ui.Time("", 12, false, 0.5);
    public readonly TextBlock SpeedText = Ui.Text("1×", 13, true), KeyframeStatus = Ui.Text("", 12, false, 0.55);
    public readonly FontIcon PlayGlyph;
    public readonly StackPanel SegmentRows = new() { Spacing = 2 };
    public readonly CheckBox Merge = new(), Snap = new(), KeepAll = new();
    public readonly ComboBox Container = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public readonly TextBlock Summary = Ui.Text("", 12.5, false, 0.65), Command = new() { FontFamily = Fonts.AppFonts.Mono, FontSize = 11, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Foreground = Ui.Solid(255, 0xFF, 0xA8, 0xB8) };
    public readonly Button ExportButton;
    public readonly ToggleButton SnapToggle = new();
    public readonly Border Status;
    public readonly TextBlock StatusText = Ui.Text("", 13);
    public readonly ProgressBar Progress = new() { Width = 160, Maximum = 1, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    public readonly Button StatusCancel, StatusReveal;
    private readonly Border _bin, _inspector, _transport, _timelineCard;

    public event Action<PlayerCommand>? CommandRequested;
    public event Action? ExportClicked, InvertClicked, ClearClicked, ExportFrameClicked, ExportFramesClicked, OptionsChanged, CancelClicked, RevealClicked;
    public event Action<string>? RemuxClicked;
    public event Action<int>? SegmentClicked, SegmentDeleteClicked;

    public EditView()
    {
        // ---- media bin ----
        var binHead = Ui.Row(8, Ui.Icon(Ui.G.Folder, 13), BinTitle);
        binHead.Margin = new Thickness(14, 10, 14, 8);
        var binGrid = new Grid();
        binGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        binGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        binGrid.Children.Add(binHead);
        SetRow(Bin, 1);
        binGrid.Children.Add(Bin);
        _bin = Card(binGrid);

        // ---- transport ----
        var play = Ui.PlayButton(42, (_, _) => CommandRequested?.Invoke(PlayerCommand.PlayPause), out PlayGlyph);
        var speed = new Button { Content = SpeedText, Height = 30, MinWidth = 52, CornerRadius = new CornerRadius(15), Background = Ui.White(0.08), BorderThickness = new Thickness(0), IsTabStop = false };
        speed.Click += (_, _) => CommandRequested?.Invoke(PlayerCommand.SpeedUp);
        var transport = new Grid { Padding = new Thickness(14, 0, 10, 0) };
        transport.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        transport.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        transport.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        TimeText.TextTrimming = TextTrimming.None; // the clock is never cut; the duration gives way first
        TimeText.FontSize = 18;
        var clock = new Grid { ColumnSpacing = 10 };
        clock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        clock.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        clock.Children.Add(TimeText);
        SetColumn(DurationText, 1);
        clock.Children.Add(DurationText);
        transport.Children.Add(clock);
        var center = Ui.Row(2, Btn("◆◂", "Edit.PrevKeyframeTip", PlayerCommand.PreviousKeyframe), Btn(Ui.G.ChevL, "Bar.FrameBack", PlayerCommand.FrameBackStep, icon: true),
            play, Btn(Ui.G.ChevR, "Bar.FrameForward", PlayerCommand.FrameStep, icon: true), Btn("▸◆", "Edit.NextKeyframeTip", PlayerCommand.NextKeyframe));
        SetColumn(center, 1);
        transport.Children.Add(center);
        var right = Ui.Row(2, speed, Btn(Ui.G.Repeat, "Menu.AbLoop", PlayerCommand.AbLoop, icon: true), Btn(Ui.G.Volume, "Bar.Mute", PlayerCommand.Mute, icon: true),
            Btn(Ui.G.Full, "Bar.Fullscreen", PlayerCommand.ToggleFullscreen, icon: true));
        right.HorizontalAlignment = HorizontalAlignment.Right;
        SetColumn(right, 2);
        transport.Children.Add(right);
        _transport = Card(transport);
        _transport.FlowDirection = FlowDirection.LeftToRight;

        // ---- inspector ----
        L.SetText(Merge, "Edit.Merge"); L.SetText(Snap, "Edit.Snap"); L.SetText(KeepAll, "Edit.KeepAll");
        foreach (var c in new[] { Merge, Snap, KeepAll }) c.Click += (_, _) => OptionsChanged?.Invoke();
        foreach (var (key, tag) in new[] { ("Edit.SameContainer", ""), ("MKV", ".mkv"), ("MP4", ".mp4"), ("MOV", ".mov") })
        {
            var item = new ComboBoxItem { Tag = tag };
            if (key.Contains('.')) L.SetText(item, key); else item.Content = key;
            Container.Items.Add(item);
        }
        Container.SelectedIndex = 0;
        Container.SelectionChanged += (_, _) => { if (!_refreshing) OptionsChanged?.Invoke(); };
        ExportButton = Ui.PillButton(Ui.G.Export, "Edit.Export", (_, _) => ExportClicked?.Invoke(), accent: true, height: 40);
        ExportButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        var more = new DropDownButton { Content = Ui.LText("Edit.More", 13.5), HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(17), Height = 34 };
        var moreMenu = new MenuFlyout();
        void MoreItem(string key, Action a) { var i = new MenuFlyoutItem(); L.SetText(i, key); i.Click += (_, _) => a(); moreMenu.Items.Add(i); }
        MoreItem("Edit.ExportFrame", () => ExportFrameClicked?.Invoke());
        MoreItem("Edit.ExportFrames", () => ExportFramesClicked?.Invoke());
        moreMenu.Items.Add(new MenuFlyoutSeparator());
        MoreItem("Edit.RemuxMkv", () => RemuxClicked?.Invoke(".mkv"));
        MoreItem("Edit.RemuxMp4", () => RemuxClicked?.Invoke(".mp4"));
        MoreItem("Edit.RemuxMov", () => RemuxClicked?.Invoke(".mov"));
        more.Flyout = moreMenu;

        var segTools = Ui.Row(6, Ui.PillButton(null, "Edit.Invert", (_, _) => InvertClicked?.Invoke(), height: 30), Ui.PillButton(null, "Edit.Clear", (_, _) => ClearClicked?.Invoke(), height: 30));
        var cmdBox = new Border { Child = Command, Background = Ui.Black(0.35), BorderBrush = Ui.White(0.07), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8) };
        cmdBox.FlowDirection = FlowDirection.LeftToRight;
        var inspector = Ui.Column(6,
            Header("Panel.Segments"), SegmentRows, segTools,
            Header("Edit.Output"), Merge, Snap, KeepAll, Labeled("Edit.Container", Container), Summary,
            Header("Edit.Command"), cmdBox,
            ExportButton, more);
        inspector.Padding = new Thickness(16, 12, 16, 16);
        _inspector = Card(new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        // ---- timeline card ----
        L.SetText(SnapToggle, "Edit.Snap");
        SnapToggle.Height = 30;
        SnapToggle.CornerRadius = new CornerRadius(15);
        SnapToggle.Click += (_, _) => { Snap.IsChecked = SnapToggle.IsChecked; OptionsChanged?.Invoke(); };
        var bar = Ui.Row(6,
            Tool("I", "Edit.SetIn", PlayerCommand.SetIn), Tool("O", "Edit.SetOut", PlayerCommand.SetOut),
            Tool("+", "Edit.Add", PlayerCommand.AddSegment), Tool("B", "Edit.Split", PlayerCommand.SplitSegment),
            Tool("Del", "Edit.Remove", PlayerCommand.RemoveSegment), new AppBarSeparator(),
            Tool("Alt ←", "Edit.PrevKeyframe", PlayerCommand.PreviousKeyframe), Tool("Alt →", "Edit.NextKeyframe", PlayerCommand.NextKeyframe),
            SnapToggle, KeyframeStatus);
        bar.Margin = new Thickness(10, 8, 10, 8);
        var tlGrid = new Grid();
        tlGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tlGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var barScroll = new ScrollViewer { Content = bar, HorizontalScrollMode = ScrollMode.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollMode = ScrollMode.Disabled };
        tlGrid.Children.Add(barScroll);
        SetRow(Timeline, 1);
        tlGrid.Children.Add(Timeline);
        _timelineCard = Card(tlGrid);
        _timelineCard.FlowDirection = FlowDirection.LeftToRight; // time runs left to right in every language

        // ---- status strip (export progress) ----
        StatusCancel = Ui.PillButton(null, "Common.Cancel", (_, _) => CancelClicked?.Invoke(), height: 30);
        StatusReveal = Ui.PillButton(Ui.G.OpenFolder, "Status.ShowInFolder", (_, _) => RevealClicked?.Invoke(), height: 30);
        Status = Ui.Pill(Ui.Row(12, StatusText, Progress, StatusCancel, StatusReveal), radius: 20, padding: new Thickness(16, 5, 6, 5));
        Status.HorizontalAlignment = HorizontalAlignment.Center;
        Status.VerticalAlignment = VerticalAlignment.Bottom;
        Status.Margin = new Thickness(0, 0, 0, TimelineH + 2 * Pad + 66);
        Status.Visibility = Visibility.Collapsed;

        foreach (var c in new UIElement[] { _bin, Preview, _transport, _inspector, _timelineCard, Status }) Children.Add(c);
        SizeChanged += (_, _) => ArrangeFor(ActualWidth, ActualHeight);
    }

    /// <summary>Card offsets for the slide-in transition (0 = in place, 1 = fully out).</summary>
    public void SetEntrance(double outness)
    {
        float o = (float)outness;
        _bin.Translation = new System.Numerics.Vector3(-60 * o, 0, 0);
        _inspector.Translation = new System.Numerics.Vector3(60 * o, 0, 0);
        _timelineCard.Translation = new System.Numerics.Vector3(0, 80 * o, 0);
        _transport.Translation = new System.Numerics.Vector3(0, 30 * o, 0);
        foreach (var c in new UIElement[] { _bin, _inspector, _timelineCard, _transport }) c.Opacity = 1 - outness;
    }

    /// <summary>Positions the cards for a window size (also called before the first layout, for transitions).</summary>
    private bool _refreshing;

    /// <summary>After a language switch: the combo box keeps showing the old text of its selected item.</summary>
    public void RefreshTexts()
    {
        _refreshing = true;
        int i = Container.SelectedIndex;
        Container.SelectedIndex = -1;
        Container.SelectedIndex = i;
        _refreshing = false;
    }

    public void ArrangeFor(double w, double h)
    {
        if (w <= 0 || h <= 0) return;
        double tlTop = h - Pad - TimelineH, upH = Math.Max(120, tlTop - Pad - TopInset);
        double prevX = Pad + BinW + Pad, prevW = Math.Max(160, w - prevX - Pad - InspW - Pad), prevH = Math.Max(90, upH - 56 - 8);
        Place(_bin, Pad, TopInset, BinW, upH);
        Place(Preview, prevX, TopInset, prevW, prevH);
        Place(_transport, prevX, TopInset + prevH + 8, prevW, 56);
        Place(_inspector, w - Pad - InspW, TopInset, InspW, upH);
        Place(_timelineCard, Pad, tlTop, w - 2 * Pad, TimelineH);
    }

    private static void Place(FrameworkElement e, double x, double y, double width, double height)
    {
        e.HorizontalAlignment = HorizontalAlignment.Left;
        e.VerticalAlignment = VerticalAlignment.Top;
        e.Margin = new Thickness(x, y, 0, 0);
        e.Width = Math.Max(0, width);
        e.Height = Math.Max(0, height);
    }

    public void SetSegmentRows(IReadOnlyList<(string Name, string In, string Out, string Dur)> rows, int current)
    {
        SegmentRows.Children.Clear();
        var colors = new[] { Ui.Rose, Ui.Crimson, Ui.Wine };
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var g = new Grid { Padding = new Thickness(8, 5, 4, 5), CornerRadius = new CornerRadius(7), Background = i == current ? Ui.Solid(0x30, 0xE6, 0x22, 0x3F) : new SolidColorBrush(Colors.Transparent) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var swatch = new Microsoft.UI.Xaml.Shapes.Rectangle { Width = 9, Height = 9, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(colors[i % 3]) };
            var name = Ui.Text(r.Name, 13, true);
            var times = Ui.Text($"{r.In} → {r.Out}   {r.Dur}", 12, false, 0.7);
            times.FontFamily = Fonts.AppFonts.Mono;
            times.FlowDirection = FlowDirection.LeftToRight;
            g.Children.Add(Ui.Column(2, Ui.Row(8, swatch, name), times));
            int index = i;
            var del = Ui.IconButton(Ui.G.Delete, "Edit.RemoveTip", 30, 12, (_, _) => SegmentDeleteClicked?.Invoke(index));
            SetColumn(del, 1);
            g.Children.Add(del);
            g.Tapped += (_, _) => SegmentClicked?.Invoke(index);
            SegmentRows.Children.Add(g);
        }
        if (rows.Count == 0) SegmentRows.Children.Add(Ui.LText("Edit.Hint", 12.5, false, 0.6));
        foreach (var t in SegmentRows.Children.OfType<TextBlock>()) t.TextWrapping = TextWrapping.Wrap;
    }

    public void SetBin(IReadOnlyList<MediaItem> items, int current, Func<MediaItem, string> meta)
    {
        Bin.Items.Clear();
        foreach (var m in items)
        {
            var row = Ui.Row(10, Ui.Icon(m.Kind == MediaKind.Video ? Ui.G.Video : Ui.G.Pictures, 15),
                Ui.Column(0, Ui.Text(m.FileName, 12.5), Ui.Text(meta(m), 11.5, false, 0.45)));
            Bin.Items.Add(new ListViewItem { Content = row, Tag = m, Padding = new Thickness(12, 6, 8, 6) });
        }
        Bin.SelectedIndex = current;
    }

    private static Border Card(UIElement child)
    {
        var b = Ui.Pill(child, radius: 14, padding: new Thickness(0));
        b.Background = Ui.Solid(0xC8, 0x16, 0x18, 0x1C);
        return b;
    }

    private static TextBlock Header(string key)
    {
        var t = Ui.LText(key, 11.5, true, 0.5);
        t.Margin = new Thickness(0, 10, 0, 2);
        return t;
    }

    private static Grid Labeled(string key, FrameworkElement control)
    {
        var g = new Grid { ColumnSpacing = 10 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(Ui.LText(key, 13));
        SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    private Button Btn(string glyphOrText, string tip, PlayerCommand cmd, bool icon = false)
    {
        var b = new Button
        {
            Content = icon ? Ui.Icon(glyphOrText, 15) : Ui.Text(glyphOrText, 13, true),
            Width = 38, Height = 38, Padding = new Thickness(0), CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), IsTabStop = false,
        };
        L.SetTip(b, tip);
        b.Click += (_, _) => CommandRequested?.Invoke(cmd);
        return b;
    }

    private Button Tool(string key, string labelKey, PlayerCommand cmd)
    {
        var kbdText = new TextBlock { Text = key, FontSize = 10.5 };
        Fonts.AppFonts.Style(kbdText, true, 10.5);
        var kbd = new Border { Child = kbdText, Background = Ui.White(0.1), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1) };
        var b = new Button
        {
            Content = Ui.Row(7, kbd, Ui.LText(labelKey, 12.5)), Height = 30, Padding = new Thickness(8, 0, 10, 0),
            CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(0), Background = Ui.White(0.05), IsTabStop = false,
        };
        b.Click += (_, _) => CommandRequested?.Invoke(cmd);
        return b;
    }
}
