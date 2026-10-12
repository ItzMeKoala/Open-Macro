namespace ReWASD.UI;

/// <summary>
/// Turns a key the window itself received (by its name in the toolkit's key list, which WPF and Avalonia share) into the profile name
/// of that key on the keyboard drawing, so the drawing can follow the real keyboard while ReWASD is the active window. Keys the drawing
/// does not have give null. The left-hand Shift, Ctrl and Alt are the ones drawn; their right-hand twins give null.
/// </summary>
public static class WindowKeys
{
    public static string? ToSourceName(string uiKey)
    {
        if (uiKey.Length == 1 && uiKey[0] is >= 'A' and <= 'Z') return uiKey;
        if (uiKey.Length == 2 && uiKey[0] == 'D' && char.IsAsciiDigit(uiKey[1])) return uiKey[1].ToString();
        if (uiKey.Length is 2 or 3 && uiKey[0] == 'F' && int.TryParse(uiKey.AsSpan(1), out int f) && f is >= 1 and <= 12) return uiKey;

        return uiKey switch
        {
            "Escape" => "Escape",
            "Tab" => "Tab",
            "Enter" or "Return" => "Enter",
            "Back" => "Back",
            "Space" => "Space",
            "LeftShift" => "ShiftKey",
            "LeftCtrl" => "ControlKey",
            "LeftAlt" => "Alt",
            "Up" or "Down" or "Left" or "Right" => uiKey,
            "PageUp" or "Prior" => "PageUp",
            "PageDown" or "Next" => "PageDown",
            "Home" or "End" or "Insert" or "Delete" => uiKey,
            _ => null
        };
    }
}
