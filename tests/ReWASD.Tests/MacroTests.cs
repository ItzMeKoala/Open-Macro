using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class MacroTests
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
    public void Script_RoundTripsThroughParser()
    {
        Assert.True(MacroDefinition.TryParseScript("KeyDown:W; Delay:50; KeyUp:W; Wheel:-2", MacroRepeatMode.Once, out var macro, out _));

        Assert.Equal(4, macro.Steps.Count);
        Assert.Equal(50, macro.Steps[1].Amount);
        Assert.Equal("KeyDown:W; Delay:50; KeyUp:W; Wheel:-2", macro.ToScript());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Jump:W")]
    [InlineData("Delay:abc")]
    [InlineData("KeyDown")]
    public void Script_RejectsBadInput(string script)
    {
        Assert.False(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void OnceMacro_RunsToCompletion_AndDoesNotRestartWhileHeld()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend);
        MacroDefinition.TryParseScript("KeyDown:W; Delay:10; KeyUp:W", MacroRepeatMode.Once, out var macro, out _);
        var held = new HashSet<MacroDefinition> { macro };

        runner.Update(held);
        Assert.True(WaitFor(() => backend.Events.Contains("ku:57")));
        runner.Update(held);
        runner.Update(held);
        Thread.Sleep(50);

        Assert.Single(backend.Events, "kd:57");
    }

    [Fact]
    public void WhileHeldMacro_StopsOnRelease_AndReleasesKeys()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend);
        MacroDefinition.TryParseScript("KeyDown:E; Delay:1000", MacroRepeatMode.WhileHeld, out var macro, out _);

        runner.Update(new HashSet<MacroDefinition> { macro });
        Assert.True(WaitFor(() => backend.Events.Contains("kd:45")));
        runner.Update(new HashSet<MacroDefinition>());

        Assert.True(WaitFor(() => backend.Events.Contains("ku:45")));
    }

    [Fact]
    public void ScrollTarget_ProducesWheelButtonName()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].Mappings[ControllerButton.DpadUp].Target = new RemapTarget
        {
            Type = TargetType.MouseScroll,
            ScrollDelta = -1
        };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.DpadUp }, 0, 0, 0, 0, 0, 0);

        Assert.Contains("Wheel Down", state.ActiveMouseButtons);
    }

    [Fact]
    public void MacroTarget_SurvivesSerializationAndEngine()
    {
        var profile = Profile.CreateDefault();
        MacroDefinition.TryParseScript("KeyTap:Space", MacroRepeatMode.WhileHeld, out var macro, out _);
        profile.ShiftLayers[0].Mappings[ControllerButton.X].Target = new RemapTarget { Type = TargetType.Macro, Macro = macro };

        var restored = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile));
        var engine = new MappingEngine(restored, () => 0) { IsActive = true };
        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.X }, 0, 0, 0, 0, 0, 0);

        var active = Assert.Single(state.ActiveMacros);
        Assert.Equal(MacroRepeatMode.WhileHeld, active.RepeatMode);
        Assert.Equal("KeyTap:Space", active.ToScript());
    }
}
