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
        ["Insert"] = 0x2D, ["Delete"] = 0x2E
    };

    // Keys whose scan codes carry the E0 prefix
    private static readonly HashSet<ushort> Extended = new() { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E };

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

        if (name.Length is 2 or 3 && (name[0] is 'F' or 'f') && int.TryParse(name.AsSpan(1), out int f) && f is >= 1 and <= 12)
        {
            vk = (ushort)(0x70 + f - 1);
            return true;
        }

        return Named.TryGetValue(name, out vk);
    }

    public static bool IsExtended(ushort vk) => Extended.Contains(vk);

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
