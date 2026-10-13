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
        var model = new TesterModel();
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
    public void TheButtons_AreSplitBetweenTheLeftAndRightHalvesOfThePad()
    {
        var vm = new TesterViewModel();

        var left = vm.LeftButtons.Select(b => b.Button).ToList();
        var right = vm.RightButtons.Select(b => b.Button).ToList();

        Assert.Equal(new[] { ControllerButton.LeftBumper, ControllerButton.DpadUp, ControllerButton.DpadDown, ControllerButton.DpadLeft, ControllerButton.DpadRight,
            ControllerButton.LeftStickClick, ControllerButton.View, ControllerButton.Guide }.OrderBy(b => b), left.OrderBy(b => b));
        Assert.Equal(new[] { ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y, ControllerButton.RightBumper,
            ControllerButton.RightStickClick, ControllerButton.Menu }.OrderBy(b => b), right.OrderBy(b => b));
        Assert.Equal(TesterModel.ListedButtons.Length, left.Count + right.Count);        // every button is on exactly one side
        Assert.Empty(left.Intersect(right));
        Assert.All(vm.LeftButtons, r => Assert.Same(r, vm.Buttons.Single(b => b.Button == r.Button)));    // the very same rows, so they update
    }

    [Fact]
    public void TheCircleText_GuidesThePerson_AndGradesTheResult()
    {
        var model = new TesterModel();
        var vm = new TesterViewModel(model);
        model.Feed(Pad(), 0);
        vm.Refresh(model.Snapshot(), "x", null, null);
        Assert.Equal("Roll the stick slowly around its edge, all the way out", vm.Left.CircleText);

        for (int deg = 0; deg < 180; deg += 2)
            model.Feed(Pad(lx: Math.Cos(deg * Math.PI / 180), ly: Math.Sin(deg * Math.PI / 180)), 10 + deg);
        vm.Refresh(model.Snapshot(), "x", null, null);
        Assert.StartsWith("Keep going", vm.Left.CircleText);
        Assert.Equal("—", vm.Left.RoundnessText);

        for (int deg = 180; deg < 360; deg += 2)
            model.Feed(Pad(lx: Math.Cos(deg * Math.PI / 180), ly: Math.Sin(deg * Math.PI / 180)), 10 + deg);
        vm.Refresh(model.Snapshot(), "x", null, null);
        Assert.Equal("Very round", vm.Left.CircleText);
        Assert.Equal("0.0%", vm.Left.RoundnessText);           // no error from a perfect circle
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

    private static MainViewModel RumbleVm(FakeRumble rumble) =>
        new(Fakes.Platform(PlatformServices.WindowsInfo, rumble: rumble), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void Vibration_IsOnlyOfferedWhereItWorks_AndTheMotorButtonsShakeBriefly()
    {
        var mute = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo with { SupportsRumble = false }), new Fakes.Ui(), Fakes.TempProfiles());
        Assert.False(mute.CanVibrate);
        mute.VibrateLeftCommand.Execute(null);                       // does nothing, harmlessly
        mute.VibrateBoth = true;
        Assert.True(new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles()).CanVibrate);

        var rumble = new FakeRumble();
        var vm = RumbleVm(rumble);
        Assert.True(vm.CanVibrate);

        vm.VibrateLeftCommand.Execute(null);
        Assert.Equal((1.0, 0.0), rumble.Snapshot()[0]);
        Assert.True(WaitFor(() => rumble.Snapshot().Count >= 2, 3000));
        Assert.Equal((0.0, 0.0), rumble.Snapshot()[1]);              // it stopped by itself

        vm.VibrateRightCommand.Execute(null);
        Assert.Equal((0.0, 1.0), rumble.Snapshot()[2]);
    }

    [Fact]
    public void TheBothMotorsSwitch_ShakesBothAtOnce_ForTheSameTimeAsOneMotor_AndFlipsBackByItself()
    {
        var rumble = new FakeRumble();
        var vm = RumbleVm(rumble);

        vm.VibrateBoth = true;                                       // no other button needed
        Assert.Equal((1.0, 1.0), rumble.Snapshot()[0]);

        Assert.True(WaitFor(() => rumble.Snapshot().Count >= 2, 3000));
        Assert.Equal((0.0, 0.0), rumble.Snapshot()[1]);              // stopped by itself, like the single motors
        Assert.True(WaitFor(() => !vm.VibrateBoth, 1000));           // and the switch is ready to be used again
        Assert.Equal(2, rumble.Snapshot().Count);
    }

    [Fact]
    public void TheInfiniteSwitch_OnlyChangesHowLongTheOtherButtonsShake()
    {
        var rumble = new FakeRumble();
        var vm = RumbleVm(rumble);

        vm.VibrateContinuously = true;
        Assert.Empty(rumble.Snapshot());                             // on its own it shakes nothing

        vm.VibrateLeftCommand.Execute(null);
        Assert.Equal((1.0, 0.0), rumble.Snapshot()[0]);
        Thread.Sleep(1200);                                          // well past the usual short shake
        Assert.Single(rumble.Snapshot());                            // nothing stopped it

        vm.VibrateBoth = true;                                       // both motors take over, and keep going too
        Assert.Equal((1.0, 1.0), rumble.Snapshot()[^1]);
        Thread.Sleep(1200);
        Assert.Equal((1.0, 1.0), rumble.Snapshot()[^1]);
        Assert.True(vm.VibrateBoth);

        vm.VibrateContinuously = false;                              // switching it off ends the shaking, and flips both motors off too
        Assert.Equal((0.0, 0.0), rumble.Snapshot()[^1]);
        Assert.False(vm.VibrateBoth);
    }

    [Fact]
    public void ABigMotorButton_TakesOverFromTheBothMotorsSwitch_AndLeavingTheTesterSwitchesEverythingOff()
    {
        var rumble = new FakeRumble();
        var vm = RumbleVm(rumble);

        vm.VibrateContinuously = true;
        vm.VibrateBoth = true;
        vm.VibrateLeftCommand.Execute(null);

        Assert.False(vm.VibrateBoth);                                // one motor now, not both
        Assert.True(vm.VibrateContinuously);                         // the infinite switch is left as it was
        Assert.Equal((1.0, 0.0), rumble.Snapshot()[^1]);

        vm.IsTesterMode = true;
        vm.IsTesterMode = false;

        Assert.False(vm.VibrateContinuously);
        Assert.Equal((0.0, 0.0), rumble.Snapshot()[^1]);
        vm.Shutdown();
    }

    [Fact]
    public void TheReactionBox_StartsARound_ButAClickDuringARoundDoesNothing()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        Assert.Equal("Click here to start", vm.Tester.ReactionText);

        vm.StartReactionTestCommand.Execute(null);
        vm.Tester.Refresh(vm.Tester.Model.Snapshot(), "x", null, null);
        Assert.True(vm.Tester.ReactionWait);
        Assert.Equal("Wait for it…", vm.Tester.ReactionText);

        vm.Tester.Model.CancelReaction();                            // pretend it is still waiting, then click again
        vm.Tester.Model.StartReaction(0, new Random(1));
        vm.Tester.Refresh(vm.Tester.Model.Snapshot(), "x", null, null);
        vm.StartReactionTestCommand.Execute(null);
        Assert.True(vm.Tester.ReactionWait);                         // still the same round: the click was ignored
    }
}

