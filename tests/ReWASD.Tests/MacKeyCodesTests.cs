using ReWASD.Engine.Input;
using ReWASD.Engine.Platform.MacOS;

namespace ReWASD.Tests;

public class MacKeyCodesTests
{
    [Theory]
    [InlineData('A', 0x00)]
    [InlineData('S', 0x01)]
    [InlineData('W', 0x0D)]
    [InlineData('Z', 0x06)]
    [InlineData('1', 0x12)]
    [InlineData('5', 0x17)]
    [InlineData('0', 0x1D)]
    [InlineData(0x20, 0x31)]   // Space
    [InlineData(0x0D, 0x24)]   // Enter -> Return
    [InlineData(0x1B, 0x35)]   // Escape
    [InlineData(0x08, 0x33)]   // Backspace -> delete
    [InlineData(0x2E, 0x75)]   // Delete -> forward delete
    [InlineData(0x26, 0x7E)]   // Up
    [InlineData(0x70, 0x7A)]   // F1
    [InlineData(0x7B, 0x6F)]   // F12
    [InlineData(0xA0, 0x38)]   // left shift
    [InlineData(0xA2, 0x3B)]   // left control
    [InlineData(0xA4, 0x3A)]   // left alt -> option
    public void KnownKeys_MapToTheirMacKeyCode(int vk, int expected)
    {
        Assert.True(MacKeyCodes.TryGetMacKeyCode((ushort)vk, out var code));
        Assert.Equal(expected, code);
    }

    [Fact]
    public void GenericModifiers_ResolveToTheLeftKey()
    {
        Assert.True(MacKeyCodes.TryGetMacKeyCode(0x10, out var shift));
        Assert.Equal(MacKeyCodes.LeftShift, shift);
        Assert.True(MacKeyCodes.TryGetMacKeyCode(0x11, out var ctrl));
        Assert.Equal(MacKeyCodes.LeftControl, ctrl);
        Assert.True(MacKeyCodes.TryGetMacKeyCode(0x12, out var alt));
        Assert.Equal(MacKeyCodes.LeftOption, alt);
    }

    [Fact]
    public void EveryKeyTheProfilesCanName_CanBeInjectedOnAMac_ExceptF21ToF24()
    {
        var names = new List<string>(KeyMap.NamedKeys);
        names.AddRange("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Select(c => c.ToString()));
        for (int f = 1; f <= 20; f++) names.Add("F" + f);

        foreach (var name in names)
        {
            Assert.True(KeyMap.TryGetVirtualKey(name, out var vk), name);
            Assert.True(MacKeyCodes.TryGetMacKeyCode(vk, out _), name);
        }

        for (int f = 21; f <= 24; f++)
        {
            Assert.True(KeyMap.TryGetVirtualKey("F" + f, out var vk));
            Assert.False(MacKeyCodes.TryGetMacKeyCode(vk, out _), "no Mac keyboard has F" + f);
        }
    }

    [Fact]
    public void CapturedMacKeys_RoundTripToTheNamesProfilesUse()
    {
        // mac key code -> virtual key -> profile name, for a spread of keys
        (ushort Code, string Name)[] cases =
        {
            (0x0D, "W"), (0x00, "A"), (0x12, "1"), (0x31, "Space"), (0x24, "Enter"), (0x33, "Back"),
            (0x75, "Delete"), (0x35, "Escape"), (0x7B, "Left"), (0x7A, "F1"), (0x6F, "F12"),
            (0x38, "ShiftKey"), (0x3C, "ShiftKey"), (0x3B, "ControlKey"), (0x3E, "ControlKey"),
            (0x3A, "Alt"), (0x3D, "Alt")
        };

        foreach (var (code, name) in cases)
        {
            Assert.True(MacKeyCodes.TryGetVirtualKey(code, out var vk), name);
            Assert.True(KeyMap.TryGetKeyName(vk, out var actual), name);
            Assert.Equal(name, actual);
        }
    }

    [Fact]
    public void KeysWithNoEquivalent_AreNotTranslated()
    {
        Assert.False(MacKeyCodes.TryGetVirtualKey(MacKeyCodes.LeftCommand, out _));
        Assert.False(MacKeyCodes.TryGetVirtualKey(MacKeyCodes.CapsLock, out _));
        Assert.False(MacKeyCodes.TryGetVirtualKey(MacKeyCodes.Function, out _));
        Assert.False(MacKeyCodes.TryGetMacKeyCode(0xE7, out _));
    }

    [Fact]
    public void ModifierKeyCodes_AreRecognised()
    {
        foreach (ushort code in new ushort[] { 0x38, 0x3C, 0x3B, 0x3E, 0x3A, 0x3D, 0x37, 0x36, 0x39, 0x3F })
            Assert.True(MacKeyCodes.IsModifier(code), code.ToString("X"));

        Assert.False(MacKeyCodes.IsModifier(0x0D));
        Assert.False(MacKeyCodes.IsModifier(0x31));
    }
}
