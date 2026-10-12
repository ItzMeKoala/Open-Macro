using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Platform;
using ReWASD.UI.Profiles;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class ProfileFileTests : IDisposable
{
    private readonly string _dir = Fakes.TempProfiles();
    private readonly Fakes.Ui _ui = new();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), _ui, _dir);

    private static Profile WithMacro(string name, string script)
    {
        var profile = Profile.CreateDefault(name);
        Assert.True(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out var macro, out var error), error);
        profile.ShiftLayers[0].Mappings[ControllerButton.A] = new ButtonMapping
        {
            SourceButton = ControllerButton.A,
            Target = new RemapTarget { Type = TargetType.Macro, Macro = macro }
        };
        return profile;
    }

    private string WriteFile(Profile profile)
    {
        string path = Path.Combine(_dir, "incoming-" + Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(_dir);
        ProfileSerializer.SaveToFile(profile, path);
        return path;
    }

    // ---- safety --------------------------------------------------------------------------------------------------

    [Fact]
    public void AProfileThatOnlyPressesKeys_HasNothingRisky()
    {
        Assert.Empty(ProfileSafety.FindRiskyActions(WithMacro("safe", "KeyDown:W; Delay:50; KeyUp:W; TypeText:hi")));
        Assert.Empty(ProfileSafety.FindRiskyActions(Profile.CreateDefault()));
    }

    [Fact]
    public void EveryWayOutOfTheKeyboardIsListed_WithWhereItHappens()
    {
        var profile = WithMacro("risky", "Shell:rm -rf ~; LaunchApp:Calculator; Webhook:POST https://x.test/h {}; Obs:record; OpenUrl:https://x.test");
        profile.ShiftLayers[1].Name = "Combat";
        profile.ShiftLayers[1].KeyboardMappings["Q"] = profile.ShiftLayers[0].Mappings[ControllerButton.A].Clone();

        var risks = ProfileSafety.FindRiskyActions(profile);

        Assert.Equal(10, risks.Count);
        Assert.Contains("Runs a command: rm -rf ~ (on A)", risks);
        Assert.Contains("Starts a program: Calculator (on A)", risks);
        Assert.Contains(risks, r => r.StartsWith("Sends a web request:") && r.EndsWith("(on key Q, Combat)"));
        Assert.Contains(risks, r => r.StartsWith("Controls OBS:"));
        Assert.Contains(risks, r => r.StartsWith("Opens a web page:"));
    }

    [Fact]
    public void ARiskyMacro_OnAnActivatorOtherThanSingle_IsFoundToo()
    {
        var profile = Profile.CreateDefault();
        Assert.True(MacroDefinition.TryParseScript("Shell:calc", MacroRepeatMode.Once, out var macro, out _));
        profile.ShiftLayers[0].Mappings[ControllerButton.B].SetBinding(ActivatorType.Long,
            new ActivatorBinding { Target = new RemapTarget { Type = TargetType.Macro, Macro = macro } });

        Assert.Single(ProfileSafety.FindRiskyActions(profile));
    }

    // ---- import and export -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ASafeProfile_ImportsWithoutAQuestion()
    {
        var vm = New();
        _ui.OpenPath = WriteFile(WithMacro("Friend", "KeyTap:A"));

        await vm.ImportProfileCommand.ExecuteAsync(null);

        Assert.Empty(_ui.Questions);
        Assert.Equal("Friend", vm.SelectedProfile.Name);
        Assert.Contains(vm.Profiles, p => p.Name == "Friend");
    }

    [Fact]
    public async Task ARiskyProfile_IsShownForApproval_AndRefusingImportsNothing()
    {
        var vm = New();
        int before = vm.Profiles.Count;
        _ui.OpenPath = WriteFile(WithMacro("Friend", "Shell:rm -rf ~"));
        _ui.Confirmation = false;

        await vm.ImportProfileCommand.ExecuteAsync(null);

        Assert.Single(_ui.Questions);
        Assert.Contains("rm -rf ~", _ui.Questions[0]);
        Assert.Equal(before, vm.Profiles.Count);
        Assert.Equal("Import cancelled", vm.StatusMessage);

        _ui.Confirmation = true;
        await vm.ImportProfileCommand.ExecuteAsync(null);
        Assert.Equal(before + 1, vm.Profiles.Count);
        Assert.Equal("Friend", vm.SelectedProfile.Name);
    }

    [Fact]
    public async Task ImportingTwice_GivesEachACopyOfItsOwn_AndNeverReusesAnId()
    {
        var vm = New();
        var profile = WithMacro("Friend", "KeyTap:A");
        _ui.OpenPath = WriteFile(profile);

        await vm.ImportProfileCommand.ExecuteAsync(null);
        await vm.ImportProfileCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Friend", "Friend (2)" }, vm.Profiles.Where(p => p.Name.StartsWith("Friend")).Select(p => p.Name));
        Assert.Equal(2, vm.Profiles.Where(p => p.Name.StartsWith("Friend")).Select(p => p.Id).Distinct().Count());
        Assert.NotEqual(profile.Id, vm.SelectedProfile.Id);
    }

    [Fact]
    public async Task AFileThatIsNotAProfile_IsRefusedWithAMessage_AndChangesNothing()
    {
        var vm = New();
        int before = vm.Profiles.Count;
        string bad = Path.Combine(_dir, "bad.txt");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(bad, "this is not json");
        _ui.OpenPath = bad;

        await vm.ImportProfileCommand.ExecuteAsync(null);
        Assert.StartsWith("Could not import bad.txt", vm.StatusMessage);

        _ui.OpenPath = Path.Combine(_dir, "missing.json");
        await vm.ImportProfileCommand.ExecuteAsync(null);
        Assert.StartsWith("Could not import missing.json", vm.StatusMessage);

        _ui.OpenPath = null;                                 // cancelling the picker is quiet
        await vm.ImportProfileCommand.ExecuteAsync(null);

        Assert.Equal(before, vm.Profiles.Count);
    }

    [Fact]
    public async Task Export_WritesAFileThatImportsBack()
    {
        var vm = New();
        vm.SelectedProfile.LeftStick.Mode = StickMode.Mouse;
        _ui.SavePath = Path.Combine(_dir, "shared.json");
        Directory.CreateDirectory(_dir);

        await vm.ExportProfileCommand.ExecuteAsync(null);

        var back = ProfileSerializer.LoadFromFile(_ui.SavePath);
        Assert.Equal(vm.SelectedProfile.Name, back.Name);
        Assert.Equal(StickMode.Mouse, back.LeftStick.Mode);
    }

    // ---- delete, history and undo -----------------------------------------------------------------------------------

    [Fact]
    public void DeletingAConfig_KeepsACopy_RemovesItsFile_AndSelectsAnother()
    {
        var vm = New();
        vm.NewProfileCommand.Execute(null);
        vm.SaveProfileCommand.Execute(null);
        var doomed = vm.SelectedProfile;
        string file = Directory.GetFiles(_dir, "*.json").Single(f => f.Contains(doomed.Name));

        vm.DeleteProfileCommand.Execute(null);

        Assert.DoesNotContain(doomed, vm.Profiles);
        Assert.NotSame(doomed, vm.SelectedProfile);
        Assert.False(File.Exists(file));
        Assert.Contains(vm.HistoryEntries, e => e.Reason == "deleted" && e.ProfileName == doomed.Name);
        Assert.False(vm.Profiles.Count == 0);
    }

    [Fact]
    public void DeletingTheOnlyConfig_LeavesAFreshOne()
    {
        var vm = New();
        while (vm.Profiles.Count > 1) vm.Profiles.RemoveAt(vm.Profiles.Count - 1);
        vm.SelectedProfile = vm.Profiles[0];

        vm.DeleteProfileCommand.Execute(null);

        Assert.Single(vm.Profiles);
        Assert.NotNull(vm.SelectedProfile);
    }

    [Fact]
    public void RestoringADeletedConfig_BringsItBackExactly()
    {
        var vm = New();
        vm.NewProfileCommand.Execute(null);
        var doomed = vm.SelectedProfile;
        doomed.LeftStick.Mode = StickMode.Keys;
        doomed.ShiftLayers[0].Mappings[ControllerButton.A].Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = "Space" };
        vm.DeleteProfileCommand.Execute(null);

        vm.RestoreHistoryEntryCommand.Execute(vm.HistoryEntries.First(e => e.Reason == "deleted"));

        Assert.Equal(doomed.Name, vm.SelectedProfile.Name);
        Assert.Equal(doomed.Id, vm.SelectedProfile.Id);
        Assert.Equal(StickMode.Keys, vm.SelectedProfile.LeftStick.Mode);
        Assert.Equal("Space", vm.SelectedProfile.ShiftLayers[0].Mappings[ControllerButton.A].Target.KeyboardKey);
    }

    [Fact]
    public void SavingOverAChangedFile_KeepsTheOldOne_AndRestoringIsItselfUndoable()
    {
        var vm = New();
        var profile = vm.SelectedProfile;
        vm.SaveProfileCommand.Execute(null);
        vm.SaveProfileCommand.Execute(null);                       // nothing changed: nothing to keep
        Assert.Empty(vm.HistoryEntries);

        profile.LeftStick.MouseSpeed = 777;
        vm.SaveProfileCommand.Execute(null);
        Assert.Single(vm.HistoryEntries);

        vm.RestoreHistoryEntryCommand.Execute(vm.HistoryEntries[0]);
        Assert.Equal(1200, vm.SelectedProfile.LeftStick.MouseSpeed);        // the version from before the save
        Assert.Equal(profile.Id, vm.SelectedProfile.Id);
        Assert.Equal(1, vm.Profiles.Count(p => p.Id == profile.Id));       // replaced, not duplicated
        Assert.Contains(vm.HistoryEntries, e => e.Reason == "replaced by a restore");

        vm.RestoreHistoryEntryCommand.Execute(vm.HistoryEntries.First(e => e.Reason == "replaced by a restore"));
        Assert.Equal(777, vm.SelectedProfile.LeftStick.MouseSpeed);        // undone
    }

    [Fact]
    public void TheObsSettingsFile_IsNeverMistakenForAConfig()
    {
        var vm = New();
        vm.ObsHost = "somewhere";
        int count = vm.Profiles.Count;

        var again = New();

        Assert.Equal(count, again.Profiles.Count);
        Assert.DoesNotContain(again.Profiles, p => p.Name == "Default Profile");
    }
}

