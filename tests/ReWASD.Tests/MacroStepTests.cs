using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class MacroStepTests
{
    private sealed class FakeBackend : IInputBackend
    {
        private readonly object _gate = new();
        private readonly List<string> _events = new();
        public List<string> Events { get { lock (_gate) return _events.ToList(); } }
        private void Add(string e) { lock (_gate) _events.Add(e); }

        public void KeyDown(ushort virtualKey) => Add($"kd:{virtualKey:X}");
        public void KeyUp(ushort virtualKey) => Add($"ku:{virtualKey:X}");
        public void MouseDown(MouseButtonKind button) => Add($"md:{button}");
        public void MouseUp(MouseButtonKind button) => Add($"mu:{button}");
        public void MouseWheel(int delta) => Add($"wheel:{delta}");
        public void MouseMove(int dx, int dy) => Add($"move:{dx},{dy}");
    }

    private sealed class FakeRumble : IRumbleOutput
    {
        private readonly object _gate = new();
        private readonly List<(double L, double R)> _calls = new();
        public List<(double L, double R)> Calls { get { lock (_gate) return _calls.ToList(); } }
        public void Rumble(double left, double right) { lock (_gate) _calls.Add((left, right)); }
    }

    private static MacroDefinition Parse(string script, MacroRepeatMode mode = MacroRepeatMode.Once)
    {
        Assert.True(MacroDefinition.TryParseScript(script, mode, out var macro, out var error), error);
        return macro;
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

    [Fact]
    public void Parser_AcceptsEveryNewStep_AndRoundTrips()
    {
        const string script = "MouseMove:right:40; PadDown:A; PadTap:B; PadUp:A; PadAxis:LeftStickX:-80; Rumble:80,60:200; Break";

        var macro = Parse(script);

        Assert.Equal(7, macro.Steps.Count);
        Assert.Equal(-80, macro.Steps[4].Amount);
        Assert.Equal("80,60", macro.Steps[5].Value);
        Assert.Equal(script, macro.ToScript());
    }

    [Theory]
    [InlineData("MouseMove:sideways:10")]
    [InlineData("MouseMove:left")]
    [InlineData("PadDown:Banana")]
    [InlineData("PadDown:None")]
    [InlineData("PadAxis:LeftStickX")]
    [InlineData("PadAxis:Nowhere:10")]
    [InlineData("PadAxis:LeftStickX:300")]
    [InlineData("Rumble:80:200")]
    [InlineData("Rumble:150,10:100")]
    [InlineData("Break:now")]
    [InlineData("99:W")]
    public void Parser_RejectsMalformedSteps(string script)
    {
        Assert.False(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void MouseMove_SumsToRequestedDistance()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend);
        var macro = Parse("MouseMove:right:35; MouseMove:up:12");

        int[] Totals()
        {
            var moves = backend.Events.Where(e => e.StartsWith("move:")).Select(e => e[5..].Split(',').Select(int.Parse).ToArray()).ToList();
            return new[] { moves.Sum(m => m[0]), moves.Sum(m => m[1]) };
        }

        runner.Update(new HashSet<MacroDefinition> { macro });

        Assert.True(WaitFor(() => Totals().SequenceEqual(new[] { 35, -12 })), $"totals were {string.Join(",", Totals())}");
    }

    [Fact]
    public void PadSteps_DriveTheMacroPadState_AndReleaseOnCancel()
    {
        var backend = new FakeBackend();
        var pad = new MacroPadState();
        using var runner = new MacroRunner(backend, pad);
        var macro = Parse("PadDown:A; PadAxis:RightStickX:50; Delay:5000", MacroRepeatMode.WhileHeld);

        runner.Update(new HashSet<MacroDefinition> { macro });
        Assert.True(WaitFor(() =>
        {
            var s = new RemappedState();
            pad.ApplyTo(s);
            return s.ActiveVirtualButtons.Contains(ControllerButton.A) && Math.Abs(s.RightStickX - 0.5) < 1e-9;
        }));

        runner.Update(new HashSet<MacroDefinition>()); // trigger released: WhileHeld macro is cancelled

        Assert.True(WaitFor(() =>
        {
            var s = new RemappedState();
            pad.ApplyTo(s);
            return s.ActiveVirtualButtons.Count == 0 && s.RightStickX == 0;
        }));
    }

    [Fact]
    public void Rumble_StartsAndStopsAfterDuration()
    {
        var backend = new FakeBackend();
        var rumble = new FakeRumble();
        using var runner = new MacroRunner(backend, rumble: rumble);
        var macro = Parse("Rumble:80,40:30");

        runner.Update(new HashSet<MacroDefinition> { macro });

        Assert.True(WaitFor(() => rumble.Calls.Count >= 2));
        Assert.Equal((0.8, 0.4), rumble.Calls[0]);
        Assert.Equal((0.0, 0.0), rumble.Calls[1]);
    }

    [Fact]
    public void Break_EndsARepeatingMacro()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend);
        var macro = Parse("KeyTap:E; Break", MacroRepeatMode.WhileHeld);

        runner.Update(new HashSet<MacroDefinition> { macro });
        Assert.True(WaitFor(() => backend.Events.Contains("ku:45")));
        Thread.Sleep(80); // a looping macro would have tapped again by now

        Assert.Single(backend.Events, "kd:45");
    }

    [Fact]
    public void KeyHeldByMacro_IsReleasedWhenMacroIsCancelled()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend);
        var macro = Parse("KeyDown:W; Delay:5000", MacroRepeatMode.WhileHeld);

        runner.Update(new HashSet<MacroDefinition> { macro });
        Assert.True(WaitFor(() => backend.Events.Contains("kd:57")));
        runner.CancelAll();

        Assert.True(WaitFor(() => backend.Events.Contains("ku:57")));
    }

    [Fact]
    public void MacroPadState_TwoMacrosHoldingTheSameButton_NeedBothToRelease()
    {
        var pad = new MacroPadState();
        pad.SetButton(ControllerButton.X, true);
        pad.SetButton(ControllerButton.X, true);
        pad.SetButton(ControllerButton.X, false);

        var state = new RemappedState();
        pad.ApplyTo(state);
        Assert.Contains(ControllerButton.X, state.ActiveVirtualButtons);

        pad.SetButton(ControllerButton.X, false);
        var after = new RemappedState();
        pad.ApplyTo(after);
        Assert.Empty(after.ActiveVirtualButtons);
    }
}
