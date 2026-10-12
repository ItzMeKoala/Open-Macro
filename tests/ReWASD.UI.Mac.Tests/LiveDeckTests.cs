using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.Mac.Tester;
using ReWASD.UI.Mac.Views;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

/// <summary>The live readings under the controller: a bigger window shows more detail, not a bigger controller.</summary>
public class LiveDeckTests
{
    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

    private static (MainViewModel Vm, MainWindow Window, Fakes.Pad Pad) Open(double width, double height)
    {
        var pad = new Fakes.Pad { State = Pad() };
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());
        var window = new MainWindow { DataContext = vm, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (vm, window, pad);
    }

    private static void Shot(Window window, string name)
    {
        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    }

    private static StagePanel Stage(MainWindow w) => w.GetVisualDescendants().OfType<StagePanel>().Single();
    private static LiveDeck Deck(MainWindow w) => w.GetVisualDescendants().OfType<LiveDeck>().Single();

    [Fact]
    public void TheDrawingKeepsItsSize_AndTheRestGoesToTheReadings()
    {
        Assert.Equal((500.0, 0.0), StagePanel.Split(500, true, 480, 200, 620));      // too short: all to the drawing
        Assert.Equal((480.0, 300.0), StagePanel.Split(780, true, 480, 200, 620));    // the drawing stays at 480
        Assert.Equal((680.0, 620.0), StagePanel.Split(1300, true, 480, 200, 620));   // past the most the readings need, the drawing grows
        Assert.Equal((900.0, 0.0), StagePanel.Split(900, false, 480, 200, 620));     // readings not wanted (keyboard tab)
    }

    [AvaloniaFact]
    public void AtTheDefaultSize_ThereIsNoRoom_AndNothingIsMeasured()
    {
        var (vm, window, _) = Open(1200, 720);

        Assert.False(Stage(window).IsDeckShown);
        Assert.False(vm.IsLiveDeckShown);
        Assert.False(vm.IsLiveDataOn);
        Shot(window, "front-default");
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void ABigWindow_ShowsTheReadings_UnderAControllerOfTheSameSize()
    {
        var (vm, window, pad) = Open(1500, 940);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Stage(window).IsDeckShown);
        Assert.True(vm.IsLiveDeckShown);
        Assert.True(vm.IsLiveDataOn);
        Assert.Equal(480, Stage(window).Children[0].Bounds.Height, 1);
        Assert.Equal(2, Deck(window).GetVisualDescendants().OfType<StickPlot>().Count());

        // The readings follow the controller
        pad.State = Pad(lx: 0.5, lt: 0.25, down: ControllerButton.A);
        Assert.True(WaitFor(() => vm.Tester.Model.Snapshot().Left.X > 0.4));
        vm.LiveTickForTests();
        vm.LiveTickForTests();
        Assert.Equal("+0.500", vm.Tester.Left.XText);
        Assert.Equal("25%", vm.Tester.LeftTrigger.PercentText);

        // Somewhere else, or on another tab, nothing is read
        vm.SelectedInputTab = InputTab.Keyboard;
        Assert.False(vm.IsLiveDataOn);
        vm.SelectedInputTab = InputTab.Gamepad;
        Assert.True(vm.IsLiveDataOn);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.False(vm.IsLiveDataOn);
        vm.ToggleSettingsCommand.Execute(null);

        window.Hide();
        Assert.False(vm.IsLiveDataOn);
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void ATallWindow_AddsTheGraph()
    {
        var (vm, window, pad) = Open(1700, 1100);
        vm.Shutdown();      // stop the reader: this test feeds the model itself
        var m = vm.Tester.Model;
        m.Reset();
        for (int i = 0; i < 400; i++)
        {
            double a = i * Math.PI / 60;
            m.Feed(Pad(lx: Math.Cos(a), ly: Math.Sin(a), rx: 0.3 * Math.Sin(a), ry: 0.6, lt: (i % 100) / 100.0, rt: 0.8), i * 4);
        }
        m.Feed(Pad(lx: 0.62, ly: 0.48, rx: -0.2, ry: 0.55, lt: 0.4, rt: 0.85, down: ControllerButton.A), 1700);
        vm.Tester.Refresh(m.Snapshot(), "DualSense Wireless Controller", vm.SelectedProfile.LeftStick, vm.SelectedProfile.RightStick);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Deck(window).FindControl<Border>("ScopeTile")!.IsVisible);
        Assert.True(Deck(window).FindControl<Border>("TimingTile")!.IsVisible);
        Shot(window, "front-tall");
    }

    [AvaloniaFact]
    public void AMediumWindow_ShowsTheReadingsWithoutTheGraph()
    {
        var (vm, window, _) = Open(1300, 920);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Stage(window).IsDeckShown);
        Assert.False(Deck(window).FindControl<Border>("ScopeTile")!.IsVisible);
        Shot(window, "front-medium");
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void TheFullTesterLink_OpensTheTester()
    {
        var (vm, window, _) = Open(1500, 940);
        var link = Deck(window).GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Full tester");
        link.Command!.Execute(null);

        Assert.NotNull(window.Tester);
        window.CloseTester();
        Assert.Null(window.Tester);
        Assert.NotEqual(WindowState.FullScreen, window.WindowState);
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void TheBottomBar_NamesTheController_WithoutDecoration()
    {
        var (vm, window, _) = Open(1200, 720);
        var text = window.FindControl<TextBlock>("DeviceText")!;
        Assert.Equal("Test Controller", text.Text);
        Assert.True(vm.IsControllerConnected);
        vm.Shutdown();
    }

    private static bool WaitFor(Func<bool> condition, int ms = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }
        return condition();
    }
}
