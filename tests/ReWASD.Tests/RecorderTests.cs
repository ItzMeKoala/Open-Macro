using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class RecorderTests
{
    private long _now;
    private MacroRecorder NewRecorder() => new(() => _now);

    private static string Script(MacroDefinition macro) => macro.ToScript();

    [Fact]
    public void KeyPress_IsRecordedWithItsDuration()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnKey("W", true);
        _now = 120; r.OnKey("W", false);

        Assert.Equal("KeyDown:W; Delay:120; KeyUp:W", Script(r.Stop(MacroRepeatMode.Once, dropStopClick: false)));
    }

    [Fact]
    public void AutoRepeatAndStrayReleases_AreIgnored()
    {
        var r = NewRecorder();
        r.Start();

        r.OnKey("Q", false);                 // released something pressed before recording
        _now = 0; r.OnKey("W", true);
        _now = 30; r.OnKey("W", true);       // auto-repeat
        _now = 60; r.OnKey("W", true);
        _now = 100; r.OnKey("W", false);

        Assert.Equal("KeyDown:W; Delay:100; KeyUp:W", Script(r.Stop(MacroRepeatMode.Once, false)));
    }

    [Fact]
    public void TinyGaps_AreTreatedAsSimultaneous()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnKey("A", true);
        _now = 4; r.OnKey("B", true);        // under the minimum recorded delay
        _now = 200; r.OnKey("A", false);
        _now = 203; r.OnKey("B", false);

        Assert.Equal("KeyDown:A; KeyDown:B; Delay:196; KeyUp:A; KeyUp:B", Script(r.Stop(MacroRepeatMode.Once, false)));
    }

    [Fact]
    public void MouseAndWheel_AreRecorded()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnMouse("Right Click", true);
        _now = 50; r.OnMouse("Right Click", false);
        _now = 100; r.OnMouse("Wheel Down", true);
        _now = 100; r.OnMouse("Wheel Up", true);

        Assert.Equal("MouseDown:Right Click; Delay:50; MouseUp:Right Click; Delay:50; Wheel:-1; Wheel:1", Script(r.Stop(MacroRepeatMode.Once, false)));
    }

    [Fact]
    public void ClickThatStartsRecording_IsNotRecorded()
    {
        var r = NewRecorder();
        r.Start();

        r.OnMouse("Left Click", false);      // the mouse-up of the click on the Record button
        _now = 10; r.OnKey("E", true);
        _now = 80; r.OnKey("E", false);

        Assert.Equal("KeyDown:E; Delay:70; KeyUp:E", Script(r.Stop(MacroRepeatMode.Once, false)));
    }

    [Fact]
    public void ClickOnStop_IsDropped()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnKey("E", true);
        _now = 60; r.OnKey("E", false);
        _now = 900; r.OnMouse("Left Click", true);   // moving to the Stop button and clicking it
        _now = 960; r.OnMouse("Left Click", false);

        Assert.Equal("KeyDown:E; Delay:60; KeyUp:E", Script(r.Stop(MacroRepeatMode.Once, dropStopClick: true)));
    }

    [Fact]
    public void StopClickWithoutItsRelease_IsStillDropped_AndNothingIsLeftStuck()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnKey("E", true);
        _now = 60; r.OnKey("E", false);
        _now = 500; r.OnMouse("Left Click", true);   // stop pressed; the release never reached us

        var macro = r.Stop(MacroRepeatMode.Once, true);

        Assert.Equal("KeyDown:E; Delay:60; KeyUp:E", Script(macro));
    }

    [Fact]
    public void PadPresses_AreWorkedOutFromTheHeldSet()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnPad(new[] { ControllerButton.A });
        _now = 5; r.OnPad(new[] { ControllerButton.A, ControllerButton.B });
        _now = 250; r.OnPad(new[] { ControllerButton.B });
        _now = 300; r.OnPad(Array.Empty<ControllerButton>());

        var steps = r.Stop(MacroRepeatMode.Once, false).Steps.Select(s => s.ToString()).ToList();
        Assert.Equal(new[] { "PadDown:A", "PadDown:B", "Delay:245", "PadUp:A", "Delay:50", "PadUp:B" }, steps);
    }

    [Fact]
    public void ButtonsStillHeldWhenStopping_AreReleasedInOrdinaryMacros()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnPad(new[] { ControllerButton.A });
        _now = 100; r.OnKey("Space", true);

        var steps = r.Stop(MacroRepeatMode.Once, false).Steps.Select(s => s.ToString()).ToList();
        Assert.Equal("PadDown:A", steps[0]);
        Assert.Contains("KeyUp:Space", steps);
        Assert.Contains("PadUp:A", steps);
    }

    [Fact]
    public void HoldUntilRelease_KeepsTheRecordedInputsHeld()
    {
        var r = NewRecorder();
        r.Start();

        _now = 0; r.OnPad(new[] { ControllerButton.A, ControllerButton.B });
        _now = 400; r.OnPad(Array.Empty<ControllerButton>());

        Assert.Equal("PadDown:A; PadDown:B", Script(r.Stop(MacroRepeatMode.HoldUntilRelease, false)));
    }

    [Fact]
    public void EventsBeforeStartAndAfterStop_AreIgnored()
    {
        var r = NewRecorder();
        r.OnKey("W", true);
        r.Start();
        _now = 0; r.OnKey("A", true);
        _now = 10; r.OnKey("A", false);
        r.Stop(MacroRepeatMode.Once, false);
        r.OnKey("Z", true);

        Assert.False(r.IsRecording);
        Assert.Equal(2, r.StepCount); // exactly the A press and release, nothing from before Start or after Stop
    }
}

