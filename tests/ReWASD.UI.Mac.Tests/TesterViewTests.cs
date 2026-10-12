using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.Mac.Art;
using ReWASD.UI.Mac.Tester;
using ReWASD.UI.Tester;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class TesterViewTests
{
    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

    private static (MainViewModel Vm, MainWindow Window, Fakes.Pad Pad) Open()
    {
        var pad = new Fakes.Pad();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());
        var window = new MainWindow { DataContext = vm };
        window.Show();
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

    [AvaloniaFact]
    public void GoingFullScreen_ShowsTheTester_AndLeavingHidesIt()
    {
        var (vm, window, _) = Open();
        Assert.Null(window.Tester);
        Assert.False(vm.IsTesterMode);

        window.WindowState = WindowState.FullScreen;

        Assert.NotNull(window.Tester);
        Assert.True(vm.IsTesterMode);
        Assert.True(window.FindControl<ContentControl>("TesterHost")!.IsVisible);

        window.WindowState = WindowState.Normal;

        Assert.Null(window.Tester);
        Assert.False(vm.IsTesterMode);
        Assert.False(window.FindControl<ContentControl>("TesterHost")!.IsVisible);
        Assert.Empty(window.GetVisualDescendants().OfType<StickPlot>());       // the screen is dropped, not just hidden
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void TheExitButton_LeavesFullScreen()
    {
        var (vm, window, _) = Open();
        window.WindowState = WindowState.FullScreen;

        window.Tester!.FindControl<Button>("ExitButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Null(window.Tester);
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void TheTesterHasEverythingOnIt()
    {
        var (vm, window, _) = Open();
        window.WindowState = WindowState.FullScreen;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var tester = window.Tester!;

        Assert.Equal(2, tester.GetVisualDescendants().OfType<StickPlot>().Count());
        Assert.Equal(2, tester.GetVisualDescendants().OfType<ScopeGraph>().Count());
        Assert.Single(tester.GetVisualDescendants().OfType<ArtView>());
        Assert.Equal(2, tester.GetVisualDescendants().OfType<ProgressBar>().Count());
        var texts = tester.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        foreach (var heading in new[] { "CONTROLLER TESTER", "CIRCULARITY TEST", "RIGHT STICK", "LEFT STICK", "EVENT LOG  ·  NEWEST FIRST" })
            Assert.Contains(heading, texts);
        Assert.Contains(texts, t => t == "Left bumper");                         // every button has a row
        Assert.Contains(texts, t => t != null && t.StartsWith("TRIGGERS"));
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void TheMainWindowsOwnPictureIsStillTheOnlyOneOutsideTheTester()
    {
        var (vm, window, _) = Open();
        window.WindowState = WindowState.FullScreen;
        window.WindowState = WindowState.Normal;

        Assert.Single(window.GetVisualDescendants().OfType<ArtView>());
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void TheStickPlot_DrawsTheMeasuredShapeFromTheNumbers()
    {
        var model = new TesterModel();
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180;
            model.Feed(Pad(lx: Math.Cos(a), ly: Math.Sin(a)), deg);
        }

        var snap = model.Snapshot().Left;
        var points = StickPlot.ShapePoints(snap, new Point(100, 100), 80);

        Assert.Equal(TesterModel.AngleBins, points.Count);
        Assert.All(points, p => Assert.Equal(80, Math.Sqrt((p.X - 100) * (p.X - 100) + (p.Y - 100) * (p.Y - 100)), 1));   // all on the circle
        Assert.True(points[0].Y < 100);                      // the first bin is just above the right-hand horizontal: screen y goes down
    }

    [AvaloniaFact]
    public void ARichTester_Renders()
    {
        var (vm, window, pad) = Open();
        window.WindowState = WindowState.FullScreen;
        window.Width = 1700;
        window.Height = 1450;
        vm.Shutdown();                                  // stop the sampler: this test feeds the model itself

        // Feed some lively history straight into the model
        var m = vm.Tester.Model;
        double t = 0;
        m.Feed(Pad(), t);
        for (int deg = 0; deg < 330; deg += 2)
        {
            double a = deg * Math.PI / 180, r = 0.97 - 0.04 * Math.Sin(a * 2);
            t += 8;
            m.Feed(Pad(lx: r * Math.Cos(a), ly: r * Math.Sin(a), rx: 0.3 * Math.Cos(a * 3), ry: 0.3 * Math.Sin(a * 3), lt: (Math.Sin(a) + 1) / 2, rt: (Math.Cos(a) + 1) / 3,
                down: deg % 90 < 45 ? new[] { ControllerButton.A, ControllerButton.RightBumper } : new[] { ControllerButton.X }), t);
        }
        for (int i = 0; i < 6; i++)
        {
            m.Feed(Pad(down: ControllerButton.B), t += 300);
            m.Feed(Pad(), t += 90 + i * 15);
        }
        m.Feed(Pad(down: ControllerButton.Y), t += 100);
        m.Feed(Pad(), t += 40);
        m.Feed(Pad(down: ControllerButton.Y), t += 6);          // a bounce
        m.Feed(Pad(lx: 0.9, ly: 0.2, rx: -0.4, ry: 0.5, lt: 0.6, rt: 0.2, down: ControllerButton.DpadLeft), t += 12);

        vm.LiveTickForTests();
        vm.LiveTickForTests();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Shot(window, "tester");

        Assert.True(vm.Tester.Left.Snapshot!.Coverage > 0.8);
        vm.Shutdown();
    }
}
