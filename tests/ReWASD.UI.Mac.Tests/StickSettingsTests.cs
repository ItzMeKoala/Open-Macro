using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class StickSettingsTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void ChangingTheWindowSettings_ChangesTheProfile()
    {
        var vm = New();
        var left = vm.SelectedProfile.LeftStick;

        vm.LeftStickSettings.ModeIndex = (int)StickMode.Mouse;
        vm.LeftStickSettings.MouseSpeed = 2000;
        vm.LeftStickSettings.PrecisionIndex = StickViewModel.PrecisionButtons.ToList().IndexOf(ControllerButton.RightTrigger);
        vm.LeftStickSettings.PrecisionFactor = 0.2;
        vm.RightStickSettings.ModeIndex = (int)StickMode.Keys;
        vm.RightStickSettings.KeyPresetIndex = 1;

        Assert.Equal(StickMode.Mouse, left.Mode);
        Assert.Equal(2000, left.MouseSpeed);
        Assert.Equal(ControllerButton.RightTrigger, left.PrecisionButton);
        Assert.Equal(0.2, left.PrecisionFactor);
        Assert.Equal(StickMode.Keys, vm.SelectedProfile.RightStick.Mode);
        Assert.Equal("Up", vm.SelectedProfile.RightStick.KeyUp);
        Assert.True(vm.LeftStickSettings.IsMouse);
        Assert.True(vm.RightStickSettings.IsKeys);
    }

    [Fact]
    public void SwitchingProfile_ShowsTheOtherProfilesSticks_WithoutChangingEither()
    {
        var vm = New();
        vm.LeftStickSettings.ModeIndex = (int)StickMode.Scroll;
        var first = vm.SelectedProfile;

        vm.NewProfileCommand.Execute(null);
        Assert.NotSame(first, vm.SelectedProfile);
        Assert.Equal(0, vm.LeftStickSettings.ModeIndex);
        Assert.Equal(StickMode.Gamepad, vm.SelectedProfile.LeftStick.Mode);

        vm.SelectedProfile = first;
        Assert.Equal((int)StickMode.Scroll, vm.LeftStickSettings.ModeIndex);
        Assert.Equal(StickMode.Scroll, first.LeftStick.Mode);
    }

    [Fact]
    public void ALayerStickChoice_IsStoredOnThatLayer()
    {
        var vm = New();
        vm.ActiveShiftLayer = 1;

        vm.LayerLeftStickIndex = 1 + (int)StickMode.Keys;
        Assert.Equal(StickMode.Keys, vm.SelectedProfile.ShiftLayers[1].LeftStickMode);
        Assert.Null(vm.SelectedProfile.ShiftLayers[0].LeftStickMode);

        vm.ActiveShiftLayer = 0;
        Assert.Equal(0, vm.LayerLeftStickIndex);
        vm.ActiveShiftLayer = 1;
        Assert.Equal(1 + (int)StickMode.Keys, vm.LayerLeftStickIndex);

        vm.LayerLeftStickIndex = 0;
        Assert.Null(vm.SelectedProfile.ShiftLayers[1].LeftStickMode);
    }

    [Fact]
    public void TheOptionLists_MatchTheEnums()
    {
        Assert.Equal(Enum.GetValues<StickMode>().Length, StickViewModel.ModeNames.Count);
        Assert.Equal(StickViewModel.PrecisionButtons.Count, StickViewModel.PrecisionNames.Count);
        Assert.Equal("Off", StickViewModel.PrecisionNames[0]);
    }
}

public class MacroEditorTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void AddingAStepTemplate_AppendsAValidStep()
    {
        var vm = New();
        vm.MacroScript = "KeyTap:A";

        foreach (var template in vm.MacroStepTemplates)
        {
            vm.AddMacroStepCommand.Execute(template);
            Assert.True(MacroDefinition.TryParseScript(vm.MacroScript, MacroRepeatMode.Once, out _, out var error), $"{template.Label}: {error}");
        }

        Assert.StartsWith("KeyTap:A; OpenUrl:", vm.MacroScript);
        Assert.Contains("Obs:Scene Gaming", vm.MacroScript);
    }

    [Fact]
    public void AddingAStep_ToAnEmptyOrSemicolonEndedScript_LeavesNoGap()
    {
        var vm = New();
        vm.MacroScript = "";
        vm.AddMacroStepCommand.Execute(vm.MacroStepTemplates[^1]);
        Assert.Equal("Delay:500", vm.MacroScript);

        vm.MacroScript = "KeyTap:A;  ";
        vm.AddMacroStepCommand.Execute(vm.MacroStepTemplates[^1]);
        Assert.Equal("KeyTap:A; Delay:500", vm.MacroScript);
    }

    [Fact]
    public void TheObsAddress_IsRemembered()
    {
        string dir = Fakes.TempProfiles();
        var first = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), dir);
        first.ObsHost = "192.168.1.5";
        first.ObsPort = 4466;
        first.ObsPassword = "pw";

        var second = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), dir);

        Assert.Equal("192.168.1.5", second.ObsHost);
        Assert.Equal(4466, second.ObsPort);
        Assert.Equal("pw", second.ObsPassword);
        Directory.Delete(dir, true);
    }
}
