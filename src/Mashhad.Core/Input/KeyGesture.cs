namespace Mashhad.Core.Input;

/// <summary>A key plus modifiers, with platform-neutral key names ("Space", "Left", "A", "[", "F11").</summary>
public readonly record struct KeyGesture(string Key, bool Ctrl = false, bool Shift = false, bool Alt = false)
{
    public static KeyGesture Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Empty key gesture.");
        bool ctrl = false, shift = false, alt = false;
        string key = "";
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            bool last = i == parts.Length - 1;
            if (!last && p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) ctrl = true;
            else if (!last && p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else if (!last && p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            else key = p.Length == 0 ? "+" : p; // "Ctrl++" => key "+"
        }
        if (key.Length == 0) throw new FormatException($"No key in gesture '{text}'.");
        return new KeyGesture(Normalize(key), ctrl, shift, alt);
    }

    public static string Normalize(string key) => key.Length == 1 ? key.ToUpperInvariant() : key;

    public override string ToString() =>
        (Ctrl ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + Key;
}

public static class KeyNames
{
    /// <summary>Maps a Windows virtual-key code to the neutral key name used by <see cref="ShortcutMap"/>.</summary>
    public static string? FromVirtualKey(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),                 // 0-9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),                 // A-Z
        >= 0x60 and <= 0x69 => ((char)('0' + vk - 0x60)).ToString(),  // numpad digits
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),                     // F1-F24
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x1B => "Escape",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2D => "Insert",
        0x2E => "Delete",
        0x6A => "*",
        0x6B => "+",
        0x6D => "-",
        0x6E => ".",
        0x6F => "/",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        0xB3 => "MediaPlayPause",
        0xB0 => "MediaNext",
        0xB1 => "MediaPrevious",
        0xB2 => "MediaStop",
        _ => null,
    };
}
