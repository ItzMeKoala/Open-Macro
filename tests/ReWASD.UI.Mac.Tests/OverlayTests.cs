using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Core.Skins;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.Mac.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class OverlayTests
{
    private static (MainViewModel Vm, MainWindow Window, Fakes.Pad Pad) Open()
    {
        var pad = new Fakes.Pad();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (vm, window, pad);
    }

    [AvaloniaFact]
    public void TheOverlay_OpensAndClosesWithTheSwitch()
    {
        var (vm, window, _) = Open();
        Assert.Null(window.Overlay);

        vm.ShowOverlay = true;
        Assert.NotNull(window.Overlay);
        Assert.True(window.Overlay!.Topmost);
        Assert.False(window.Overlay.Art.ShowEditing);
        Assert.NotNull(window.Overlay.Art.Scene);                  // the controller picture is drawn, the same one as in the editor
        Assert.True(window.Overlay.Art.IsVisible);

        vm.ShowOverlay = true;                                    // nothing doubles up
        Assert.Single(window.GetVisualDescendants().OfType<ArtView>().Where(v => v.Name != "KeyboardArt"));

        vm.ShowOverlay = false;
        Assert.Null(window.Overlay);
    }

    [AvaloniaFact]
    public void ClosingTheOverlayItself_TurnsTheSwitchOff()
    {
        var (vm, window, _) = Open();
        vm.ShowOverlay = true;

        window.Overlay!.Close();

        Assert.False(vm.ShowOverlay);
        Assert.Null(window.Overlay);
    }

    [AvaloniaFact]
    public void ClosingTheMainWindow_ClosesTheOverlayToo()
    {
        var (vm, window, _) = Open();
        vm.ShowOverlay = true;
        var overlay = window.Overlay!;

        window.Close();

        Assert.Null(window.Overlay);
        Assert.False(overlay.IsVisible);
    }

    [AvaloniaFact]
    public void TheOverlay_FollowsTheSamePressedButtons()
    {
        var (vm, window, pad) = Open();
        vm.ShowOverlay = true;
        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.A }, 0, 0, 0, 0, 0, 0);

        vm.LiveTickForTests();

        Assert.True(vm.ControllerButtons.First(b => b.Button == ControllerButton.A).IsPressed);
        Assert.Equal("A", vm.PressedText);

        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.DpadUp, ControllerButton.LeftBumper }, 0, 0, 0, 0, 0, 0);
        vm.LiveTickForTests();
        Assert.Contains("LB", vm.PressedText);
        Assert.Contains("Up", vm.PressedText);

        pad.State = null;
        vm.LiveTickForTests();
        Assert.Equal("", vm.PressedText);
    }

    [AvaloniaFact]
    public void ASkinWithoutAPicture_ShowsThePressedButtonsAsText()
    {
        var (vm, window, pad) = Open();
        vm.SelectedSkin = SkinKind.PlayStation4;
        Assert.False(vm.OverlayHasArt);

        vm.ShowOverlay = true;
        Assert.False(window.Overlay!.Art.IsVisible);
        var text = window.Overlay.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(text.IsVisible);

        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.B }, 0, 0, 0, 0, 0, 0);
        vm.LiveTickForTests();
        Assert.Equal("B", text.Text);
    }

    [AvaloniaFact]
    public void WhileTheOverlayIsOff_NothingIsWorkedOut()
    {
        var (vm, _, pad) = Open();
        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.A }, 0, 0, 0, 0, 0, 0);

        vm.LiveTickForTests();

        Assert.Equal("", vm.PressedText);
    }

    [AvaloniaFact]
    public void EditingAPart_DoesNotGlowInTheOverlay_ButDoesInTheEditor()
    {
        var (vm, window, _) = Open();
        vm.ShowOverlay = true;
        vm.SelectButton(vm.ControllerButtons.First(b => b.Button == ControllerButton.X));

        var editor = window.GetVisualDescendants().OfType<ArtView>().Single(v => v.Name != "KeyboardArt");
        Assert.True(editor.ShowEditing);
        Assert.False(window.Overlay!.Art.ShowEditing);
    }
}

