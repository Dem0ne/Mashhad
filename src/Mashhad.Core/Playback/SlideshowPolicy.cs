using Mashhad.Core.Media;

namespace Mashhad.Core.Playback;

public enum SlideshowAdvance
{
    /// <summary>Show for a fixed time, then advance (still images).</summary>
    AfterDelay,
    /// <summary>Play to the end, then advance (videos, audio).</summary>
    AtEndOfFile,
    /// <summary>Let an animation run for at least the delay, then advance.</summary>
    AfterDelayLooping,
}

/// <summary>Decides how each item of a mixed image/video slideshow advances.</summary>
public sealed class SlideshowPolicy
{
    public TimeSpan ImageDuration { get; set; } = TimeSpan.FromSeconds(5);
    public bool Loop { get; set; } = true;

    public (SlideshowAdvance Mode, TimeSpan Delay) For(MediaKind kind) => kind switch
    {
        MediaKind.Video or MediaKind.Audio => (SlideshowAdvance.AtEndOfFile, TimeSpan.Zero),
        MediaKind.AnimatedImage => (SlideshowAdvance.AfterDelayLooping, ImageDuration),
        _ => (SlideshowAdvance.AfterDelay, ImageDuration),
    };

    /// <summary>The item to show next, or null when the slideshow should stop.</summary>
    public MediaItem? Advance(MediaQueue queue)
    {
        if (queue.Count <= 1) return null;
        bool wrap = queue.Wrap;
        queue.Wrap = Loop;
        try { return queue.Next(); }
        finally { queue.Wrap = wrap; }
    }
}