public class ProfileHistoryTests : IDisposable
{
    private readonly string _dir = Fakes.TempProfiles();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void OnlyTheNewestCopies_AreKept()
    {
        var clock = new DateTime(2026, 1, 1, 12, 0, 0);
        var history = new ProfileHistory(_dir, () => clock = clock.AddSeconds(1));
        var profile = Profile.CreateDefault("P");

        for (int i = 0; i < ProfileHistory.MaxEntries + 5; i++)
        {
            profile.Name = $"P{i}";
            Assert.True(history.Snapshot(profile, "test"));
        }

        var list = history.List();
        Assert.Equal(ProfileHistory.MaxEntries, list.Count);
        Assert.Equal($"P{ProfileHistory.MaxEntries + 4}", list[0].ProfileName);      // newest first
        Assert.Equal("P5", list[^1].ProfileName);
    }

    [Fact]
    public void ACopy_IsIndependentOfTheLaterProfile_AndADamagedOneIsSkipped()
    {
        var history = new ProfileHistory(_dir);
        var profile = Profile.CreateDefault("P");
        profile.LeftStick.MouseSpeed = 500;
        history.Snapshot(profile, "before");
        profile.LeftStick.MouseSpeed = 900;

        File.WriteAllText(Path.Combine(_dir, "history", "00000000-bad.json"), "{ not json");

        var entry = Assert.Single(history.List());
        Assert.Equal(500, history.Load(entry)!.LeftStick.MouseSpeed);
        Assert.Null(history.Load(new HistoryEntry(Path.Combine(_dir, "history", "gone.json"), DateTime.Now, "x", "y")));
    }

    [Fact]
    public void AnUnwritableFolder_DoesNotThrow()
    {
        string file = Path.Combine(Path.GetTempPath(), "rewasd-notadir-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(file, "x");
        try
        {
            Assert.False(new ProfileHistory(file).Snapshot(Profile.CreateDefault(), "x"));   // "history" cannot be created inside a file
        }
        finally
        {
            File.Delete(file);
        }
    }
}
