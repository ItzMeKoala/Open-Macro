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
    [InlineData(0xA1, "ShiftKey")]   // right shift shares the name
    [InlineData(0x11, "ControlKey")]
    [InlineData(0xA5, "Alt")]
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
    [Fact]
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

    [Fact]
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
