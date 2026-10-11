namespace ReWASD.Engine.Platform.MacOS;

/// <summary>
/// Translates between the Windows virtual-key codes the engine uses everywhere (see <see cref="Input.KeyMap"/>)
/// and macOS hardware key codes (the kVK_ANSI_* values). Key codes are positions on an ANSI keyboard, so the
/// profile name "W" always means the key where W sits on a US layout, whatever layout is active.
/// </summary>
public static class MacKeyCodes
{
    public const ushort LeftShift = 0x38, RightShift = 0x3C;
    public const ushort LeftControl = 0x3B, RightControl = 0x3E;
    public const ushort LeftOption = 0x3A, RightOption = 0x3D;
    public const ushort LeftCommand = 0x37, RightCommand = 0x36;
    public const ushort CapsLock = 0x39, Function = 0x3F;

    // Windows VK -> mac key code, for every key KeyMap can produce
    private static readonly Dictionary<ushort, ushort> ToMac = BuildToMac();
    private static readonly Dictionary<ushort, ushort> FromMac = ToMac.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static Dictionary<ushort, ushort> BuildToMac()
    {
        var map = new Dictionary<ushort, ushort>
        {
            // Letters (VK is the upper-case ASCII code)
            ['A'] = 0x00, ['S'] = 0x01, ['D'] = 0x02, ['F'] = 0x03, ['H'] = 0x04, ['G'] = 0x05, ['Z'] = 0x06, ['X'] = 0x07,
            ['C'] = 0x08, ['V'] = 0x09, ['B'] = 0x0B, ['Q'] = 0x0C, ['W'] = 0x0D, ['E'] = 0x0E, ['R'] = 0x0F, ['Y'] = 0x10,
            ['T'] = 0x11, ['O'] = 0x1F, ['U'] = 0x20, ['I'] = 0x22, ['P'] = 0x23, ['L'] = 0x25, ['J'] = 0x26, ['K'] = 0x28,
            ['N'] = 0x2D, ['M'] = 0x2E,

            // Digit row
            ['1'] = 0x12, ['2'] = 0x13, ['3'] = 0x14, ['4'] = 0x15, ['5'] = 0x17, ['6'] = 0x16, ['7'] = 0x1A, ['8'] = 0x1C,
            ['9'] = 0x19, ['0'] = 0x1D,

            [0x20] = 0x31, // Space
            [0x09] = 0x30, // Tab
            [0x1B] = 0x35, // Escape
            [0x0D] = 0x24, // Enter -> Return
            [0x08] = 0x33, // Backspace -> the key Apple labels "delete"
            [0x2E] = 0x75, // Delete -> forward delete
            [0x2D] = 0x72, // Insert -> Help (the Mac keyboard has no Insert)

            // Left/right modifiers; Alt is Option
            [0xA0] = LeftShift, [0xA1] = RightShift,
            [0xA2] = LeftControl, [0xA3] = RightControl,
            [0xA4] = LeftOption, [0xA5] = RightOption,

            [0x25] = 0x7B, [0x26] = 0x7E, [0x27] = 0x7C, [0x28] = 0x7D, // arrows
            [0x21] = 0x74, [0x22] = 0x79, [0x23] = 0x77, [0x24] = 0x73  // PageUp, PageDown, End, Home
        };

        // F1..F20 exist on a Mac keyboard layout; F21..F24 do not
        ushort[] functionKeys = { 0x7A, 0x78, 0x63, 0x76, 0x60, 0x61, 0x62, 0x64, 0x65, 0x6D, 0x67, 0x6F, 0x69, 0x6B, 0x71, 0x6A, 0x40, 0x4F, 0x50, 0x5A };
        for (int i = 0; i < functionKeys.Length; i++) map[(ushort)(0x70 + i)] = functionKeys[i];

        return map;
    }

    /// <summary>The mac key code for a Windows virtual key. Generic Shift/Ctrl/Alt (0x10-0x12) resolve to the left key.</summary>
    public static bool TryGetMacKeyCode(ushort virtualKey, out ushort macKeyCode)
    {
        virtualKey = virtualKey switch { 0x10 => 0xA0, 0x11 => 0xA2, 0x12 => 0xA4, _ => virtualKey };
        return ToMac.TryGetValue(virtualKey, out macKeyCode);
    }

    /// <summary>The Windows virtual key for a mac key code. Command, Caps Lock, Fn and keys with no equivalent return false.</summary>
    public static bool TryGetVirtualKey(ushort macKeyCode, out ushort virtualKey) =>
        FromMac.TryGetValue(macKeyCode, out virtualKey);

    /// <summary>True for the modifier key codes (the events for these arrive as flag changes, not key presses).</summary>
    public static bool IsModifier(ushort macKeyCode) =>
        macKeyCode is LeftShift or RightShift or LeftControl or RightControl or LeftOption or RightOption
            or LeftCommand or RightCommand or CapsLock or Function;
}
