using ReWASD.Engine.Input;

namespace ReWASD.Tests;

public class HookTests
{
    [Theory]
    [InlineData(0x57, "W")]
    [InlineData(0x31, "1")]
    [InlineData(0x20, "Space")]
    [InlineData(0x0D, "Enter")]
    [InlineData(0x08, "Back")]
    [InlineData(0x7C, "F13")]
    [InlineData(0x70, "F1")]
    [InlineData(0x87, "F24")]
    [InlineData(0xA0, "ShiftKey")]
    [InlineData(0x10, "ShiftKey")]     // the generic Shift is the left one
    [InlineData(0xA1, "RightShift")]   // the right-hand modifiers have names of their own
    [InlineData(0x11, "ControlKey")]
    [InlineData(0xA3, "RightCtrl")]
    [InlineData(0xA5, "RightAlt")]
    [InlineData(0x5B, "Win")]
    [InlineData(0x5C, "Win")]          // both Windows keys
    [InlineData(0x14, "CapsLock")]
    [InlineData(0x5D, "Apps")]
    [InlineData(0x2C, "PrintScreen")]
    [InlineData(0x91, "ScrollLock")]
    [InlineData(0x13, "Pause")]
    [InlineData(0xBA, "Semicolon")]
    [InlineData(0xDE, "Quote")]
    [InlineData(0xBC, "Comma")]
    [InlineData(0xBE, "Period")]
    [InlineData(0xBF, "Slash")]
    [InlineData(0xC0, "Backtick")]
    [InlineData(0xBD, "Minus")]
    [InlineData(0xBB, "Equals")]
    [InlineData(0xDB, "LeftBracket")]
    [InlineData(0xDD, "RightBracket")]
    [InlineData(0xDC, "Backslash")]
    [InlineData(0x26, "Up")]
    public void CapturedKeys_MapToProfileNames(int vk, string expected)
    {
        Assert.True(KeyMap.TryGetKeyName((ushort)vk, out var name));
        Assert.Equal(expected, name);
    }

    [Fact]
    public void EveryCanonicalName_RoundTripsThroughBothDirections()
    {
        foreach (var name in KeyMap.NamedKeys)
        {
            Assert.True(KeyMap.TryGetVirtualKey(name, out var vk), name);
            Assert.True(KeyMap.TryGetKeyName(vk, out var back), name);
            Assert.True(KeyMap.TryGetVirtualKey(back, out var again), back);
            Assert.Equal(vk, again);
        }

        for (int f = 1; f <= 24; f++)
        {
            Assert.True(KeyMap.TryGetVirtualKey("F" + f, out var vk), "F" + f);
            Assert.True(KeyMap.TryGetKeyName(vk, out var back));
            Assert.Equal("F" + f, back);
        }
    }

    [Theory]
    [InlineData("OemSemicolon", 0xBA)]
    [InlineData("Oem7", 0xDE)]
    [InlineData("RShiftKey", 0xA1)]
    [InlineData("LWin", 0x5B)]
    [InlineData("Capital", 0x14)]
    [InlineData("Snapshot", 0x2C)]
    public void WindowsKeyNames_AreUnderstoodToo(string name, int vk)
    {
        Assert.True(KeyMap.TryGetVirtualKey(name, out var actual));
        Assert.Equal(vk, actual);
    }

    [Fact]
    public void PauseAndPrintScreen_AreSentByVirtualKey_AndTheRightHandModifiersAsExtendedKeys()
    {
        Assert.True(KeyMap.SendsByVirtualKey(0x13));
        Assert.True(KeyMap.SendsByVirtualKey(0x2C));
        Assert.False(KeyMap.SendsByVirtualKey(0x41));
        foreach (ushort vk in new ushort[] { 0xA3, 0xA5, 0x5B, 0x5D }) Assert.True(KeyMap.IsExtended(vk), vk.ToString("X"));
        Assert.False(KeyMap.IsExtended(0xA1));      // right Shift is not an extended key
        Assert.False(KeyMap.IsExtended(0xBA));
    }

    [Fact]
    public void UnknownKeys_AreNotNamed()
    {
        Assert.False(KeyMap.TryGetKeyName(0xE7, out _));   // VK_PACKET
        Assert.False(KeyMap.TryGetKeyName(0x00, out _));
    }

    [Fact]
    public void MouseNames_AllResolve()
    {
        foreach (var name in LowLevelInputHook.MouseNames)
        {
            Assert.True(KeyMap.TryResolveMouse(name, out _, out _), name);
        }
    }

    // Uses F13, a key no application binds, and swallows it, so running this has no visible effect.
    [WindowsFact]
    public void Hook_CapturesAndSwallowsMappedKey_AndTracksRelease()
    {
        using var hook = new LowLevelInputHook(name => name == "F13", _ => false) { IncludeInjected = true };
        if (!StartOrSkip(hook)) return;

        var backend = new Win32InputBackend();
        const ushort f13 = 0x7C;
        try
        {
            backend.KeyDown(f13);
            Assert.True(WaitFor(() => hook.Snapshot().Keys.Contains("F13")), "hook never saw the key press");

            backend.KeyUp(f13);
            Assert.True(WaitFor(() => !hook.Snapshot().Keys.Contains("F13")), "release was not tracked");
        }
        finally
        {
            backend.KeyUp(f13);
        }
    }

    [WindowsFact]
    public void Hook_IgnoresInjectedInput_ByDefault()
    {
        using var hook = new LowLevelInputHook(_ => true, _ => true); // would swallow everything it saw
        if (!StartOrSkip(hook)) return;

        var backend = new Win32InputBackend();
        const ushort f14 = 0x7D;
        try
        {
            backend.KeyDown(f14);
            Thread.Sleep(300);
            Assert.DoesNotContain("F14", hook.Snapshot().Keys); // our own injected events are never read back in
        }
        finally
        {
            backend.KeyUp(f14);
        }
    }

    // Hooks need an interactive desktop. CI (no desktop) skips; a developer machine must be able to install them.
    private static bool StartOrSkip(LowLevelInputHook hook)
    {
        if (hook.Start()) return true;
        Assert.True(Environment.GetEnvironmentVariable("CI") != null, "the keyboard/mouse hook could not be installed on this machine");
        return false;
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }
        return condition();
    }
}
