namespace ReWASD.Engine.Input;

/// <summary>Resolves the key and mouse names used in profiles to Win32 identifiers.</summary>
public static class KeyMap
{
    public const int WheelNotch = 120;

    private static readonly Dictionary<string, ushort> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Tab"] = 0x09, ["Escape"] = 0x1B, ["Enter"] = 0x0D, ["Return"] = 0x0D,
        ["Back"] = 0x08, ["Backspace"] = 0x08,
        ["ShiftKey"] = 0xA0, ["Shift"] = 0xA0, ["ControlKey"] = 0xA2, ["Ctrl"] = 0xA2,
        ["Alt"] = 0xA4, ["Menu"] = 0xA4,
        ["Up"] = 0x26, ["Down"] = 0x28, ["Left"] = 0x25, ["Right"] = 0x27,
        ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["End"] = 0x23, ["Home"] = 0x24,
        ["Insert"] = 0x2D, ["Delete"] = 0x2E,

        // The right-hand modifiers have names of their own
        ["RightShift"] = 0xA1, ["RShiftKey"] = 0xA1, ["RightCtrl"] = 0xA3, ["RControlKey"] = 0xA3, ["RightAlt"] = 0xA5, ["RMenu"] = 0xA5,

        // Lock, system and Windows keys (Win is Command on a Mac)
        ["CapsLock"] = 0x14, ["Capital"] = 0x14, ["Win"] = 0x5B, ["LWin"] = 0x5B, ["Apps"] = 0x5D,
        ["PrintScreen"] = 0x2C, ["Snapshot"] = 0x2C, ["ScrollLock"] = 0x91, ["Scroll"] = 0x91, ["Pause"] = 0x13,