public class HoldMacroTests
{
    private sealed class NullBackend : IInputBackend
    {
        public void KeyDown(ushort virtualKey) { }
        public void KeyUp(ushort virtualKey) { }
        public void MouseDown(MouseButtonKind button) { }
        public void MouseUp(MouseButtonKind button) { }
        public void MouseWheel(int delta) { }
        public void MouseMove(int dx, int dy) { }
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }

    private static HashSet<ControllerButton> Held(MacroPadState pad)
    {
        var s = new RemappedState();
        pad.ApplyTo(s);
        return s.ActiveVirtualButtons;
    }

    [Fact]
    public void HoldUntilRelease_HoldsBothButtons_UntilTheTriggerIsReleased()
    {
        var pad = new MacroPadState();
        using var runner = new MacroRunner(new NullBackend(), pad);
        MacroDefinition.TryParseScript("PadDown:X; PadDown:Y", MacroRepeatMode.HoldUntilRelease, out var macro, out _);

        runner.Update(new HashSet<MacroDefinition> { macro });
        Assert.True(WaitFor(() => Held(pad).SetEquals(new[] { ControllerButton.X, ControllerButton.Y })));

        Thread.Sleep(150); // still held well after the steps finished
        Assert.True(Held(pad).SetEquals(new[] { ControllerButton.X, ControllerButton.Y }));

        runner.Update(new HashSet<MacroDefinition>());
        Assert.True(WaitFor(() => Held(pad).Count == 0));
    }

    [Fact]
    public void OneButtonPressingTwo_WorksEndToEnd_ThroughTheEngine()
    {
        // Button A on the physical pad should press X and Y together on the virtual pad
        var profile = Profile.CreateDefault();
        MacroDefinition.TryParseScript("PadDown:X; PadDown:Y", MacroRepeatMode.HoldUntilRelease, out var macro, out _);
        profile.ShiftLayers[0].Mappings[ControllerButton.A].Target = new RemapTarget { Type = TargetType.Macro, Macro = macro };

        var engine = new MappingEngine(profile, () => 0) { IsActive = true };
        var pad = new MacroPadState();
        using var runner = new MacroRunner(new NullBackend(), pad);

        RemappedState Frame(params ControllerButton[] pressed)
        {
            var state = engine.ProcessInputs(new HashSet<ControllerButton>(pressed), 0, 0, 0, 0, 0, 0);
            pad.ApplyTo(state);
            runner.Update(state.ActiveMacros);
            return state;
        }

        Frame(ControllerButton.A);
        Assert.True(WaitFor(() => Frame(ControllerButton.A).ActiveVirtualButtons.IsSupersetOf(new[] { ControllerButton.X, ControllerButton.Y })));
        Assert.DoesNotContain(ControllerButton.A, Frame(ControllerButton.A).ActiveVirtualButtons); // A itself was remapped away

        Assert.True(WaitFor(() => !Frame().ActiveVirtualButtons.Contains(ControllerButton.X) && !Frame().ActiveVirtualButtons.Contains(ControllerButton.Y)));
    }
}
