using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class EditingTests : IDisposable
{
    private readonly string _dir = Fakes.TempProfiles();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), _dir);

    private static ButtonViewModel Pad(MainViewModel vm, ControllerButton b) => vm.ControllerButtons.First(x => x.Button == b);

    private static RemapTarget MappingOf(MainViewModel vm, ControllerButton b, int layer = 0, ActivatorType activator = ActivatorType.Single) =>
        vm.SelectedProfile.ShiftLayers[layer].Mappings[b].GetBinding(activator)!.Target;

    private static void EditToKey(MainViewModel vm, string key)
    {
        vm.TargetMode = TargetMode.Remap;
        vm.SelectedTargetType = TargetType.KeyboardKey;
        vm.SelectedKeyboardKey = key;
    }

    // ---- knowing there is something to save ---------------------------------------------------------------------------

    [Fact]
    public void SelectingAnInput_IsNotAnEdit_ButChangingTheInspectorIs()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        Assert.False(vm.IsInspectorDirty);
        Assert.Equal("Saved", vm.SaveButtonText);

        vm.SelectButton(Pad(vm, ControllerButton.B));
        Assert.False(vm.IsInspectorDirty);

        EditToKey(vm, "Q");
        Assert.True(vm.IsInspectorDirty);
        Assert.Equal("Save change", vm.SaveButtonText);

        vm.SaveCurrentMappingCommand.Execute(null);
        Assert.False(vm.IsInspectorDirty);
        Assert.Equal("Saved", vm.SaveButtonText);
        Assert.Equal("Q", MappingOf(vm, ControllerButton.B).KeyboardKey);
    }

    [Fact]
    public void AMacroWithAnError_StaysUnsaved_AndStaysMarked()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        vm.TargetMode = TargetMode.Macro;
        vm.MacroScript = "Nonsense:1";

        vm.SaveCurrentMappingCommand.Execute(null);

        Assert.True(vm.IsInspectorDirty);
        Assert.Contains("Unknown action", vm.MacroError);
    }

    // ---- nothing is lost by clicking away -----------------------------------------------------------------------------

    [Fact]
    public void ClickingAnotherInput_StoresThePendingEdit()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        EditToKey(vm, "Space");

        vm.SelectButton(Pad(vm, ControllerButton.B));

        Assert.Equal(TargetType.KeyboardKey, MappingOf(vm, ControllerButton.A).Type);
        Assert.Equal("Space", MappingOf(vm, ControllerButton.A).KeyboardKey);
        Assert.StartsWith("Saved your change to", vm.StatusMessage);
        Assert.False(vm.IsInspectorDirty);
        Assert.Equal(TargetType.VirtualControllerButton, MappingOf(vm, ControllerButton.B).Type);      // B is untouched
    }

    [Fact]
    public void AnEditThatCannotBeStored_IsDroppedWithAReason_NotSavedHalfWay()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        vm.TargetMode = TargetMode.Macro;
        vm.MacroScript = "Nonsense:1";

        vm.SelectButton(Pad(vm, ControllerButton.B));

        Assert.Equal(TargetType.VirtualControllerButton, MappingOf(vm, ControllerButton.A).Type);
        Assert.Contains("was not saved", vm.StatusMessage);
        Assert.False(vm.IsInspectorDirty);
    }

    [Fact]
    public void ChangingTheActivator_StoresTheEditUnderTheOldOne()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        EditToKey(vm, "E");

        vm.SelectedActivator = ActivatorType.Long;

        Assert.Equal("E", MappingOf(vm, ControllerButton.A).KeyboardKey);                              // went to Single
        Assert.Null(vm.SelectedProfile.ShiftLayers[0].Mappings[ControllerButton.A].GetBinding(ActivatorType.Long));
        Assert.Equal(TargetType.None, vm.SelectedTargetType);                                          // Long starts out empty
        Assert.False(vm.IsInspectorDirty);
    }

    [Fact]
    public void ChangingTheLayer_StoresTheEditInTheLayerItWasMadeIn()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.X));
        EditToKey(vm, "R");

        vm.ActiveShiftLayer = 1;

        Assert.Equal("R", MappingOf(vm, ControllerButton.X, layer: 0).KeyboardKey);
        Assert.False(vm.SelectedProfile.ShiftLayers[1].Mappings.ContainsKey(ControllerButton.X));
    }

    [Fact]
    public void ChangingTheConfig_StoresTheEditInTheConfigItWasMadeIn()
    {
        var vm = New();
        var first = vm.SelectedProfile;
        vm.SelectButton(Pad(vm, ControllerButton.Y));
        EditToKey(vm, "T");

        vm.NewProfileCommand.Execute(null);

        Assert.NotSame(first, vm.SelectedProfile);
        Assert.Equal("T", first.ShiftLayers[0].Mappings[ControllerButton.Y].Target.KeyboardKey);
        Assert.Equal(TargetType.VirtualControllerButton, vm.SelectedProfile.ShiftLayers[0].Mappings[ControllerButton.Y].Target.Type);
    }

    [Fact]
    public void Closing_StoresThePendingEdit_AndWritesTheConfig()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        EditToKey(vm, "Z");

        vm.Shutdown();

        string file = Directory.GetFiles(_dir, "*.json").Single();
        Assert.Contains("\"Z\"", File.ReadAllText(file));
    }

    // ---- saving configs by themselves ----------------------------------------------------------------------------------

    [Fact]
    public void AChangedConfig_IsWrittenOnceItHasStayedTheSameForAMoment()
    {
        var vm = New();
        Assert.Empty(Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*.json") : Array.Empty<string>());

        vm.SelectedProfile.LeftStick.MouseSpeed = 1234;
        vm.AutoSaveTick();
        Assert.Equal("Unsaved changes", vm.SaveStateText);                        // seen, but wait: it may still be changing
        Assert.Empty(Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*.json") : Array.Empty<string>());

        vm.AutoSaveTick();
        Assert.Equal("Saved", vm.SaveStateText);
        string file = Directory.GetFiles(_dir, "*.json").Single();
        Assert.Contains("1234", File.ReadAllText(file));
    }

    [Fact]
    public void AConfigThatKeepsChanging_IsNotWrittenUntilItSettles()
    {
        var vm = New();

        for (int i = 1; i <= 5; i++)
        {
            vm.SelectedProfile.LeftStick.MouseSpeed = 1000 + i;
            vm.AutoSaveTick();
        }
        Assert.Empty(Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*.json") : Array.Empty<string>());

        vm.AutoSaveTick();
        Assert.Contains("1005", File.ReadAllText(Directory.GetFiles(_dir, "*.json").Single()));
    }

    [Fact]
    public void NothingChanged_MeansNothingWritten_AndTurningItOffMeansNever()
    {
        var vm = New();
        vm.AutoSaveTick(); vm.AutoSaveTick();
        Assert.Empty(Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*.json") : Array.Empty<string>());
        Assert.Equal("Saved", vm.SaveStateText);

        vm.AutoSaveProfiles = false;
        vm.SelectedProfile.LeftStick.MouseSpeed = 777;
        vm.AutoSaveTick(); vm.AutoSaveTick();
        vm.FlushAutoSave();
        Assert.Empty(Directory.GetFiles(_dir, "*.json"));

        Assert.False(New().AutoSaveProfiles);                                       // and the choice is remembered
    }

    [Fact]
    public void ANewConfig_GetsAFileOfItsOwn_WithoutAnyEdit()
    {
        var vm = New();
        vm.NewProfileCommand.Execute(null);
        vm.AutoSaveTick(); vm.AutoSaveTick();

        Assert.Contains(Directory.GetFiles(_dir, "*.json"), f => Path.GetFileName(f).StartsWith("New Profile"));
    }

    [Fact]
    public void SwitchingConfig_WritesTheOneYouLeft()
    {
        var vm = New();
        var first = vm.SelectedProfile;
        first.LeftStick.MouseSpeed = 4321;

        vm.NewProfileCommand.Execute(null);

        Assert.Contains("4321", File.ReadAllText(Path.Combine(_dir, first.Name + ".json")));
    }

    [Fact]
    public void AutoSaves_DoNotFloodTheHistory()
    {
        var vm = New();
        for (int i = 0; i < 6; i++)
        {
            vm.SelectedProfile.LeftStick.MouseSpeed = 2000 + i;
            vm.AutoSaveTick(); vm.AutoSaveTick();
        }

        Assert.True(vm.HistoryEntries.Count <= 1, $"{vm.HistoryEntries.Count} restore points");
        Assert.Contains("2005", File.ReadAllText(Directory.GetFiles(_dir, "*.json").Single()));
    }

    // ---- plain words ----------------------------------------------------------------------------------------------------

    [Fact]
    public void TheButtonAndTheStatus_SayWhatIsHappeningInPlainWords()
    {
        var vm = New();
        Assert.Equal("Start remapping", vm.ApplyButtonText);
        Assert.Equal("Remapping is off", vm.RemappingStateText);
        Assert.Equal("Remapping is off.", vm.StatusMessage);

        vm.IsRemappingActive = true;
        Assert.Equal("Stop remapping", vm.ApplyButtonText);
        Assert.Equal("Remapping is on", vm.RemappingStateText);
    }
}
