using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

/// <summary>Loads the real window headlessly: XAML mistakes only show up when it is loaded, so this catches them.</summary>
public class WindowTests
{
    private static MainViewModel NewViewModel(Fakes.Pad? pad = null) =>
        new(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());

    private static MainWindow Show(MainViewModel vm)
    {
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return window;
    }

    /// <summary>Saves a picture of the window when REWASD_SHOTS names a folder, so the screens can be looked at.</summary>
    private static void Shot(MainWindow window, string name)
    {
        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    }

    [AvaloniaFact]
    public void TheWindowOpens_AndDrawsEveryControllerButton()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("src")).ToList();
        Assert.Equal(vm.ControllerButtons.Count, buttons.Count);
        Assert.Equal("Default Controller Profile", vm.SelectedProfile.Name);
        Shot(window, "gamepad");
    }

    [AvaloniaFact]
    public void KeyboardAndMouseTabs_ShowTheirOwnSources()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        vm.SelectedInputTab = InputTab.Keyboard;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(vm.KeyboardSources.Count, window.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("src")));
        Shot(window, "keyboard");

        vm.SelectedInputTab = InputTab.Mouse;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(vm.MouseSources.Count, window.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("src")));
        Shot(window, "mouse");
    }

    [AvaloniaFact]
    public void PreferencesPage_ExplainsMacPermissions()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        vm.ToggleSettingsCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("PERMISSIONS", texts);
        Assert.DoesNotContain("DRIVERS", texts);
        Assert.Contains(texts, t => t != null && t.StartsWith("Panic key: Control + Option"));
        Shot(window, "preferences");
    }

    [AvaloniaFact]
    public void MacHidesTheWindowsOnlyControls()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        // The virtual-controller and hide-controller toggles have nothing to do on a Mac
        var toggles = window.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("icon") && t.IsEffectivelyVisible).ToList();
        Assert.DoesNotContain(toggles, t => t.Name == "OutputToggle");
    }

    [AvaloniaFact]
    public void RemapMenu_ListsEveryGroup_AndPickingAnOptionClosesIt()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        Assert.Equal(new[] { "Nothing", "Controller (Windows only)", "Keyboard", "Mouse", "Layers" }, vm.RemapGroups.Select(g => g.Name));

        vm.IsRemapMenuOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Shot(window, "remap-menu");

        var space = vm.RemapOptions.First(o => o.Kind == RemapKind.Key && o.Name == "Space");
        vm.ChooseRemapOptionCommand.Execute(space);

        Assert.False(vm.IsRemapMenuOpen);
        Assert.Equal(space, vm.SelectedRemapOption);
        Assert.Equal(TargetType.KeyboardKey, vm.SelectedTargetType);
        Assert.Equal("Space", vm.SelectedKeyboardKey);
    }

    [AvaloniaFact]
    public void ConfigAndLayerMenus_Open()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        vm.IsConfigMenuOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Shot(window, "config-menu");
        vm.IsConfigMenuOpen = false;

        vm.ActiveShiftLayer = 1;
        vm.IsLayerMenuOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Shot(window, "layer-menu");
    }

    [AvaloniaFact]
    public void MacroPanel_Renders()
    {
        var vm = NewViewModel();
        var window = Show(vm);

        vm.TargetMode = TargetMode.Macro;
        vm.MacroScript = "KeyDown:W; Delay:50; KeyUp:W";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Shot(window, "macro");
    }
}
