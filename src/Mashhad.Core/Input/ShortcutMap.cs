namespace Mashhad.Core.Input;

public enum PlayerCommand
{
    None,
    // Files / app
    OpenFile, OpenFolder, ToggleFullscreen, ExitFullscreen, ToggleInfoPanel, TogglePlaylistPanel,
    ToggleEditMode, ToggleSlideshow, ShowSettings, Quit,
    // Gallery
    NextItem, PreviousItem, FirstItem, LastItem,
    // Playback
    PlayPause, Stop, SeekBack, SeekForward, SeekBackFine, SeekForwardFine, SeekBackLarge, SeekForwardLarge,
    SeekStart, FrameStep, FrameBackStep, SpeedUp, SpeedDown, SpeedReset,
    VolumeUp, VolumeDown, Mute, CycleAudio, CycleSubtitle, ToggleSubtitles, SubDelayPlus, SubDelayMinus,
    AbLoop, ToggleLoopFile, Screenshot, ScreenshotNoSubs,
    // Images
    ZoomIn, ZoomOut, ZoomFit, ZoomActual, RotateCw, RotateCcw, PanLeft, PanRight, PanUp, PanDown,
    ToggleCrop, ApplyCrop,
    // Lossless editing
    SetIn, SetOut, AddSegment, SplitSegment, RemoveSegment, PreviousKeyframe, NextKeyframe,
    JumpIn, JumpOut, Export,
}

/// <summary>Where a binding applies. More specific contexts win over <see cref="Global"/>.</summary>
public enum ShortcutContext
{
    Global,
    Timed,   // video / audio is showing
    Image,   // an image is showing
    Edit,    // lossless edit mode is on
    Crop,    // crop selection is active
}

/// <summary>Keyboard map combining mpv, VLC and LosslessCut conventions.</summary>
public sealed class ShortcutMap
{
    private readonly Dictionary<(ShortcutContext, KeyGesture), PlayerCommand> _map = new();

