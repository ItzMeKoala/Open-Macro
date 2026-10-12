using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Platform;
using ReWASD.Engine.Remapper;
using ReWASD.UI;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class PresetTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void EveryPreset_IsAValidSafeProfile_ThatSurvivesSaving()
    {
        Assert.NotEmpty(ProfilePresets.All);
        foreach (var preset in ProfilePresets.All)
        {
            var profile = preset.Create();
            Assert.Equal(preset.Name, profile.Name);
            Assert.False(string.IsNullOrWhiteSpace(preset.Description));
            Assert.Empty(ProfileSafety.FindRiskyActions(profile));

            var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile));
            Assert.Equal(profile.LeftStick.Mode, back.LeftStick.Mode);
            Assert.Equal(profile.ShiftLayers[0].Mappings.Count, back.ShiftLayers[0].Mappings.Count);

            // Every key a preset presses is one the key table knows
            foreach (var m in profile.ShiftLayers[0].Mappings.Values.Where(m => m.Target.Type == TargetType.KeyboardKey))
                Assert.True(ReWASD.Engine.Input.KeyMap.TryGetVirtualKey(m.Target.KeyboardKey, out _), $"{preset.Name}: unknown key {m.Target.KeyboardKey}");
            foreach (var m in profile.ShiftLayers[0].Mappings.Values.Where(m => m.Target.Type == TargetType.MouseButton))
                Assert.True(ReWASD.Engine.Input.KeyMap.TryResolveMouse(m.Target.MouseButton, out _, out _), $"{preset.Name}: unknown mouse {m.Target.MouseButton}");
        }
    }

    [Fact]
    public void ThePresetsBehaveAsTheyDescribe()
    {
        var couch = ProfilePresets.All.Single(p => p.Id == "couch").Create();
        var engine = new MappingEngine(couch, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.A }, 1, 0, 0, 1, 0, 0);
        Assert.Contains("Left Click", state.ActiveMouseButtons);
        Assert.True(state.MouseVelocityX > 0);
        Assert.True(state.ScrollVelocity > 0);

        var wasd = ProfilePresets.All.Single(p => p.Id == "wasd").Create();
        engine = new MappingEngine(wasd, () => 0) { IsActive = true };
        state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.A }, 0, 1, 1, 0, 0, 0);
        Assert.Contains("W", state.ActiveKeyboardKeys);
        Assert.Contains("Space", state.ActiveKeyboardKeys);
        Assert.True(state.MouseVelocityX > 0);
    }

    [Fact]
    public void NewFromPreset_AddsASeparateConfig_AndSelectsIt()
    {
        var vm = New();
        var before = vm.Profiles.Count;
        var preset = vm.Presets[0];

        vm.NewFromPresetCommand.Execute(preset);
        vm.NewFromPresetCommand.Execute(preset);

        Assert.Equal(before + 2, vm.Profiles.Count);
        Assert.Equal(preset.Name + " (2)", vm.SelectedProfile.Name);
        Assert.Equal(StickMode.Mouse, vm.SelectedProfile.LeftStick.Mode);
        Assert.Equal((int)StickMode.Mouse, vm.LeftStickSettings.ModeIndex);       // the window shows it
        Assert.NotEqual(vm.Profiles[^1].Id, vm.Profiles[^2].Id);
    }
}

public class UsageStatsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "rewasd-usage-" + Guid.NewGuid().ToString("N") + ".cfg");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void AHeldButton_IsOnePress_NotOnePerFrame()
    {
        var stats = new UsageStats();
        var a = new HashSet<ControllerButton> { ControllerButton.A };

        for (int i = 0; i < 100; i++) stats.Observe(a);
        stats.Observe(new HashSet<ControllerButton>());
        stats.Observe(a);

        Assert.Equal(2, stats.Presses[ControllerButton.A]);
        Assert.Equal(2, stats.TotalPresses);
    }

    [Fact]
    public void TheTopList_IsOrderedByCount_ThenName()
    {
        var stats = new UsageStats();
        var none = new HashSet<ControllerButton>();
        void Press(ControllerButton b, int times) { for (int i = 0; i < times; i++) { stats.Observe(new HashSet<ControllerButton> { b }); stats.Observe(none); } }
        Press(ControllerButton.B, 3); Press(ControllerButton.A, 3); Press(ControllerButton.X, 5); Press(ControllerButton.Y, 1);

        var top = stats.Top(3);

        Assert.Equal(new[] { ControllerButton.X, ControllerButton.A, ControllerButton.B }, top.Select(t => t.Button));
        Assert.Equal(5, top[0].Count);
    }

    [Fact]
    public void TheCounts_SurviveSaving_AndDamagedFilesStartOver()
    {
        var stats = new UsageStats { MacrosRun = 7, SecondsRemapping = 3725 };
        stats.Observe(new HashSet<ControllerButton> { ControllerButton.LeftBumper });
        Assert.True(stats.Save(_path));

        var back = UsageStats.Load(_path);
        Assert.Equal(1, back.Presses[ControllerButton.LeftBumper]);
        Assert.Equal(7, back.MacrosRun);
        Assert.Contains("1 h 2 min", back.Summary);

        File.WriteAllText(_path, "{ broken");
        Assert.Equal(0, UsageStats.Load(_path).TotalPresses);
        Assert.Equal(0, UsageStats.Load(_path + ".missing").TotalPresses);
    }

    [Fact]
    public void Reset_ClearsEverything_AndTheSummaryInvitesUse()
    {
        var stats = new UsageStats { MacrosRun = 2 };
        stats.Observe(new HashSet<ControllerButton> { ControllerButton.A });
        Assert.DoesNotContain("Nothing counted", stats.Summary);

        stats.Reset();

        Assert.Equal(0, stats.TotalPresses);
        Assert.Contains("Nothing counted", stats.Summary);
    }

    [Fact]
    public void TheViewModel_ShowsAndResetsTheCounts()
    {
        string dir = Fakes.TempProfiles();
        Directory.CreateDirectory(dir);
        var seeded = new UsageStats();
        seeded.Observe(new HashSet<ControllerButton> { ControllerButton.A });
        seeded.Save(Path.Combine(dir, "usage.cfg"));

        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), dir);
        Assert.Contains("1 button presses", vm.UsageSummary);
        Assert.Equal("A  —  1", vm.TopButtonLines.Single());

        vm.ResetUsageCommand.Execute(null);
        Assert.Empty(vm.TopButtonLines);
        Assert.Contains("Nothing counted", vm.UsageSummary);
        Directory.Delete(dir, true);
    }
}
