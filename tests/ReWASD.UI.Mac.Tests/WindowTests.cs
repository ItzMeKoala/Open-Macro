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

        // The keyboard is the detailed drawing, with a part for every key that can be remapped
        vm.SelectedInputTab = InputTab.Keyboard;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var keyboard = window.GetVisualDescendants().OfType<Art.ArtView>().Single(v => v.Name == "KeyboardArt");
        Assert.True(keyboard.IsEffectivelyVisible);
        Assert.All(vm.KeyboardSources, k => Assert.NotNull(keyboard.Inspect(k.SourceName)));
        Shot(window, "keyboard");

        vm.SelectedInputTab = InputTab.Mouse;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var mouseArt = window.GetVisualDescendants().OfType<Art.ArtView>().Single(v => v.Name == "MouseArt");
        Assert.True(mouseArt.IsEffectivelyVisible);
        Assert.All(vm.MouseSources, m => Assert.NotNull(mouseArt.Inspect(m.SourceName)));
        Shot(window, "mouse");
    }

    [AvaloniaFact]
    public void MouseParts_CanEachBeSelectedWithoutOverlapping()
    {
        var vm = NewViewModel();
        var window = Show(vm);
        try
        {
            vm.SelectedInputTab = InputTab.Mouse;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var view = window.GetVisualDescendants().OfType<Art.ArtView>().Single(v => v.Name == "MouseArt");
            var root = view.GetVisualDescendants().OfType<Viewbox>().First().Child!;

            void Click(double x, double y)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();          // lay out anything the last click changed first
                var point = Avalonia.VisualExtensions.TranslatePoint(root, new Avalonia.Point(x, y), window)!.Value;
                window.MouseDown(point, Avalonia.Input.MouseButton.Left);
                window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            }

            // The middle of each button's own clickable area, read from the drawing
            var scene = ReWASD.Core.Skins.Art.ArtLibrary.Mouse;
            foreach (var name in new[] { "Left Click", "Right Click", "Wheel Up", "Middle Click", "Wheel Down", "Mouse Button 5", "Mouse Button 4" })
            {
                var b = scene.PartForKey(name)!.HitBounds;
                double x = b.CenterX, y = b.CenterY;
                if (name is "Left Click" or "Right Click") { x = name == "Left Click" ? 210 : 390; y = 240; }     // the buttons are not boxes
                Click(x, y);
                Assert.True(name == vm.SelectedButton!.SourceName, $"Clicking {name} at ({x}, {y}) selected {vm.SelectedButton.SourceName}; part there: {view.KeyAt(x, y) ?? "none"}");
            }

            Click(300, 650); // the palm: not a button
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
        vm.SettingsSection = SettingsSection.System;
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
