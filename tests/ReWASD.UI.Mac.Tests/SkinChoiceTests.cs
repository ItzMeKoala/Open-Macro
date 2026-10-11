using ReWASD.Core.Models;
using ReWASD.Core.Skins;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class SkinChoiceTests
{
    private sealed class NamedPad : IPhysicalController
    {
        public string? Name;
        public int ExcludedUserIndex { get; set; } = -1;
        public int ActiveUserIndex => Name == null ? -1 : 0;
        public PhysicalState? Read() => Name == null ? null : new PhysicalState(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);
        public IReadOnlyList<int> ConnectedSlots() => Name == null ? Array.Empty<int>() : new[] { 0 };
        public string DescribeSlot(int slot) => Name ?? "";
    }

    private static MainViewModel New(NamedPad pad)
    {
        var platform = Fakes.Platform(PlatformServices.MacOSInfo);
        var wrapped = new PlatformBackends
        {
            Info = platform.Info, Reader = pad, InputBackend = platform.InputBackend, Rumble = platform.Rumble,
            VirtualPad = platform.VirtualPad, CreateHook = platform.CreateHook, MissingPermissions = platform.MissingPermissions
        };
        return new MainViewModel(wrapped, new Fakes.Ui(), Fakes.TempProfiles());
    }

    [Theory]
    [InlineData("DualSense Wireless Controller", SkinKind.PlayStation5)]
    [InlineData("DualSense Edge Wireless Controller", SkinKind.PlayStation5)]
    [InlineData("DUALSHOCK 4 Wireless Controller", SkinKind.PlayStation4)]
    [InlineData("Wireless Controller", SkinKind.PlayStation4)]
    [InlineData("Xbox Wireless Controller", SkinKind.Xbox)]
    [InlineData("Xbox One S Controller", SkinKind.Xbox)]
    [InlineData("Pro Controller", SkinKind.SwitchPro)]
    public void ANamedController_GetsItsOwnPicture(string name, SkinKind expected) =>
        Assert.Equal(expected, ControllerSkins.GuessFromName(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XInput Controller 1")]
    [InlineData("Some Arcade Stick")]
    public void AnUnknownController_GuessesNothing(string? name) => Assert.Null(ControllerSkins.GuessFromName(name));

    [Fact]
    public void ADualSenseAlreadyConnected_ShowsThePlayStation5Drawing()
    {
        var vm = New(new NamedPad { Name = "DualSense Wireless Controller" });

        Assert.Equal(SkinKind.PlayStation5, vm.SelectedSkin);
        Assert.True(vm.ShowSkinArt);
    }

    [Fact]
    public void PluggingAControllerIn_SwitchesThePicture_AndUnplugging_KeepsIt()
    {
        var pad = new NamedPad();
        var vm = New(pad);
        Assert.Equal(SkinKind.Xbox, vm.SelectedSkin);

        pad.Name = "DualSense Wireless Controller";
        vm.RefreshDevicesForTests();
        Assert.Equal(SkinKind.PlayStation5, vm.SelectedSkin);

        pad.Name = null;
        vm.RefreshDevicesForTests();
        Assert.Equal(SkinKind.PlayStation5, vm.SelectedSkin);
    }

    [Fact]
    public void APictureChosenByHand_IsNeverOverridden()
    {
        var pad = new NamedPad();
        var vm = New(pad);

        vm.SelectedSkin = SkinKind.SwitchPro;        // the person picked it
        pad.Name = "DualSense Wireless Controller";
        vm.RefreshDevicesForTests();

        Assert.Equal(SkinKind.SwitchPro, vm.SelectedSkin);
    }

    [Fact]
    public void AnUnrecognisedController_LeavesThePictureAlone()
    {
        var vm = New(new NamedPad { Name = "XInput Controller 1" });

        Assert.Equal(SkinKind.Xbox, vm.SelectedSkin);
    }
}

public class SkinMemoryTests
{
    private static MainViewModel New(string dir) =>
        new(Fakes.Platform(ReWASD.Engine.Platform.PlatformServices.MacOSInfo), new Fakes.Ui(), dir);

    [Fact]
    public void ThePictureYouPick_IsRememberedNextTime()
    {
        string dir = Fakes.TempProfiles();
        var first = New(dir);
        first.SelectedSkin = SkinKind.PlayStation5;

        var second = New(dir);

        Assert.Equal(SkinKind.PlayStation5, second.SelectedSkin);
        Assert.True(second.ShowSkinArt);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void WithNothingSaved_ThePictureIsTheDefault_AndNothingIsWritten()
    {
        string dir = Fakes.TempProfiles();

        var vm = New(dir);

        Assert.Equal(SkinKind.Xbox, vm.SelectedSkin);
        Assert.False(File.Exists(Path.Combine(dir, "skin.txt")));
    }

    [Fact]
    public void ADamagedMemory_IsIgnored()
    {
        string dir = Fakes.TempProfiles();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "skin.txt"), "not a picture");

        var vm = New(dir);

        Assert.Equal(SkinKind.Xbox, vm.SelectedSkin);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void TheAutomaticPick_IsNotSavedAsAChoice()
    {
        string dir = Fakes.TempProfiles();
        var pad = new Fakes.Pad { State = new ReWASD.Engine.Input.PhysicalState(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0) };
        var vm = new MainViewModel(Fakes.Platform(ReWASD.Engine.Platform.PlatformServices.MacOSInfo, pad), new Fakes.Ui(), dir);

        Assert.False(File.Exists(Path.Combine(dir, "skin.txt")));
        Assert.NotNull(vm);
    }
}
