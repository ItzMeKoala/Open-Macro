using ReWASD.Engine.Input;

namespace ReWASD.Tests;

/// <summary>The hook state machine shared by every platform: swallowing, release tracking, wheel decay and the panic key.</summary>
public class InputHookCoreTests
{
    private sealed class Harness
    {
        public long Now;
        public HashSet<string> SuppressedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SuppressedMouse { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Keys { get; } = new();
        public List<string> Mouse { get; } = new();
        public int Panics;
        public readonly ManualResetEventSlim PanicSeen = new(false);
        public InputHookCore Core { get; }

        public Harness()
        {
            Core = new InputHookCore(
                k => SuppressedKeys.Contains(k), m => SuppressedMouse.Contains(m),
                () => { Interlocked.Increment(ref Panics); PanicSeen.Set(); },
                (n, d) => Keys.Add($"{n}:{(d ? "down" : "up")}"),
                (n, d) => Mouse.Add($"{n}:{(d ? "down" : "up")}"),
                () => Now);
        }
    }

    private const ushort W = 0x57, F12 = 0x7B, LShift = 0xA0, LCtrl = 0xA2, LAlt = 0xA4;

    [Fact]
    public void UnmappedKey_IsTrackedButNotSwallowed()
    {
        var h = new Harness();

        Assert.False(h.Core.HandleKey(W, true));
        Assert.Contains("W", h.Core.Snapshot().Keys);
        Assert.False(h.Core.HandleKey(W, false));
        Assert.DoesNotContain("W", h.Core.Snapshot().Keys);
        Assert.Equal(new[] { "W:down", "W:up" }, h.Keys);
    }

    [Fact]
    public void MappedKey_IsSwallowed_IncludingRepeatAndRelease()
    {
        var h = new Harness();
        h.SuppressedKeys.Add("W");

        Assert.True(h.Core.HandleKey(W, true));
        Assert.True(h.Core.HandleKey(W, true));   // auto-repeat
        h.SuppressedKeys.Clear();                 // mapping removed while the key is still down
        Assert.True(h.Core.HandleKey(W, true));   // still swallowed, so the app never sees half a press
        Assert.True(h.Core.HandleKey(W, false));  // and its release is swallowed too
        Assert.False(h.Core.HandleKey(W, false)); // but a stray second release is not
    }

    [Fact]
    public void UnknownKey_IsIgnored()
    {
        var h = new Harness();

        Assert.False(h.Core.HandleKey(0xE7, true));
        Assert.Empty(h.Keys);
        Assert.Empty(h.Core.Snapshot().Keys);
    }

    [Fact]
    public void MappedMouseButton_IsSwallowedUntilReleased()
    {
        var h = new Harness();
        h.SuppressedMouse.Add("Left Click");

        Assert.True(h.Core.HandleMouseButton("Left Click", true));
        Assert.True(h.Core.IsMouseSwallowed("Left Click"));
        Assert.Contains("Left Click", h.Core.Snapshot().Mouse);
        Assert.True(h.Core.HandleMouseButton("Left Click", false));
        Assert.False(h.Core.IsMouseSwallowed("Left Click"));
        Assert.Empty(h.Core.Snapshot().Mouse);
    }

    [Fact]
    public void UnmappedMouseButton_PassesThrough()
    {
        var h = new Harness();

        Assert.False(h.Core.HandleMouseButton("Right Click", true));
        Assert.False(h.Core.IsMouseSwallowed("Right Click"));
        Assert.False(h.Core.HandleMouseButton("Right Click", false));
    }

    [Fact]
    public void MappedWheel_ReadsAsPressedFor60ms_ThenExpires()
    {
        var h = new Harness();
        h.SuppressedMouse.Add("Wheel Up");

        Assert.True(h.Core.HandleWheel("Wheel Up"));
        Assert.Contains("Wheel Up", h.Core.Snapshot().Mouse);

        h.Now = 59;
        Assert.Contains("Wheel Up", h.Core.Snapshot().Mouse);
        h.Now = 60;
        Assert.DoesNotContain("Wheel Up", h.Core.Snapshot().Mouse);
    }

    [Fact]
    public void UnmappedWheel_PassesThroughButIsReported()
    {
        var h = new Harness();

        Assert.False(h.Core.HandleWheel("Wheel Down"));
        Assert.Equal(new[] { "Wheel Down:down" }, h.Mouse);
        Assert.Empty(h.Core.Snapshot().Mouse);
    }

    [Fact]
    public void PanicCombo_RaisesOnce_AndIsNeverPassedOnOrReported()
    {
        var h = new Harness();
        h.SuppressedKeys.Add("F12");

        h.Core.HandleKey(LCtrl, true);
        h.Core.HandleKey(LAlt, true);
        h.Core.HandleKey(LShift, true);
        h.Keys.Clear();

        Assert.True(h.Core.HandleKey(F12, true));
        Assert.True(h.PanicSeen.Wait(2000));
        Assert.True(h.Core.HandleKey(F12, false));
        Assert.Equal(1, h.Panics);
        Assert.Empty(h.Keys);
        Assert.DoesNotContain("F12", h.Core.Snapshot().Keys);
    }

    [Fact]
    public void F12WithoutAllThreeModifiers_IsAnOrdinaryKey()
    {
        var h = new Harness();
        h.Core.HandleKey(LCtrl, true);
        h.Core.HandleKey(LAlt, true);

        Assert.False(h.Core.HandleKey(F12, true));
        Assert.Contains("F12", h.Core.Snapshot().Keys);
        Assert.Equal(0, h.Panics);
    }

    [Fact]
    public void PanicModifiersAreTrackedFromEitherSide()
    {
        var h = new Harness();
        h.Core.HandleKey(0xA3, true); // right ctrl
        h.Core.HandleKey(0xA5, true); // right alt
        h.Core.HandleKey(0xA1, true); // right shift

        Assert.True(h.Core.HandleKey(F12, true));
        Assert.True(h.PanicSeen.Wait(2000));
    }

    [Fact]
    public void Reset_ForgetsEverything()
    {
        var h = new Harness();
        h.SuppressedKeys.Add("W");
        h.Core.HandleKey(W, true);
        h.Core.HandleKey(LCtrl, true);

        h.Core.Reset();

        Assert.Empty(h.Core.Snapshot().Keys);
        Assert.False(h.Core.HandleKey(W, false)); // no longer owed a swallowed release
        h.Core.HandleKey(LAlt, true);
        h.Core.HandleKey(LShift, true);
        Assert.False(h.Core.HandleKey(F12, true)); // ctrl was forgotten, so this is not the panic combo
    }
}