public class TesterExtrasTests
{
    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

    [Fact]
    public void StepsAndTravel_ReachTheTexts()
    {
        var model = new TesterModel();
        var vm = new TesterViewModel(model);
        for (int i = 0; i < 256; i++) model.Feed(Pad(lx: (i - 128) / 128.0, lt: i / 255.0), i * 2);

        vm.Refresh(model.Snapshot(), "x", null, null);

        Assert.Equal("256 × 1 steps  ·  8-bit", vm.Left.StepsText);
        Assert.Equal("255 steps", vm.LeftTrigger.StepsText);
        Assert.Equal(TesterModel.TravelBins, vm.LeftTrigger.Travel.Length);
        Assert.True(vm.LeftTrigger.Travel.All(n => n > 0));
        Assert.Equal(TesterModel.HistogramBins, vm.Histogram.Length);
    }

    private static MainViewModel NewVm(Fakes.Ui ui) => new(Fakes.Platform(PlatformServices.MacOSInfo, new Fakes.Pad { State = Pad() }), ui, Fakes.TempProfiles());

    [Fact]
    public void Pause_FreezesTheNumbers_ButNotTheRecording()
    {
        var vm = NewVm(new Fakes.Ui());
        vm.Tester.Model.Feed(Pad(lx: 0.3), 0);
        vm.Tester.Model.Feed(Pad(lx: 0.3, down: ControllerButton.A), 10);
        vm.IsTesterMode = true;
        vm.Shutdown();                                         // stop the sampler: this test feeds the model itself
        vm.Tester.Model.Reset();
        vm.Tester.Model.Feed(Pad(lx: 0.3), 0);
        vm.Tester.Model.Feed(Pad(lx: 0.3, down: ControllerButton.A), 10);
        vm.LiveTickForTests(); vm.LiveTickForTests();
        Assert.Equal("1", vm.Tester.Buttons.First(b => b.Button == ControllerButton.A).PressesText);

        vm.ToggleTesterPauseCommand.Execute(null);
        Assert.True(vm.Tester.IsPaused);
        vm.Tester.Model.Feed(Pad(lx: 0.3), 100);
        vm.Tester.Model.Feed(Pad(lx: 0.3, down: ControllerButton.A), 120);
        vm.LiveTickForTests(); vm.LiveTickForTests();
        Assert.Equal("1", vm.Tester.Buttons.First(b => b.Button == ControllerButton.A).PressesText);        // the screen holds still

        vm.ToggleTesterPauseCommand.Execute(null);
        Assert.Equal("2", vm.Tester.Buttons.First(b => b.Button == ControllerButton.A).PressesText);        // resuming catches up
    }

