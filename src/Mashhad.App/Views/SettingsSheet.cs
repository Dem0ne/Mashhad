using Mashhad.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Mashhad.App.Views;

/// <summary>Settings as a floating card on the right of the folder view.</summary>
public sealed class SettingsSheet : UserControl
{
    public readonly ComboBox LanguageBox = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public readonly ToggleSwitch Hwdec = new(), Resume = new(), Wrap = new(), SingleInstance = new(), SlideshowLoop = new(), SlideshowShuffle = new();
    public readonly NumberBox SlideshowSeconds = new() { Minimum = 1, Maximum = 3600, SmallChange = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
    public readonly ComboBox ScreenshotFormat = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public readonly TextBlock Folders = Ui.Text("", 12, false, 0.65), About = Ui.Text("", 12, false, 0.65);
    public event Action? Changed, ChooseScreenshotFolder, ChooseExportFolder, ChooseFfmpegFolder, DefaultApps, CloseRequested;
    private bool _loading;

    public SettingsSheet()
    {
        foreach (var (tag, key, text) in new[] { ("system", "Settings.SystemDefault", ""), ("en", "", "English"), ("ar", "", "العربية") })
        {
            var i = new ComboBoxItem { Tag = tag };
            if (key.Length > 0) L.SetText(i, key); else i.Content = text;
            LanguageBox.Items.Add(i);
        }
        foreach (var (tag, text) in new[] { ("png", "PNG"), ("jpg", "JPEG"), ("webp", "WebP") }) ScreenshotFormat.Items.Add(new ComboBoxItem { Tag = tag, Content = text });
        L.SetText(LanguageBox, "Settings.Language");
        L.SetText(ScreenshotFormat, "Settings.ScreenshotFormat");
        L.SetText(SlideshowSeconds, "Settings.SlideshowSeconds");
        foreach (var (t, k) in new[] { (Hwdec, "Settings.Hwdec"), (Resume, "Settings.Resume"), (Wrap, "Settings.Wrap"), (SingleInstance, "Settings.SingleInstance"), (SlideshowLoop, "Settings.SlideshowLoop"), (SlideshowShuffle, "Settings.SlideshowShuffle") })
        {
            L.SetText(t, k);
            t.Toggled += (_, _) => { if (!_loading) Changed?.Invoke(); };
        }
        LanguageBox.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(); };
        ScreenshotFormat.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(); };
        SlideshowSeconds.ValueChanged += (_, _) => { if (!_loading) Changed?.Invoke(); };
        Folders.TextWrapping = About.TextWrapping = TextWrapping.Wrap;
        Folders.IsTextSelectionEnabled = About.IsTextSelectionEnabled = true;

        Button Wide(string key, Action a) { var b = Ui.PillButton(null, key, (_, _) => a()); b.HorizontalAlignment = HorizontalAlignment.Stretch; b.Background = Ui.White(0.06); return b; }
        var head = new Grid();
        head.Children.Add(Ui.Row(8, Ui.Icon(Ui.G.Settings, 15), Ui.LText("Panel.Settings", 16, true)));
        var close = Ui.IconButton(Ui.G.Close, "Panel.Close", 32, 11, (_, _) => CloseRequested?.Invoke());
        close.HorizontalAlignment = HorizontalAlignment.Right;
        head.Children.Add(close);
        var body = Ui.Column(12, head, LanguageBox, Hwdec, Resume, Wrap, SingleInstance, SlideshowSeconds, SlideshowLoop, SlideshowShuffle, ScreenshotFormat,
            Ui.LText("Settings.Folders", 13.5, true), Wide("Settings.ScreenshotFolder", () => ChooseScreenshotFolder?.Invoke()),
            Wide("Settings.ExportFolder", () => ChooseExportFolder?.Invoke()), Wide("Settings.FfmpegFolder", () => ChooseFfmpegFolder?.Invoke()), Folders,
            Wide("Settings.DefaultApp", () => DefaultApps?.Invoke()), About);
        body.Padding = new Thickness(18, 14, 18, 18);
        Content = new Border
        {
            Child = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Background = Ui.Card(),
            BorderBrush = Ui.White(0.09),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
        };
        Width = 340;
        HorizontalAlignment = HorizontalAlignment.Right;
        Margin = new Thickness(0, 56, 12, 12);
        Visibility = Visibility.Collapsed;
    }

    public void Load(AppSettings s)
    {
        _loading = true;
        Select(LanguageBox, s.Language);
        Select(ScreenshotFormat, s.ScreenshotFormat);
        Hwdec.IsOn = s.HardwareDecoding != "no";
        Resume.IsOn = s.ResumePlayback;
        Wrap.IsOn = s.WrapGallery;
        SingleInstance.IsOn = s.SingleInstance;
        SlideshowSeconds.Value = s.SlideshowSeconds;
        SlideshowLoop.IsOn = s.SlideshowLoop;
        SlideshowShuffle.IsOn = s.SlideshowShuffle;
        _loading = false;
    }

    public void Save(AppSettings s)
    {
        s.Language = TagOf(LanguageBox, "system");
        s.ScreenshotFormat = TagOf(ScreenshotFormat, "png");
        s.HardwareDecoding = Hwdec.IsOn ? "d3d11va" : "no";
        s.ResumePlayback = Resume.IsOn;
        s.WrapGallery = Wrap.IsOn;
        s.SingleInstance = SingleInstance.IsOn;
        if (!double.IsNaN(SlideshowSeconds.Value)) s.SlideshowSeconds = SlideshowSeconds.Value;
        s.SlideshowLoop = SlideshowLoop.IsOn;
        s.SlideshowShuffle = SlideshowShuffle.IsOn;
        SettingsStore.Sanitize(s);
    }

    private static void Select(ComboBox box, string tag)
    {
        foreach (var o in box.Items)
            if (o is ComboBoxItem { Tag: string t } i && t == tag) { box.SelectedItem = i; return; }
        box.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox box, string fallback) => box.SelectedItem is ComboBoxItem { Tag: string t } ? t : fallback;
}
