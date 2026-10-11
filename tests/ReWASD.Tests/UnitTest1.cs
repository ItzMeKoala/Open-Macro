using ReWASD.Core.Models;
using ReWASD.Core.Serialization;

namespace ReWASD.Tests;

public class ProfileTests
{
    [Fact]
    public void Profile_Initialization_ContainsDefaultLayersAndButtons()
    {
        var profile = Profile.CreateDefault("Test Profile");

        Assert.Equal("Test Profile", profile.Name);
        Assert.Equal(5, profile.ShiftLayers.Count);
        Assert.True(profile.ShiftLayers[0].Mappings.ContainsKey(ControllerButton.A));
        Assert.Equal(ControllerButton.A, profile.ShiftLayers[0].Mappings[ControllerButton.A].Target.VirtualButton);
    }

    [Fact]
    public void ProfileSerializer_RoundTrip_PreservesData()
    {
        var profile = Profile.CreateDefault("Apex Legends Profile");
        profile.ShiftLayers[0].Mappings[ControllerButton.RightTrigger].Target = new RemapTarget
        {
            Type = TargetType.KeyboardKey,
            KeyboardKey = "Space"
        };
        profile.ShiftLayers[0].Mappings[ControllerButton.RightTrigger].Mode = MappingMode.Turbo;
        profile.ShiftLayers[0].Mappings[ControllerButton.RightTrigger].TurboRateMs = 35;

        var json = ProfileSerializer.ToJson(profile);
        var restored = ProfileSerializer.FromJson(json);

        Assert.Equal("Apex Legends Profile", restored.Name);
        var rtMapping = restored.ShiftLayers[0].Mappings[ControllerButton.RightTrigger];
        Assert.Equal(TargetType.KeyboardKey, rtMapping.Target.Type);
        Assert.Equal("Space", rtMapping.Target.KeyboardKey);
        Assert.Equal(MappingMode.Turbo, rtMapping.Mode);
        Assert.Equal(35, rtMapping.TurboRateMs);
    }

    [Fact]
    public void StickSettings_DeadzoneAndCurve_TransformsAccurately()
    {
        var settings = new StickSettings
        {
            DeadzoneMin = 0.1,
            DeadzoneMax = 0.9,
            CurveType = ResponseCurveType.Linear
        };

        // Within deadzone -> should be 0
        Assert.Equal(0.0, settings.TransformAxis(0.05));
        Assert.Equal(0.0, settings.TransformAxis(-0.08));

        // Beyond max deadzone -> should be 1.0 (or -1.0)
        Assert.Equal(1.0, settings.TransformAxis(0.95));
        Assert.Equal(-1.0, settings.TransformAxis(-0.95));

        // Midpoint: (0.5 - 0.1) / (0.9 - 0.1) = 0.4 / 0.8 = 0.5
        Assert.Equal(0.5, settings.TransformAxis(0.5), 2);
    }
}
