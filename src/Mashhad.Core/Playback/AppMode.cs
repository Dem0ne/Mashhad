using Mashhad.Core.Media;

namespace Mashhad.Core.Playback;

/// <summary>The three modes of the app: browse a folder, view/play one item, edit a video losslessly.</summary>
public enum AppMode { Folder, Play, Edit }

public static class AppModes
{
    /// <summary>Whether <paramref name="target"/> can be entered with <paramref name="current"/> as the current item.</summary>
    public static bool CanEnter(AppMode target, MediaItem? current) => target switch
    {
        AppMode.Folder => true,
        AppMode.Play => current is not null,
        AppMode.Edit => current is { Kind: MediaKind.Video },
        _ => false,
    };

    /// <summary>Where Escape (or "back") goes from each mode.</summary>
    public static AppMode Back(AppMode mode) => mode == AppMode.Edit ? AppMode.Play : AppMode.Folder;
}
