using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.Tester;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class TesterViewModelTests
{
    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

    private static bool WaitFor(Func<bool> condition, int ms = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }

    private sealed class FakeRumble : IRumbleOutput
    {
        public readonly List<(double L, double R)> Calls = new();
        public void Rumble(double left, double right) { lock (Calls) Calls.Add((left, right)); }
        public List<(double L, double R)> Snapshot() { lock (Calls) return Calls.ToList(); }
    }

    // ---- the texts -----------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTexts_ShowTheNumbersPlainly()
    {
        var model = new TesterModel(() => new DateTime(2026, 1, 1, 9, 30, 15, 250));
        var vm = new TesterViewModel(model);
        model.Feed(Pad(), 0);
        model.Feed(Pad(lx: 0.5, ly: -0.25, lt: 0.5, down: ControllerButton.A), 3_725_000);

        vm.Refresh(model.Snapshot(), "Xbox Wireless Controller", new StickSettings { DeadzoneMin = 0.1, DeadzoneMax = 0.9 }, new StickSettings());

        Assert.True(vm.Connected);
        Assert.Equal("Xbox Wireless Controller", vm.ControllerName);
        Assert.Equal("01:02:05", vm.SessionText);
        Assert.Equal("x +0.500    y -0.250", vm.Left.PositionText);
        Assert.Equal("0.559", vm.Left.MagnitudeText);
        Assert.Equal("333°", vm.Left.AngleText);
        Assert.Equal(0.1, vm.Left.DeadzoneMin);
        Assert.Equal(0.9, vm.Left.DeadzoneMax);
        Assert.Equal("50%", vm.LeftTrigger.PercentText);
        Assert.Equal("128 / 255", vm.LeftTrigger.RawText);
        Assert.Equal(0.5, vm.LeftTrigger.Value);
        Assert.Equal("Move a stick to measure", vm.RateText);          // one change is too few to call a rate

        var a = vm.Buttons.First(b => b.Button == ControllerButton.A);
        Assert.True(a.IsDown);
        Assert.Equal("1", a.PressesText);
        Assert.Equal("A", a.Name);
        Assert.Contains(vm.Log, l => l.EndsWith("A pressed"));
    }

    [Fact]
    public void WithoutAController_EverythingSaysSo()
    {
        var vm = new TesterViewModel();

        vm.Refresh(vm.Model.Snapshot(), "ignored", null, null);

        Assert.False(vm.Connected);
        Assert.Equal("No controller", vm.ControllerName);
        Assert.Equal("00:00:00", vm.SessionText);
        Assert.Equal("—", vm.Left.AngleText);
        Assert.Equal("0 pulls", vm.LeftTrigger.PressesText);
        Assert.Equal("not pressed", vm.RightTrigger.HeldText);
        Assert.Equal(TesterModel.ListedButtons.Length, vm.Buttons.Count);
        Assert.All(vm.Buttons, b => Assert.False(b.IsDown));
    }

    [Fact]
    public void TheCircleText_GuidesThePerson_AndGradesTheResult()
    {
        var model = new TesterModel();
        var vm = new TesterViewModel(model);
        model.Feed(Pad(), 0);
        vm.Refresh(model.Snapshot(), "x", null, null);
        Assert.Equal("Roll the stick slowly around its edge", vm.Left.CircleText);

        for (int deg = 0; deg < 180; deg += 2)
            model.Feed(Pad(lx: Math.Cos(deg * Math.PI / 180), ly: Math.Sin(deg * Math.PI / 180)), 10 + deg);
        vm.Refresh(model.Snapshot(), "x", null, null);
        Assert.StartsWith("Keep going", vm.Left.CircleText);
        Assert.Equal("—", vm.Left.RoundnessText);

        for (int deg = 180; deg < 360; deg += 2)
            model.Feed(Pad(lx: Math.Cos(deg * Math.PI / 180), ly: Math.Sin(deg * Math.PI / 180)), 10 + deg);
        vm.Refresh(model.Snapshot(), "x", null, null);
        Assert.Equal("Very round", vm.Left.CircleText);
        Assert.Equal("100.0%", vm.Left.RoundnessText);
        Assert.Equal("100%", vm.Left.CoverageText);
        Assert.Contains("max 1.000", vm.Left.RadiusText);
    }

    [Fact]
    public void ABouncingButton_IsFlaggedInItsRow()
    {
        var model = new TesterModel();
        var vm = new TesterViewModel(model);
        model.Feed(Pad(down: ControllerButton.B), 0);
        model.Feed(Pad(), 50);
        model.Feed(Pad(down: ControllerButton.B), 55);

        vm.Refresh(model.Snapshot(), "x", null, null);

        var row = vm.Buttons.First(b => b.Button == ControllerButton.B);
        Assert.True(row.HasBounce);
        Assert.Equal("1 bounce", row.BounceText);
        Assert.Equal("2", row.PressesText);
        Assert.Equal("0 ms", row.HoldText);                            // held right now, for no time yet
    }

    // ---- in the app ----------------------------------------------------------------------------------------------------

    [Fact]
    public void InTesterMode_TheControllerIsReadByTheSamplerAndShown()
    {
        var pad = new Fakes.Pad { State = Pad(lx: 0.5, down: ControllerButton.A) };
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());

        vm.IsTesterMode = true;
        Assert.True(WaitFor(() => vm.Tester.Model.Snapshot().Connected));
        vm.LiveTickForTests();
        vm.LiveTickForTests();

        Assert.True(vm.Tester.Connected);
        Assert.Equal("Test Controller", vm.Tester.ControllerName);
        Assert.True(vm.Tester.Buttons.First(b => b.Button == ControllerButton.A).IsDown);
        Assert.Equal("x +0.500    y 0.000", vm.Tester.Left.PositionText);
        Assert.True(vm.ControllerButtons.First(b => b.Button == ControllerButton.A).IsPressed);     // the normal display follows too

        pad.State = null;
        Assert.True(WaitFor(() => !vm.Tester.Model.Snapshot().Connected));

        vm.IsTesterMode = false;
        vm.Shutdown();
    }

    [Fact]
    public void LeavingTesterMode_StopsReadingTheController()
    {
        var pad = new Fakes.Pad { State = Pad() };
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());
        vm.IsTesterMode = true;
        Assert.True(WaitFor(() => vm.Tester.Model.Snapshot().Connected));

        vm.IsTesterMode = false;
        long before = vm.Tester.Model.Snapshot().Rate.TotalChanges;
        pad.State = Pad(lx: 1);                      // would be a change, if anything were still reading
        Thread.Sleep(100);

        Assert.Equal(before, vm.Tester.Model.Snapshot().Rate.TotalChanges);
        Assert.Equal(0, vm.Tester.Model.Snapshot().Left.PeakMagnitude);
    }

    [Fact]
    public void TheResetButtons_ClearTheCounts()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        vm.Tester.Model.Feed(Pad(lx: 1, rx: 1, down: ControllerButton.A), 0);
        vm.Tester.Model.Feed(Pad(), 50);

        vm.ResetTesterLeftStickCommand.Execute(null);
        Assert.Equal(0, vm.Tester.Model.Snapshot().Left.PeakMagnitude);
        Assert.Equal(1, vm.Tester.Model.Snapshot().Right.PeakMagnitude);

        vm.ResetTesterCommand.Execute(null);
        Assert.Equal(0, vm.Tester.Model.Snapshot().Right.PeakMagnitude);
        Assert.Equal(0, vm.Tester.Model.Snapshot().Buttons.First(b => b.Button == ControllerButton.A).Presses);
    }

    [Fact]
    public void Vibration_IsOnlyOfferedWhereItWorks_ShakesBriefly_AndStops()
    {
        var mac = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        Assert.False(mac.CanVibrate);
        mac.VibrateBothCommand.Execute(null);                        // does nothing, harmlessly

        var rumble = new FakeRumble();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.WindowsInfo, rumble: rumble), new Fakes.Ui(), Fakes.TempProfiles());
        Assert.True(vm.CanVibrate);

        vm.VibrateLeftCommand.Execute(null);
        Assert.Equal((1.0, 0.0), rumble.Snapshot()[0]);
        Assert.True(WaitFor(() => rumble.Snapshot().Count >= 2, 3000));
        Assert.Equal((0.0, 0.0), rumble.Snapshot()[1]);              // it stopped by itself

        vm.VibrateBothCommand.Execute(null);
        vm.StopVibrationCommand.Execute(null);
        Assert.Equal((0.0, 0.0), rumble.Snapshot()[^1]);
    }
}
