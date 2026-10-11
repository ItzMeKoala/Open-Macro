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
    public void TransformAxis_NaN_ReturnsZero()
    {
        Assert.Equal(0.0, new StickSettings().TransformAxis(double.NaN));
    }
}
