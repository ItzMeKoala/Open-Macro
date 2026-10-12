using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class UxTests
{
    private static (MainViewModel Vm, MainWindow Window, string Dir) Open()
    {
        string dir = Fakes.TempProfiles();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), dir);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (vm, window, dir);
    }

    private static List<string?> VisibleTexts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();

    // ---- settings -------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Settings_ShowsOneSectionAtATime_AndTheListSwitchesBetweenThem()
    {
        var (vm, window, _) = Open();
        vm.ToggleSettingsCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var sticks = VisibleTexts(window);
        Assert.Contains("Sticks", sticks);
        Assert.Contains(sticks, t => t != null && t.StartsWith("LEFT STICK"));
        Assert.DoesNotContain("PERMISSIONS", sticks);
        Assert.DoesNotContain("HISTORY", sticks);
        Assert.DoesNotContain(sticks, t => t != null && t.StartsWith("COMBOS"));

        vm.SettingsSection = SettingsSection.Configs;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var configs = VisibleTexts(window);
        Assert.Contains("HISTORY", configs);
        Assert.Contains("SAVING", configs);
        Assert.DoesNotContain(configs, t => t != null && t.StartsWith("LEFT STICK"));
        Assert.Contains("Configs", configs);

        vm.SettingsSection = SettingsSection.Combos;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var combos = VisibleTexts(window);
        Assert.Contains(combos, t => t != null && t.StartsWith("COMBOS"));
        Assert.Contains(combos, t => t != null && t.StartsWith("COMMAND WHEEL"));
        Assert.Contains(combos, t => t != null && t.StartsWith("ACTIVATOR TIMING"));
    }

    [AvaloniaFact]
    public void EverySettingThatExisted_IsStillReachable()
    {
        var (vm, window, _) = Open();
        vm.ToggleSettingsCommand.Execute(null);

        var seen = new HashSet<string>();
        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            vm.SettingsSection = section;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            foreach (var t in VisibleTexts(window))
                if (t != null && t.Length > 3 && t[..3].ToUpperInvariant() == t[..3] && char.IsLetter(t[0])) seen.Add(t);
        }

        foreach (var heading in new[]
        {
            "PERMISSIONS", "UPDATES", "SAFETY", "USAGE (THIS COMPUTER ONLY)", "OBS STUDIO (FOR OBS: MACRO STEPS)", "PROFILES AND LOCK", "HISTORY",
            "SAVING", "KEYBOARD SHORTCUTS", "OVERLAY (FOR STREAMS)", "BACKGROUND", "ON THIS MAC"
        })
            Assert.Contains(heading.ToUpperInvariant(), seen.Select(x => x.ToUpperInvariant()));
        Assert.Contains(seen, t => t.StartsWith("LEFT STICK"));
        Assert.Contains(seen, t => t.StartsWith("RIGHT STICK"));
        Assert.Contains(seen, t => t.StartsWith("COMBOS"));
        Assert.Contains(seen, t => t.StartsWith("COMMAND WHEEL"));
        Assert.Contains(seen, t => t.StartsWith("ACTIVATOR TIMING"));
    }

    [AvaloniaFact]
    public void TheSectionTitle_AndSummary_FollowTheChoice()
    {
        var (vm, _, _) = Open();
        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            vm.SettingsSection = section;
            Assert.False(string.IsNullOrWhiteSpace(vm.SettingsSectionTitle));
            Assert.EndsWith(".", vm.SettingsSectionSummary);
        }
        vm.SettingsSection = SettingsSection.Combos;
        Assert.Equal("Combos and timing", vm.SettingsSectionTitle);
    }

    [AvaloniaFact]
    public void SettingsCoverTheEditor_AndGoAwayAgain()
    {
        var (vm, window, _) = Open();
        Assert.DoesNotContain("Settings", VisibleTexts(window).Where(t => t == "Back to mapping"));

        vm.ToggleSettingsCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Contains("Back to mapping", VisibleTexts(window));

        vm.ToggleSettingsCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("Back to mapping", VisibleTexts(window));
    }

    // ---- getting started --------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void ANewcomer_SeesTheSteps_UntilTheyDismissThem_ForGood()
    {
        var (vm, window, dir) = Open();
        Assert.True(vm.ShowGettingStarted);
        Assert.Contains(VisibleTexts(window), t => t != null && t.StartsWith("New here?"));

        vm.DismissGettingStartedCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(VisibleTexts(window), t => t != null && t.StartsWith("New here?"));

        var again = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), dir);
        Assert.False(again.ShowGettingStarted);
        Directory.Delete(dir, true);
    }

    // ---- the editor ---------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void TheSaveButton_IsOnlyOfferedWhenThereIsSomethingToSave()
    {
        var (vm, window, _) = Open();
        var save = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("primary"));

        Assert.False(save.IsEffectivelyEnabled);
        Assert.Equal("Saved", save.Content);

        vm.SelectedTargetType = TargetType.KeyboardKey;
        vm.SelectedKeyboardKey = "Q";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(save.IsEffectivelyEnabled);
        Assert.Equal("Save change", save.Content);
        Assert.Contains(VisibleTexts(window), t => t != null && t.StartsWith("Changes are also saved"));
    }

    [AvaloniaFact]
    public void TheBarsSayWhatTheyDo()
    {
        var (vm, window, _) = Open();
        var texts = VisibleTexts(window);

        Assert.Contains("Tester", texts);
        Assert.Contains("Settings", texts);
        Assert.Contains("Start remapping", texts);
        Assert.Contains("Saved", texts);
        Assert.Contains("Remapping is off.", texts);
        Assert.DoesNotContain("APPLY", texts);

        vm.IsRemappingActive = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Contains("Stop remapping", VisibleTexts(window));
    }

    // ---- shortcuts ----------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void TheShortcuts_Work()
    {
        var (vm, window, _) = Open();

        window.KeyPress(Key.OemComma, RawInputModifiers.Meta, PhysicalKey.Comma, ",");
        Assert.True(vm.IsPreferencesPage);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(vm.IsPreferencesPage);

        vm.SelectedProfile.LeftStick.MouseSpeed = 4242;
        window.KeyPress(Key.S, RawInputModifiers.Meta, PhysicalKey.S, "s");
        Assert.Contains("Saved config", vm.StatusMessage);
    }
}
