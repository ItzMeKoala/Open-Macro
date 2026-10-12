using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

/// <summary>The shared view model on fake macOS backends: it must behave the same without a virtual pad or HidHide.</summary>
public class ViewModelTests
{
    private static MainViewModel New(Fakes.Pad? pad = null, Fakes.Backend? backend = null, Fakes.Hook? hook = null, string? dir = null) =>
        new(Fakes.Platform(PlatformServices.MacOSInfo, pad, backend, hook), new Fakes.Ui(), dir ?? Fakes.TempProfiles());

    private static void Map(MainViewModel vm, ControllerButton button, string key)
    {
        vm.SelectButton(vm.ControllerButtons.First(b => b.Button == button));
        vm.SelectedRemapOption = vm.RemapOptions.First(o => o.Kind == RemapKind.Key && o.Name == key);
        vm.SaveCurrentMappingCommand.Execute(null);
    }

    [Fact]
    public void MacStatusTexts_SayWhatIsNotAvailable()
    {
        var vm = New();

        Assert.True(vm.IsMacOS);
        Assert.False(vm.SupportsVirtualController);
        Assert.False(vm.SupportsDeviceHiding);
        Assert.Equal("Virtual controller: not available on macOS", vm.DriverStatusText);
        Assert.Equal("Hide controller: not available on macOS", vm.HidHideStatusText);
        Assert.All(vm.VirtualControllerOptions, o => Assert.False(o.IsAvailable));
        Assert.Contains("No game controller detected", vm.ConnectedDevices[0]);
    }

    [Fact]
    public void ConnectedController_IsNamed()
    {
        var pad = new Fakes.Pad { State = new PhysicalState(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0) };
        var vm = New(pad);

        Assert.Contains("Test Controller", vm.ConnectedDevices[0]);
    }

    [Fact]
    public void Apply_RemapsAPadButtonToAKey_AndStopReleasesIt()
    {
        var pad = new Fakes.Pad();
        var backend = new Fakes.Backend();
        var hook = new Fakes.Hook();
        var vm = New(pad, backend, hook);
        Map(vm, ControllerButton.A, "Space");

        vm.ToggleApplyCommand.Execute(null);
        Assert.True(vm.IsRemappingActive);
        Assert.True(hook.IsInstalled);
        Assert.Contains("controller to keyboard, mouse and macros", vm.StatusMessage);

        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.A }, 0, 0, 0, 0, 0, 0);
        Assert.True(SpinUntil(() => backend.Events.Contains("kd:20")), "Space was never pressed");

        vm.Shutdown();
        Assert.Contains("ku:20", backend.Events);
        Assert.False(hook.IsInstalled);
    }

    [Fact]
    public void MutedMapping_ExplainsThatMacCannotHideTheController()
    {
        var vm = New();
        vm.SelectedButton = vm.ControllerButtons[0];
        vm.IsMuted = true;
        vm.SaveCurrentMappingCommand.Execute(null);

        vm.ToggleApplyCommand.Execute(null);

        Assert.Contains("not possible on macOS", vm.StatusMessage);
        vm.Shutdown();
    }

    [Fact]
    public void PanicKey_StopsRemapping()
    {
        var hook = new Fakes.Hook();
        var vm = New(hook: hook);
        vm.ToggleApplyCommand.Execute(null);
        Assert.True(vm.IsRemappingActive);

        hook.RaisePanic();

        Assert.False(vm.IsRemappingActive);
        Assert.Contains("Panic key pressed", vm.StatusMessage);
    }

    [Fact]
    public void SavedConfig_ReloadsFromTheProfilesFolder()
    {
        string dir = Fakes.TempProfiles();
        var first = New(dir: dir);
        Map(first, ControllerButton.B, "E");
        first.SaveProfileCommand.Execute(null);
        Assert.True(Directory.GetFiles(dir, "*.json").Length == 1);

        var second = New(dir: dir);

        var b = second.SelectedProfile.ShiftLayers[0].Mappings[ControllerButton.B];
        Assert.Equal(TargetType.KeyboardKey, b.Target.Type);
        Assert.Equal("E", b.Target.KeyboardKey);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void ProfilesFolder_IsTheMacApplicationSupportFolder()
    {
        if (!OperatingSystem.IsMacOS()) return;
        Assert.EndsWith("Library/Application Support/ReWASD-OpenSource/profiles", AppPaths.ProfilesDirectory);
    }

    [Fact]
    public void TogglingHideInputDevice_WhenNotSupported_ReportsStatus()
    {
        var vm = New();
        vm.HideInputDevice = true;

        Assert.Equal("Hiding a controller from other apps is not possible on macOS.", vm.HideStatusText);
    }

    private static bool SpinUntil(Func<bool> condition, int timeoutMs = 3000) => SpinWait.SpinUntil(condition, timeoutMs);
}
