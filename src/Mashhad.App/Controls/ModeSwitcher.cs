using Mashhad.Core.Playback;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Mashhad.App.Controls;

/// <summary>The Folder · Play · Edit pill that appears in every mode.</summary>
public sealed class ModeSwitcher : UserControl
{
    private readonly Dictionary<AppMode, Button> _buttons = new();
    private AppMode _mode = AppMode.Folder;

    public event Action<AppMode>? ModeRequested;

    public ModeSwitcher(bool overVideo)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (mode, key) in new[] { (AppMode.Folder, "Mode.Folder"), (AppMode.Play, "Mode.Play"), (AppMode.Edit, "Mode.Edit") })
        {
            var b = new Button
            {
                Content = Ui.Row(7, Ui.Icon(Ui.ModeGlyph(mode), 13), Ui.LText(key, 13)),
                Height = 30,
                Padding = new Thickness(15, 0, 15, 0),
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                IsTabStop = false,
            };
            var m = mode;
            b.Click += (_, _) => ModeRequested?.Invoke(m);
            _buttons[mode] = b;
            row.Children.Add(b);
        }
        Content = Ui.Pill(row, overVideo, 21);
        SetMode(AppMode.Folder);
    }

    public void SetMode(AppMode mode)
    {
        _mode = mode;
        foreach (var (m, b) in _buttons)
        {
            bool on = m == mode;
            b.Background = on ? Ui.Brand() : new SolidColorBrush(Colors.Transparent);
            b.Foreground = Ui.White(on ? 1 : 0.75);
            if (on) Ui.KeepBackground(b);
            else
            {
                b.Resources.Remove("ButtonBackgroundPointerOver");
                b.Resources.Remove("ButtonBackgroundPressed");
            }
        }
    }

    public void SetAvailable(bool play, bool edit)
    {
        _buttons[AppMode.Play].IsEnabled = play || _mode == AppMode.Play;
        _buttons[AppMode.Edit].IsEnabled = edit || _mode == AppMode.Edit;
    }
}