    [Fact]
    public async Task CopyingTheReport_PutsItOnTheClipboard()
    {
        var ui = new Fakes.Ui();
        var vm = NewVm(ui);
        vm.Tester.Model.Feed(Pad(down: ControllerButton.B), 0);

        await vm.CopyTesterReportCommand.ExecuteAsync(null);

        Assert.StartsWith("Open-Macro controller test report", ui.Clipboard);
        Assert.Contains("Sticks", ui.Clipboard);
        Assert.DoesNotContain("Health", ui.Clipboard);
        Assert.Equal("Report copied to the clipboard", vm.Tester.Notice);
    }

    [Fact]
    public async Task SavingTheReport_WritesAFile_AndCancellingDoesNothing()
    {
        var ui = new Fakes.Ui();
        var vm = NewVm(ui);
        string path = Path.Combine(Path.GetTempPath(), "rewasd-report-" + Guid.NewGuid().ToString("N") + ".txt");

        await vm.SaveTesterReportCommand.ExecuteAsync(null);                 // cancelled: no path chosen
        Assert.Equal("", vm.Tester.Notice);

        ui.SavePath = path;
        await vm.SaveTesterReportCommand.ExecuteAsync(null);

        Assert.StartsWith("Open-Macro controller test report", File.ReadAllText(path));
        Assert.Contains("saved", vm.Tester.Notice);
        File.Delete(path);

        ui.SavePath = Path.Combine(path, "no", "such", "folder", "r.txt");
        await vm.SaveTesterReportCommand.ExecuteAsync(null);
        Assert.StartsWith("Could not save", vm.Tester.Notice);
    }
}