    public IEnumerable<(ShortcutContext Context, KeyGesture Gesture, PlayerCommand Command)> Bindings =>
        _map.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value));

    public void Bind(ShortcutContext context, string gesture, PlayerCommand command) =>
        _map[(context, KeyGesture.Parse(gesture))] = command;

    /// <summary>
    /// Resolves a key press. <paramref name="active"/> lists the contexts currently in effect,
    /// most specific first; <see cref="ShortcutContext.Global"/> is always consulted last.
    /// </summary>
    public PlayerCommand Resolve(KeyGesture gesture, params ShortcutContext[] active)
    {
        foreach (var ctx in active)
            if (_map.TryGetValue((ctx, gesture), out var cmd)) return cmd;
        return _map.TryGetValue((ShortcutContext.Global, gesture), out var g) ? g : PlayerCommand.None;
    }

    /// <summary>First gesture bound to a command (for tooltips).</summary>
    public string? GestureFor(PlayerCommand command)
    {
        foreach (var kv in _map.OrderBy(k => k.Key.Item1 == ShortcutContext.Global ? 0 : 1)
                     .ThenBy(k => k.Key.Item2.ToString().Length))
            if (kv.Value == command) return kv.Key.Item2.ToString();
        return null;
    }

    public static ShortcutMap CreateDefault()
    {
        var m = new ShortcutMap();
        const ShortcutContext G = ShortcutContext.Global, T = ShortcutContext.Timed, I = ShortcutContext.Image,
            E = ShortcutContext.Edit, C = ShortcutContext.Crop;

        // App
        m.Bind(G, "Ctrl+O", PlayerCommand.OpenFile);
        m.Bind(G, "Ctrl+Shift+O", PlayerCommand.OpenFolder);
        m.Bind(G, "F", PlayerCommand.ToggleFullscreen);
        m.Bind(G, "F11", PlayerCommand.ToggleFullscreen);
        m.Bind(G, "Alt+Enter", PlayerCommand.ToggleFullscreen);
        m.Bind(G, "Escape", PlayerCommand.ExitFullscreen);
        m.Bind(G, "Ctrl+I", PlayerCommand.ToggleInfoPanel);
        m.Bind(G, "Ctrl+L", PlayerCommand.TogglePlaylistPanel);
        m.Bind(G, "Ctrl+E", PlayerCommand.ToggleEditMode);
        m.Bind(G, "F5", PlayerCommand.ToggleSlideshow);
        m.Bind(G, "Ctrl+,", PlayerCommand.ShowSettings);
        m.Bind(G, "Ctrl+Q", PlayerCommand.Quit);

        // Gallery (work for both images and videos)
        m.Bind(G, "PageDown", PlayerCommand.NextItem);
        m.Bind(G, "PageUp", PlayerCommand.PreviousItem);
        m.Bind(G, "N", PlayerCommand.NextItem);
        m.Bind(G, "P", PlayerCommand.PreviousItem);
        m.Bind(G, "MediaNext", PlayerCommand.NextItem);
        m.Bind(G, "MediaPrevious", PlayerCommand.PreviousItem);
        m.Bind(G, "Ctrl+Home", PlayerCommand.FirstItem);
        m.Bind(G, "Ctrl+End", PlayerCommand.LastItem);

        // Playback (mpv / VLC)
        m.Bind(G, "Space", PlayerCommand.PlayPause);
        m.Bind(G, "K", PlayerCommand.PlayPause);
        m.Bind(G, "MediaPlayPause", PlayerCommand.PlayPause);
        m.Bind(G, "MediaStop", PlayerCommand.Stop);
        m.Bind(T, "Left", PlayerCommand.SeekBack);
        m.Bind(T, "Right", PlayerCommand.SeekForward);
        m.Bind(T, "Shift+Left", PlayerCommand.SeekBackFine);
        m.Bind(T, "Shift+Right", PlayerCommand.SeekForwardFine);
        m.Bind(T, "Ctrl+Left", PlayerCommand.SeekBackLarge);
        m.Bind(T, "Ctrl+Right", PlayerCommand.SeekForwardLarge);
        m.Bind(T, "Home", PlayerCommand.SeekStart);
        m.Bind(T, ".", PlayerCommand.FrameStep);
        m.Bind(T, ",", PlayerCommand.FrameBackStep);
        m.Bind(T, "]", PlayerCommand.SpeedUp);
        m.Bind(T, "[", PlayerCommand.SpeedDown);
        m.Bind(T, "Backspace", PlayerCommand.SpeedReset);
        m.Bind(G, "Up", PlayerCommand.VolumeUp);
        m.Bind(G, "Down", PlayerCommand.VolumeDown);
        m.Bind(T, "0", PlayerCommand.VolumeUp);
        m.Bind(T, "9", PlayerCommand.VolumeDown);
        m.Bind(G, "M", PlayerCommand.Mute);
        m.Bind(T, "B", PlayerCommand.CycleAudio);
        m.Bind(T, "J", PlayerCommand.CycleSubtitle);
        m.Bind(T, "V", PlayerCommand.ToggleSubtitles);
        m.Bind(T, "Z", PlayerCommand.SubDelayMinus);
        m.Bind(T, "X", PlayerCommand.SubDelayPlus);
        m.Bind(T, "L", PlayerCommand.AbLoop);
        m.Bind(T, "Shift+L", PlayerCommand.ToggleLoopFile);
        m.Bind(G, "S", PlayerCommand.Screenshot);
        m.Bind(G, "Shift+S", PlayerCommand.ScreenshotNoSubs);

        // Images (Photos-like)
        m.Bind(I, "Left", PlayerCommand.PreviousItem);
        m.Bind(I, "Right", PlayerCommand.NextItem);
        m.Bind(I, "Home", PlayerCommand.FirstItem);
        m.Bind(I, "End", PlayerCommand.LastItem);
        m.Bind(I, "=", PlayerCommand.ZoomIn);
        m.Bind(I, "+", PlayerCommand.ZoomIn);
        m.Bind(I, "-", PlayerCommand.ZoomOut);
        m.Bind(I, "0", PlayerCommand.ZoomFit);
        m.Bind(I, "1", PlayerCommand.ZoomActual);
        m.Bind(I, "R", PlayerCommand.RotateCw);
        m.Bind(I, "Shift+R", PlayerCommand.RotateCcw);
        m.Bind(I, "Ctrl+Left", PlayerCommand.PanLeft);
        m.Bind(I, "Ctrl+Right", PlayerCommand.PanRight);
        m.Bind(I, "Ctrl+Up", PlayerCommand.PanUp);
        m.Bind(I, "Ctrl+Down", PlayerCommand.PanDown);
        m.Bind(I, "C", PlayerCommand.ToggleCrop);
        m.Bind(C, "Enter", PlayerCommand.ApplyCrop);
        m.Bind(C, "Escape", PlayerCommand.ToggleCrop);

        // Lossless editing (LosslessCut)
        m.Bind(E, "I", PlayerCommand.SetIn);
        m.Bind(E, "O", PlayerCommand.SetOut);
        m.Bind(E, "=", PlayerCommand.AddSegment);
        m.Bind(E, "+", PlayerCommand.AddSegment);
        m.Bind(E, "B", PlayerCommand.SplitSegment);
        m.Bind(E, "Delete", PlayerCommand.RemoveSegment);
        m.Bind(E, "Alt+Left", PlayerCommand.PreviousKeyframe);
        m.Bind(E, "Alt+Right", PlayerCommand.NextKeyframe);
        m.Bind(E, "Shift+I", PlayerCommand.JumpIn);
        m.Bind(E, "Shift+O", PlayerCommand.JumpOut);
        m.Bind(E, "Ctrl+Enter", PlayerCommand.Export);
        return m;
    }
}