public class BackgroundTests
{
    private static (MainViewModel Vm, MainWindow Window, Fakes.Ui Ui, string Dir) Open(bool keepRunning)
    {
        var ui = new Fakes.Ui();
        string dir = Fakes.TempProfiles();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), ui, dir) { KeepRunningInBackground = keepRunning };
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (vm, window, ui, dir);
    }

    [AvaloniaFact]
    public void WithTheSwitchOn_TheCloseButtonOnlyHidesTheWindow()
    {
        var (vm, window, _, _) = Open(keepRunning: true);
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        window.Close();

        Assert.False(closed);
        Assert.False(window.IsVisible);

        window.Show();                                  // "Show ReWASD" in the tray
        Assert.True(window.IsVisible);

        vm.RequestQuit();                               // Quit in the tray
        window.Close();
        Assert.True(closed);
    }

    [AvaloniaFact]
    public void WithTheSwitchOff_TheCloseButtonCloses()
    {
        var (_, window, _, _) = Open(keepRunning: false);
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        window.Close();

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void Quit_AsksTheAppToExit()
    {
        var (vm, _, ui, _) = Open(keepRunning: true);

        vm.RequestQuit();

        Assert.True(ui.ExitRequested);
        Assert.True(vm.IsQuitting);
    }

    [AvaloniaFact]
    public void TheSettings_AreRemembered_AndTheDockIconFollows()
    {
        var (vm, _, ui, dir) = Open(keepRunning: false);
        Assert.True(vm.CanHideDockIcon);

        vm.KeepRunningInBackground = true;
        vm.HideDockIcon = true;
        Assert.Equal(true, ui.DockHidden);

        var ui2 = new Fakes.Ui();
        var again = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), ui2, dir);
        Assert.True(again.KeepRunningInBackground);
        Assert.True(again.HideDockIcon);
        Assert.Equal(true, ui2.DockHidden);                 // restored when the app starts

        again.HideDockIcon = false;
        Assert.Equal(false, ui2.DockHidden);
        Directory.Delete(dir, true);
    }

    [AvaloniaFact]
    public void OnWindows_ThereIsNoDockIconToHide()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.WindowsInfo), new Fakes.Ui(), Fakes.TempProfiles());
        Assert.False(vm.CanHideDockIcon);
    }
}

