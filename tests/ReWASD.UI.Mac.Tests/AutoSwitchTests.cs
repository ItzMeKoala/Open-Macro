using ReWASD.Core.Models;
using ReWASD.Engine.Foreground;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class AutoSwitchTests
{
    private ForegroundApp? _front;

    private MainViewModel New(string? dir = null, Func<ForegroundApp?>? foreground = null) =>
        new(Fakes.Platform(PlatformServices.MacOSInfo, foreground: foreground ?? (() => _front)), new Fakes.Ui(), dir ?? Fakes.TempProfiles());

    private static ForegroundApp App(string name, string? bundle = null, int pid = 4242) => new(name, bundle, pid);

    private static Profile AddLinked(MainViewModel vm, string name, string process)
    {
        var profile = Profile.CreateDefault(name);
        profile.ProcessExecutable = process;
        vm.Profiles.Add(profile);
        return profile;
    }

    [Fact]
    public void ProfileFollowsTheAppInFront_AndComesBack()
    {
        var vm = New();
        var home = vm.SelectedProfile;
        var game = AddLinked(vm, "Game", "game");

        _front = App("Game");
        vm.CheckForeground();
        Assert.Same(game, vm.SelectedProfile);

        _front = App("Notes");
        vm.CheckForeground();
        Assert.Same(home, vm.SelectedProfile);
    }

    [Fact]
    public void TheEngineFollowsToo()
    {
        var vm = New();
        var game = AddLinked(vm, "Game", "game");

        _front = App("Game");
        vm.CheckForeground();

        Assert.Equal(game.Id, vm.SelectedProfile.Id);
        Assert.Equal("Switched to Game for Game", vm.StatusMessage);
    }

    [Fact]
    public void AProfilePickedByHand_IsTheOneToComeBackTo()
    {
        var vm = New();
        var game = AddLinked(vm, "Game", "game");
        var other = AddLinked(vm, "Other", "");
        other.ProcessExecutable = null;

        vm.SelectedProfile = other;               // the person chooses it
        _front = App("Game");
        vm.CheckForeground();
        Assert.Same(game, vm.SelectedProfile);

        _front = App("Notes");
        vm.CheckForeground();
        Assert.Same(other, vm.SelectedProfile);
    }

    [Fact]
    public void OurOwnWindowInFront_ChangesNothing()
    {
        var vm = New();
        var game = AddLinked(vm, "Game", "game");
        _front = App("Game");
        vm.CheckForeground();

        _front = App("ReWASD", pid: Environment.ProcessId);   // the person went to edit in our window
        vm.CheckForeground();

        Assert.Same(game, vm.SelectedProfile);
    }

    [Fact]
    public void WhenSwitchedOff_NothingChanges_AndTheHandPickedProfileReturns()
    {
        var vm = New();
        var home = vm.SelectedProfile;
        AddLinked(vm, "Game", "game");
        _front = App("Game");
        vm.CheckForeground();
        Assert.NotSame(home, vm.SelectedProfile);

        vm.AutoSwitchProfiles = false;
        Assert.Same(home, vm.SelectedProfile);

        vm.CheckForeground();
        Assert.Same(home, vm.SelectedProfile);
    }

    [Fact]
    public void AFailingOrMissingForegroundCheck_IsHarmless()
    {
        var failing = New(foreground: () => throw new InvalidOperationException("no window server"));
        failing.CheckForeground();
        Assert.False(failing.SelectedProfile == null);

        var none = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        Assert.False(none.CanAutoSwitch);
        none.CheckForeground();
    }

    [Fact]
    public void TheSetting_IsRemembered()
    {
        string dir = Fakes.TempProfiles();
        var first = New(dir);
        first.AutoSwitchProfiles = false;

        Assert.False(New(dir).AutoSwitchProfiles);
        Directory.Delete(dir, true);
    }
}

public class LockViewModelTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void TheLockButton_LocksAndUnlocksTheController()
    {
        var vm = New();

        vm.ToggleLockCommand.Execute(null);
        Assert.True(vm.IsControllerLocked);
        Assert.Contains("locked", vm.StatusMessage);

        vm.ToggleLockCommand.Execute(null);
        Assert.False(vm.IsControllerLocked);
        Assert.Equal("Controller unlocked", vm.StatusMessage);
    }

    [Fact]
    public void TheChordChoice_IsStoredInTheProfile_AndShownAgain()
    {
        var vm = New();
        var first = vm.SelectedProfile;

        vm.LockChordIndex = 1;
        Assert.Equal(new[] { ControllerButton.View, ControllerButton.Menu }, first.LockChord);

        vm.NewProfileCommand.Execute(null);
        Assert.Equal(0, vm.LockChordIndex);
        Assert.Empty(vm.SelectedProfile.LockChord);

        vm.SelectedProfile = first;
        Assert.Equal(1, vm.LockChordIndex);
        Assert.Equal(2, first.LockChord.Count);   // showing it did not change it

        vm.LockChordIndex = 0;
        Assert.Empty(first.LockChord);
    }
}
