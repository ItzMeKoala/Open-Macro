using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class MappingEngineTests
{
    private static RemappedState Process(MappingEngine engine, params ControllerButton[] pressed) =>
        engine.ProcessInputs(new HashSet<ControllerButton>(pressed), 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Toggle_StaysActiveAfterRelease_AndClearsOnSecondPress()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].Mappings[ControllerButton.A].Mode = MappingMode.Toggle;
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        Assert.Contains(ControllerButton.A, Process(engine, ControllerButton.A).ActiveVirtualButtons);
        Assert.Contains(ControllerButton.A, Process(engine).ActiveVirtualButtons);
        // Second press toggles it off
        Assert.DoesNotContain(ControllerButton.A, Process(engine, ControllerButton.A).ActiveVirtualButtons);
        Assert.DoesNotContain(ControllerButton.A, Process(engine).ActiveVirtualButtons);
    }

    [Fact]
    public void Turbo_AlternatesWhileHeld()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].Mappings[ControllerButton.A].Mode = MappingMode.Turbo;
        profile.ShiftLayers[0].Mappings[ControllerButton.A].TurboRateMs = 50;
        long now = 0;
        var engine = new MappingEngine(profile, () => now) { IsActive = true };

        Assert.Contains(ControllerButton.A, Process(engine, ControllerButton.A).ActiveVirtualButtons);
        now = 60;
        Assert.DoesNotContain(ControllerButton.A, Process(engine, ControllerButton.A).ActiveVirtualButtons);
        now = 110;
        Assert.Contains(ControllerButton.A, Process(engine, ControllerButton.A).ActiveVirtualButtons);
    }

    [Fact]
    public void InvertY_FlipsStickOutput()
    {
        var profile = Profile.CreateDefault();
        profile.LeftStick.InvertY = true;
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton>(), 0, 1.0, 0, 0, 0, 0);
        Assert.Equal(-1.0, state.LeftStickY);
    }

    [Fact]
    public void UnmappedTriggerPress_StaysAnalog_NotDigital()
    {
        var engine = new MappingEngine(Profile.CreateDefault(), () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftTrigger }, 0, 0, 0, 0, 0.6, 0);

        Assert.DoesNotContain(ControllerButton.LeftTrigger, state.ActiveVirtualButtons);
        Assert.Equal(0.6, state.LeftTrigger);
    }

    [Fact]
    public void RemappedTrigger_NoLongerDrivesAnalogValue()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].Mappings[ControllerButton.RightTrigger].Target = new RemapTarget
        {
            Type = TargetType.MouseButton,
            MouseButton = "Left Click"
        };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.RightTrigger }, 0, 0, 0, 0, 0, 1.0);

        Assert.Equal(0.0, state.RightTrigger);
        Assert.Contains("Left Click", state.ActiveMouseButtons);
    }

    [Fact]
    public void StickDirectionRemappedToKey_ReleasesAnalogStick()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].Mappings[ControllerButton.LeftStickUp].Target = new RemapTarget
        {
            Type = TargetType.KeyboardKey,
            KeyboardKey = "W"
        };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftStickUp }, 0, 1.0, 0.5, 0, 0, 0);

        Assert.Contains("W", state.ActiveKeyboardKeys);
        Assert.Equal(0.0, state.LeftStickY);
        Assert.NotEqual(0.0, state.RightStickX); // other stick unaffected
    }

    [Fact]
    public void TransformAxis_NaN_ReturnsZero()
    {
        Assert.Equal(0.0, new StickSettings().TransformAxis(double.NaN));
    }
}
