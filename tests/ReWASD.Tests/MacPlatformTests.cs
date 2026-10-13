using System.Runtime.Versioning;
using ReWASD.Engine.Platform;
using ReWASD.Engine.Platform.MacOS;

namespace ReWASD.Tests;

/// <summary>Checks of the macOS backends. Skipped on other systems; the input-injection checks also need the macOS permissions.</summary>
[SupportedOSPlatform("macos")]
public class MacPlatformTests
{
    [MacFact]
    public void MacBackends_AreCreatedWithoutAVirtualPad()
    {
        using var backends = PlatformServices.Create();

        Assert.False(backends.VirtualPad.IsDriverAvailable);
        Assert.IsType<MacGameController>(backends.Reader);
        Assert.IsType<MacInputBackend>(backends.InputBackend);
        using var hook = backends.CreateHook(_ => false, _ => false);
        Assert.IsType<MacInputHook>(hook);
        Assert.IsType<MacRumbleOutput>(backends.Rumble);
        Assert.True(backends.Info.SupportsRumble);
        backends.Rumble.Rumble(1, 1); // no controller connected: accepted and ignored
        backends.Rumble.Rumble(0, 0);
    }

    [MacFact]
    public void MacRumble_BuildsAHapticPatternWithoutAnyHardware()
    {
        Assert.True(ObjC.LoadFramework("Foundation") && ObjC.LoadFramework("CoreHaptics"));
        var pool = ObjC.objc_autoreleasePoolPush();
        try
        {
            // A wrong message signature here would crash the process, so this proves the calls are shaped right
            Assert.NotEqual(IntPtr.Zero, MacRumbleOutput.BuildPattern(1.0));
            Assert.NotEqual(IntPtr.Zero, MacRumbleOutput.BuildPattern(0.3));
        }
        finally
        {
            ObjC.objc_autoreleasePoolPop(pool);
        }
    }

    [MacFact]
    public void MacRumble_WithoutAControllerNeverCrashes_AndCanBeDisposedRepeatedly()
    {
        var reader = new MacGameController();
        var rumble = new MacRumbleOutput(reader);
        for (int i = 0; i < 100; i++)
        {
            rumble.Rumble(i % 2, 1 - i % 2);
            rumble.Rumble(double.NaN, 5);       // nonsense is clamped, not passed on
        }
        rumble.Rumble(0, 0);
        rumble.Dispose();
        rumble.Dispose();
        rumble.Rumble(1, 1);                    // after disposal still harmless
    }

    [MacFact]
    public void MacGameController_ReadsAndListsWithoutAControllerOrACrash()
    {
        var reader = new MacGameController();

        Assert.True(reader.IsAvailable, "the GameController framework should load on any supported Mac");
        for (int i = 0; i < 200; i++)
        {
            _ = reader.Read();                // many polls: the autorelease pool must keep this from leaking or crashing
            _ = reader.ConnectedSlots();
        }
        Assert.Equal(-1, reader.ExcludedUserIndex);
        Assert.Equal("Controller 1", reader.DescribeSlot(0)); // no controller there: a neutral name, not an exception
    }

    [MacFact]
    public void MacHook_ReportsWhyItCouldNotStart_WhenPermissionIsMissing()
    {
        using var hook = new MacInputHook(_ => false, _ => false);

        if (hook.Start())
        {
            Assert.True(hook.IsInstalled);
            Assert.Null(hook.FailureReason);
            hook.Stop();
            Assert.False(hook.IsInstalled);
            return;
        }

        Assert.False(hook.IsInstalled);
        Assert.False(string.IsNullOrWhiteSpace(hook.FailureReason));
    }

    [MacFact]
    public void MacHook_SeesAnInjectedKey_WhenAllowed_AndSwallowsIt()
    {
        if (!MacPermissions.AllGranted) return; // needs Accessibility and Input Monitoring; skipped where they are not granted

        // F13 is a key no application binds, so injecting it has no visible effect
        using var hook = new MacInputHook(name => name == "F13", _ => false) { IncludeInjected = true };
        if (!hook.Start()) return;

        using var backend = new MacInputBackend();
        const ushort f13 = 0x7C;
        try
        {
            backend.KeyDown(f13);
            Assert.True(WaitFor(() => hook.Snapshot().Keys.Contains("F13")), "the tap never saw the key press");
            backend.KeyUp(f13);
            Assert.True(WaitFor(() => !hook.Snapshot().Keys.Contains("F13")), "the release was not tracked");
        }
        finally
        {
            backend.KeyUp(f13);
        }
    }

    [MacFact]
    public void MacHook_IgnoresItsOwnInjectedInput_ByDefault()
    {
        if (!MacPermissions.AllGranted) return;

        using var hook = new MacInputHook(_ => true, _ => true); // would swallow everything it saw
        if (!hook.Start()) return;

        using var backend = new MacInputBackend();
        const ushort f14 = 0x7D;
        try
        {
            backend.KeyDown(f14);
            Thread.Sleep(300);
            Assert.DoesNotContain("F14", hook.Snapshot().Keys);
        }
        finally
        {
            backend.KeyUp(f14);
        }
    }

    [MacFact]
    public void MacBackend_MovesThePointer_AndBack()
    {
        if (!MacPermissions.CanControlInput) return;

        using var backend = new MacInputBackend();

        // A person using the machine at the same time can nudge the pointer mid-test, so allow a few tries
        bool moved = false;
        for (int attempt = 0; attempt < 4 && !moved; attempt++)
        {
            var before = Position();
            try
            {
                backend.MouseMove(12, 7);
                moved = WaitFor(() => Math.Abs(Position().X - (before.X + 12)) < 1.5 && Math.Abs(Position().Y - (before.Y + 7)) < 1.5, 800);
            }
            finally
            {
                backend.MouseMove(-12, -7);
            }
        }

        Assert.True(moved, "the pointer did not move by the requested amount");
    }

    private static CGPoint Position()
    {
        var probe = MacNative.CGEventCreate(IntPtr.Zero);
        try { return MacNative.CGEventGetLocation(probe); }
        finally { MacNative.CFRelease(probe); }
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