public class WheelTests
{
    private static (MainViewModel Vm, MainWindow Window) Open()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (vm, window);
    }

    private static void AddItem(MainViewModel vm, string label, string action)
    {
        vm.NewWheelLabel = label;
        vm.NewWheelAction = action;
        vm.AddWheelItemCommand.Execute(null);
    }

    [Fact]
    public void TheSettings_WriteIntoTheProfile()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

        vm.WheelButtonIndex = MainViewModel.WheelButtons.ToList().IndexOf(ControllerButton.LeftTrigger);
        vm.WheelStickIndex = 1;
        vm.WheelThreshold = 0.7;
        AddItem(vm, "Browser", "LaunchApp:Safari");
        AddItem(vm, "Notes", "OpenUrl:https://example.com");

        var wheel = vm.SelectedProfile.Wheel;
        Assert.Equal(ControllerButton.LeftTrigger, wheel.Button);
        Assert.False(wheel.UseRightStick);
        Assert.Equal(0.7, wheel.Threshold);
        Assert.Equal(new[] { "Browser", "Notes" }, wheel.Items.Select(i => i.Label));
        Assert.True(wheel.IsOn);
        Assert.Equal(2, vm.WheelItems.Count);
    }

    [Fact]
    public void BadSlices_AreRefusedWithAReason()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

        AddItem(vm, "", "KeyTap:A");
        Assert.Contains("name", vm.WheelMessage);

        AddItem(vm, "X", "Nonsense:1");
        Assert.Contains("Unknown action", vm.WheelMessage);

        for (int i = 0; i < CommandWheel.MaxItems; i++) AddItem(vm, "S" + i, "KeyTap:A");
        AddItem(vm, "One too many", "KeyTap:A");
        Assert.Contains("at most", vm.WheelMessage);
        Assert.Equal(CommandWheel.MaxItems, vm.SelectedProfile.Wheel.Items.Count);
    }

    [Fact]
    public void RemovingASlice_TakesItOut_AndEachProfileShowsItsOwnWheel()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        var first = vm.SelectedProfile;
        AddItem(vm, "A", "KeyTap:A");
        AddItem(vm, "B", "KeyTap:B");
        vm.RemoveWheelItemCommand.Execute(vm.WheelItems[0]);
        Assert.Equal(new[] { "B" }, first.Wheel.Items.Select(i => i.Label));

        vm.NewProfileCommand.Execute(null);
        Assert.Empty(vm.WheelItems);
        Assert.Equal(0, vm.WheelButtonIndex);

        vm.SelectedProfile = first;
        Assert.Equal(new[] { "B" }, vm.WheelItems.Select(i => i.Label));
    }

    [Fact]
    public void TheFrameState_OpensAndClosesTheWheelOnScreen_OnlyWhenSomethingChanges()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.WheelVisible) or nameof(MainViewModel.WheelSelectedIndex)) changes.Add(e.PropertyName!);
        };

        vm.OnWheelFrame(new ReWASD.Engine.Remapper.RemappedState());
        Assert.Empty(changes);                                                      // nothing new: no work for the window

        vm.OnWheelFrame(new ReWASD.Engine.Remapper.RemappedState { WheelOpen = true, WheelIndex = 2 });
        Assert.True(vm.WheelVisible);
        Assert.Equal(2, vm.WheelSelectedIndex);

        int count = changes.Count;
        vm.OnWheelFrame(new ReWASD.Engine.Remapper.RemappedState { WheelOpen = true, WheelIndex = 2 });
        Assert.Equal(count, changes.Count);                                         // the same frame again changes nothing

        vm.OnWheelFrame(new ReWASD.Engine.Remapper.RemappedState());
        Assert.False(vm.WheelVisible);
        Assert.Equal(-1, vm.WheelSelectedIndex);
    }

    [AvaloniaFact]
    public void TheWheelWindow_AppearsWithTheSlices_HighlightsTheChosenOne_AndGoesAway()
    {
        var (vm, window) = Open();
        AddItem(vm, "Browser", "LaunchApp:Safari");
        AddItem(vm, "Notes", "OpenUrl:https://example.com");
        AddItem(vm, "Music", "KeyTap:A");
        Assert.Null(window.Wheel);

        vm.WheelVisible = true;

        var wheel = window.Wheel!;
        Assert.NotNull(wheel);
        Assert.True(wheel.Topmost);
        Assert.False(wheel.ShowActivated);          // it must never take the keyboard from a game
        Assert.Equal(3, wheel.SliceCount);
        Assert.Equal(-1, wheel.DrawnSelection);

        vm.WheelSelectedIndex = 1;
        string? shots = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (!string.IsNullOrEmpty(shots))
        {
            Directory.CreateDirectory(shots);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            wheel.CaptureRenderedFrame()?.Save(Path.Combine(shots, "wheel.png"));
        }
        Assert.Equal(1, wheel.DrawnSelection);
        Assert.Equal("Notes", wheel.CentreText);

        vm.WheelSelectedIndex = -1;
        Assert.Equal(-1, wheel.DrawnSelection);
        Assert.Equal("", wheel.CentreText);

        vm.WheelVisible = false;
        Assert.Null(window.Wheel);
    }

    [AvaloniaFact]
    public void ClosingTheMainWindow_ClosesTheWheelToo()
    {
        var (vm, window) = Open();
        AddItem(vm, "A", "KeyTap:A");
        AddItem(vm, "B", "KeyTap:B");
        vm.WheelVisible = true;
        var wheel = window.Wheel!;

        window.Close();

        Assert.Null(window.Wheel);
        Assert.False(wheel.IsVisible);
    }
}
