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
        vm.SelectedSkin = ReWASD.Core.Skins.SkinKind.SwitchPro;   // a skin with only the outline drawing
        var window = Show(vm);

        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("src") && b.IsEffectivelyVisible).ToList();
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
    public void MouseRegions_CanEachBeSelectedWithoutOverlapping()
    {
        var vm = NewViewModel();
        var window = Show(vm);
        try
        {
            vm.SelectedInputTab = InputTab.Mouse;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var surface = window.GetVisualDescendants().OfType<Views.InputSurface>().Single();
            var drawing = surface.GetVisualDescendants().OfType<Viewbox>().Single(v => v.IsEffectivelyVisible).Child!;

            void Click(double x, double y)
            {
                var point = Avalonia.VisualExtensions.TranslatePoint(drawing, new Avalonia.Point(x, y), window)!.Value;
                window.MouseDown(point, Avalonia.Input.MouseButton.Left);
                window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            }

            foreach (var (name, x, y) in new[]
            {
                ("Right Click", 410.0, 125.0), ("Left Click", 290.0, 125.0),
                ("Wheel Up", 350.0, 87.0), ("Middle Click", 350.0, 120.0),
                ("Wheel Down", 350.0, 153.0), ("Mouse Button 5", 268.0, 241.0),
                ("Mouse Button 4", 268.0, 283.0)
            })
            {
                Click(x, y);
                Assert.Equal(name, vm.SelectedButton!.SourceName);
            }

            Click(350, 330); // unmapped body: no source should claim it
            Assert.Equal("Mouse Button 4", vm.SelectedButton!.SourceName);
        }
        finally
        {
            window.Close();
            vm.Shutdown();
        }
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