        // Punctuation, by the key it sits on in a US layout (the Windows OEM names work too)
        ["Backtick"] = 0xC0, ["Oemtilde"] = 0xC0, ["Oem3"] = 0xC0,
        ["Minus"] = 0xBD, ["OemMinus"] = 0xBD,
        ["Equals"] = 0xBB, ["Oemplus"] = 0xBB,
        ["LeftBracket"] = 0xDB, ["OemOpenBrackets"] = 0xDB, ["Oem4"] = 0xDB,
        ["RightBracket"] = 0xDD, ["OemCloseBrackets"] = 0xDD, ["Oem6"] = 0xDD,
        ["Backslash"] = 0xDC, ["OemPipe"] = 0xDC, ["Oem5"] = 0xDC,
        ["Semicolon"] = 0xBA, ["OemSemicolon"] = 0xBA, ["Oem1"] = 0xBA,
        ["Quote"] = 0xDE, ["OemQuotes"] = 0xDE, ["Oem7"] = 0xDE,
        ["Comma"] = 0xBC, ["Oemcomma"] = 0xBC,
        ["Period"] = 0xBE, ["OemPeriod"] = 0xBE,
        ["Slash"] = 0xBF, ["OemQuestion"] = 0xBF, ["Oem2"] = 0xBF
    };

    // Keys whose scan codes carry the E0 prefix
    private static readonly HashSet<ushort> Extended = new() { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0xA3, 0xA5, 0x5B, 0x5C, 0x5D };

    // Keys that are sent by their virtual key rather than a scan code: Pause has no plain scan code of its own (it would come out
    // as Num Lock) and Print Screen's depends on the modifiers held
    private static readonly HashSet<ushort> ByVirtualKey = new() { 0x13, 0x2C };

    public static bool TryGetVirtualKey(string name, out ushort vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();

        if (name.Length == 1)
        {
            char c = char.ToUpperInvariant(name[0]);
            if (c is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                vk = c;
                return true;
            }
        }

        if (name.Length is 2 or 3 && (name[0] is 'F' or 'f') && int.TryParse(name.AsSpan(1), out int f) && f is >= 1 and <= 24)
        {
            vk = (ushort)(0x70 + f - 1);
            return true;
        }

        return Named.TryGetValue(name, out vk);
    }

    // Typed characters on a US layout: the key and whether Shift is needed
    private const string ShiftedDigits = ")!@#$%^&*(";
    private static readonly (char Plain, char Shifted, ushort Vk)[] Punctuation =
    {
        (';', ':', 0xBA), ('=', '+', 0xBB), (',', '<', 0xBC), ('-', '_', 0xBD), ('.', '>', 0xBE),
        ('/', '?', 0xBF), ('`', '~', 0xC0), ('[', '{', 0xDB), ('\\', '|', 0xDC), (']', '}', 0xDD), ('\'', '"', 0xDE)
    };

    /// <summary>The key (and Shift) that types a character on a US keyboard. False for characters it cannot type.</summary>
    public static bool TryGetTypedKey(char c, out ushort vk, out bool shift)
    {
        vk = 0;
        shift = false;

        if (c is >= 'a' and <= 'z') { vk = (ushort)char.ToUpperInvariant(c); return true; }
        if (c is >= 'A' and <= 'Z') { vk = c; shift = true; return true; }
        if (c is >= '0' and <= '9') { vk = c; return true; }
        if (c == ' ') { vk = 0x20; return true; }
        if (c == '\n') { vk = 0x0D; return true; }
        if (c == '\t') { vk = 0x09; return true; }

        int digit = ShiftedDigits.IndexOf(c);
        if (digit >= 0) { vk = (ushort)('0' + digit); shift = true; return true; }

        foreach (var (plain, shifted, key) in Punctuation)
        {
            if (c == plain) { vk = key; return true; }
            if (c == shifted) { vk = key; shift = true; return true; }
        }
        return false;
    }

    public static bool IsExtended(ushort vk) => Extended.Contains(vk);

    /// <summary>True for keys that must be injected by virtual key, not by scan code.</summary>
    public static bool SendsByVirtualKey(ushort vk) => ByVirtualKey.Contains(vk);

    // One canonical name per virtual key, so a captured key always maps back to the name profiles use
    private static readonly string[] CanonicalNamed =
    {
        "Space", "Tab", "Escape", "Enter", "Back", "ShiftKey", "ControlKey", "Alt",
        "Up", "Down", "Left", "Right", "PageUp", "PageDown", "End", "Home", "Insert", "Delete",
        "RightShift", "RightCtrl", "RightAlt", "CapsLock", "Win", "Apps", "PrintScreen", "ScrollLock", "Pause",
        "Backtick", "Minus", "Equals", "LeftBracket", "RightBracket", "Backslash", "Semicolon", "Quote", "Comma", "Period", "Slash"
    };

    private static readonly Dictionary<ushort, string> ReverseNamed =
        CanonicalNamed.ToDictionary(n => Named[n], n => n);

    /// <summary>
    /// Turns a captured virtual key into the profile name for it. The generic Shift, Ctrl and Alt codes are the left-hand keys; the
    /// right-hand ones have their own names. Both Windows keys are "Win".
    /// </summary>
    public static bool TryGetKeyName(ushort vk, out string name)
    {
        if (vk is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
        {
            name = ((char)vk).ToString();
            return true;
        }

        if (vk is >= 0x70 and <= 0x87)
        {
            name = "F" + (vk - 0x6F);
            return true;
        }

        vk = vk switch
        {
            0x10 => 0xA0, // Shift
            0x11 => 0xA2, // Control
            0x12 => 0xA4, // Alt
            0x5C => 0x5B, // right Windows key
            _ => vk
        };

        return ReverseNamed.TryGetValue(vk, out name!);
    }

    /// <summary>The names the UI offers for keys that are not letters, digits or F-keys.</summary>
    public static IReadOnlyList<string> NamedKeys => CanonicalNamed;

    /// <summary>
    /// Resolves a mouse target name. Returns true if recognised; exactly one of
    /// <paramref name="button"/> / <paramref name="wheelDelta"/> is set.
    /// </summary>
    public static bool TryResolveMouse(string name, out MouseButtonKind? button, out int wheelDelta)
    {
        button = null;
        wheelDelta = 0;
        switch ((name ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "left": case "left click": button = MouseButtonKind.Left; return true;
            case "right": case "right click": button = MouseButtonKind.Right; return true;
            case "middle": case "middle click": button = MouseButtonKind.Middle; return true;
            case "xbutton1": case "mouse button 4": button = MouseButtonKind.X1; return true;
            case "xbutton2": case "mouse button 5": button = MouseButtonKind.X2; return true;
            case "wheel up": wheelDelta = WheelNotch; return true;
            case "wheel down": wheelDelta = -WheelNotch; return true;
            default: return false;
        }
    }
}
