using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class InspectorTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
    private static ButtonViewModel Pad(MainViewModel vm, ControllerButton b) => vm.ControllerButtons.First(x => x.Button == b);

    [Fact]
    public void ThereAreSixTriggers_InTheOrderThePanelShowsThem_WithPlainNames()
    {
        var vm = New();

        Assert.Equal(new[] { "Press", "Hold", "Double", "Triple", "On down", "On release" }, vm.ActivatorChoices.Select(c => c.Label));
        Assert.All(vm.ActivatorChoices, c => Assert.False(string.IsNullOrWhiteSpace(c.Help)));
    }

    [Fact]
    public void TheChosenTrigger_IsMarked_AndChoosingOneSwitchesTheEditor()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        Assert.Equal(new[] { true, false, false, false, false, false }, vm.ActivatorChoices.Select(c => c.IsSelected));

        vm.ChooseActivatorCommand.Execute(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Double));

        Assert.Equal(ActivatorType.Double, vm.SelectedActivator);
        Assert.Equal(1, vm.ActivatorChoices.Count(c => c.IsSelected));
        Assert.True(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Double).IsSelected);
    }

    [Fact]
    public void ADotShowsWhichTriggersAlreadyDoSomething()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.X));
        Assert.True(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Single).HasMapping);
        Assert.False(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Long).HasMapping);

        vm.ChooseActivatorCommand.Execute(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Long));
        vm.TargetMode = TargetMode.Remap;
        vm.SelectedTargetType = TargetType.KeyboardKey;
        vm.SelectedKeyboardKey = "F";
        vm.SaveCurrentMappingCommand.Execute(null);

        Assert.True(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Long).HasMapping);
        Assert.False(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Double).HasMapping);

        vm.SelectButton(Pad(vm, ControllerButton.Y));                                   // another input: its own dots
        Assert.False(vm.ActivatorChoices.First(c => c.Type == ActivatorType.Long).HasMapping);
    }

    [Fact]
    public void TheSummary_SaysWhatTheInputDoesNow_InPlainWords()
    {
        var vm = New();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        Assert.StartsWith("Not remapped", vm.SelectedSummary);
        Assert.False(vm.SelectedIsRemapped);

        vm.SelectedTargetType = TargetType.KeyboardKey;
        vm.SelectedKeyboardKey = "Space";
        vm.SaveCurrentMappingCommand.Execute(null);

        Assert.True(vm.SelectedIsRemapped);
        Assert.Equal("Key: Space", vm.SelectedSummary);

        vm.ResetCurrentMappingCommand.Execute(null);
        Assert.False(vm.SelectedIsRemapped);
        Assert.StartsWith("Not remapped", vm.SelectedSummary);
    }

    [Fact]
    public void EveryBehaviour_HasAnExplanation_ThatFollowsTheChoice()
    {
        var vm = New();
        foreach (var mode in vm.AvailableModes)
        {
            vm.SelectedMode = mode;
            Assert.False(string.IsNullOrWhiteSpace(vm.ModeDescription), mode.ToString());
        }
        vm.SelectedMode = MappingMode.Toggle;
        Assert.Contains("switches it on or off", vm.ModeDescription);
    }

    [AvaloniaFact]
    public void ThePanel_ShowsTheSameThingsThePhotoOfItSuggested_AndNothingMore()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        vm.SelectButton(Pad(vm, ControllerButton.RightBumper));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();

        Assert.Contains("Right Bumper", texts);
        foreach (var label in new[] { "Trigger", "Action", "Send", "Behaviour" }) Assert.Contains(label, texts);
        Assert.Contains("Press", texts);
        Assert.Contains("On release", texts);
        Assert.Contains("Stays pressed for as long as you hold the button.", texts);

        // The clutter is gone: no jargon labels, no "Currently:", no version or permission chips in the bottom bar
        Assert.DoesNotContain("WHEN", texts);
        Assert.DoesNotContain("DO", texts);
        Assert.DoesNotContain(texts, t => t != null && t.StartsWith("Currently"));
        Assert.DoesNotContain(texts, t => t != null && t.StartsWith("v0."));
        Assert.DoesNotContain(texts, t => t != null && t.StartsWith("Permissions:"));
    }

    [AvaloniaFact]
    public void TheTriggerChips_ClickThrough()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        vm.SelectButton(Pad(vm, ControllerButton.A));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var chips = window.GetVisualDescendants().OfType<Button>().Where(b => b is not RadioButton && b.Classes.Contains("seg")).ToList();
        Assert.Equal(6, chips.Count);

        var hold = chips.First(c => c.DataContext is ActivatorChoice { Type: ActivatorType.Long });
        hold.Command!.Execute(hold.CommandParameter);

        Assert.Equal(ActivatorType.Long, vm.SelectedActivator);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(hold.Classes.Contains("sel"));
    }

    [AvaloniaFact]
    public void TheBottomBar_OnlyShowsWhatNeedsAttention()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.DoesNotContain(texts, t => t != null && t.StartsWith("Needs permission"));
        Assert.DoesNotContain(texts, t => t != null && t.Contains("ViGEmBus"));
    }
}
