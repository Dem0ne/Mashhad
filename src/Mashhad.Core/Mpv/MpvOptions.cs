using System.Globalization;
using Mashhad.Core.Settings;

namespace Mashhad.Core.Mpv;

/// <summary>The libmpv configuration Mashhad runs with. Pure data, so it is unit-testable.</summary>
public static class MpvOptions
{
    /// <summary>Options every supported libmpv understands.</summary>
    public static List<KeyValuePair<string, string>> Build(AppSettings settings, long? windowHandle, string screenshotDirectory)
    {
        var o = new List<KeyValuePair<string, string>>();
        void Add(string k, string v) => o.Add(new(k, v));

        Add("config", "no");                 // never read the user's mpv.conf: behaviour must be ours
        Add("terminal", "no");
        Add("idle", "yes");
        Add("keep-open", "yes");             // stay on the last frame; we decide what happens at EOF
        Add("osc", "no");                    // the WinUI chrome replaces mpv's on-screen controller
        Add("input-default-bindings", "no"); // all input is routed through ShortcutMap
        Add("input-vo-keyboard", "no");
        Add("osd-bar", "no");
        Add("osd-font-size", "30");
        Add("osd-duration", "1200");

        // Low-resource defaults: zero-copy hardware decoding, modest demuxer cache.
        Add("hwdec", string.IsNullOrWhiteSpace(settings.HardwareDecoding) ? "d3d11va" : settings.HardwareDecoding);
        Add("demuxer-max-bytes", "64MiB");
        Add("demuxer-max-back-bytes", "24MiB");

        // Images are first-class: show them until the user moves on.
        Add("image-display-duration", "inf");

        Add("volume", settings.Volume.ToString("0.#", CultureInfo.InvariantCulture));
        Add("volume-max", "150");
        Add("mute", settings.Muted ? "yes" : "no");
        Add("sub-auto", "fuzzy");
        Add("audio-file-auto", "no");
        Add("screenshot-directory", screenshotDirectory);
        Add("screenshot-template", "%F-%wH.%wM.%wS.%wT");
        Add("screenshot-format", settings.ScreenshotFormat);
        if (!string.IsNullOrWhiteSpace(settings.PreferredAudioLanguages)) Add("alang", settings.PreferredAudioLanguages);
        if (!string.IsNullOrWhiteSpace(settings.PreferredSubtitleLanguages)) Add("slang", settings.PreferredSubtitleLanguages);

        if (windowHandle is long wid)
        {
            Add("wid", wid.ToString(CultureInfo.InvariantCulture));
            Add("force-window", "yes");
        }
        return o;
    }

    /// <summary>Options that only exist on some platforms/versions; failures to set them are ignored.</summary>
    public static List<KeyValuePair<string, string>> BuildOptional(bool embedded)
    {
        var o = new List<KeyValuePair<string, string>>();
        if (embedded)
        {
            o.Add(new("gpu-context", "d3d11"));
            o.Add(new("gpu-api", "d3d11"));
            o.Add(new("drag-and-drop", "no"));   // drops are handled by the host window
            o.Add(new("input-cursor", "no"));
            o.Add(new("cursor-autohide", "no"));
            o.Add(new("background", "color"));
            o.Add(new("background-color", "#000000"));
        }
        return o;
    }
}
