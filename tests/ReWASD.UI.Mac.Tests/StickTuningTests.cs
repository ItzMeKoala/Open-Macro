using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class StickCalibratorTests
{
    [Fact]
    public void Resting_SetsTheDeadzoneJustAboveTheDrift()
    {
        var cal = new StickCalibrator();
        cal.Start(CalibrationKind.Rest, 0);

        Assert.Null(cal.Feed(0.02, 0.01, 100));
        Assert.Null(cal.Feed(0.06, -0.04, 700));      // the worst reading: 0.072 long
        Assert.Null(cal.Feed(0.01, 0.00, 1400));
        Assert.True(cal.IsRunning);
        Assert.InRange(cal.Progress, 0.9, 1.0);

        double? done = cal.Feed(0, 0, StickCalibrator.RestMs);

        Assert.Equal(StickCalibrator.RestDeadzone(Math.Sqrt(0.06 * 0.06 + 0.04 * 0.04)), done);
        Assert.InRange(done!.Value, 0.09, 0.11);
        Assert.False(cal.IsRunning);
    }

    [Fact]
    public void ARoughStick_NeverGetsADeadzoneSoBigTheStickFeelsDead()
    {
        Assert.Equal(0.45, StickCalibrator.RestDeadzone(0.9));
        Assert.Equal(0.02, StickCalibrator.RestDeadzone(0));
    }

    [Fact]
    public void Range_SetsTheOuterLimitJustUnderTheFurthestReach()
    {
        var cal = new StickCalibrator();
        cal.Start(CalibrationKind.Range, 1000);

        cal.Feed(0.5, 0.5, 1500);
        cal.Feed(0.0, 0.88, 2500);                      // this stick only reaches 0.88
        cal.Feed(-0.7, -0.4, 3000);
        Assert.True(cal.IsRunning);

        double? done = cal.Feed(0.1, 0.1, 1000 + StickCalibrator.RangeMs);

        Assert.Equal(StickCalibrator.RangeLimit(0.88), done);
        Assert.InRange(done!.Value, 0.84, 0.86);
    }

    [Theory]
    [InlineData(0.2, 0.6)]      // never below 0.6: the stick would saturate while barely moved
    [InlineData(1.5, 1.0)]
    [InlineData(1.0, 0.97)]
    public void TheOuterLimit_StaysInASaneRange(double reach, double expected) =>
        Assert.Equal(expected, StickCalibrator.RangeLimit(reach));

    [Fact]
    public void NoController_MeansNothingMeasured_AndCancelStops()
    {
        var cal = new StickCalibrator();
        cal.Start(CalibrationKind.Rest, 0);
        Assert.Equal(0.02, cal.Feed(null, null, StickCalibrator.RestMs));      // nothing seen: the smallest sensible deadzone

        cal.Start(CalibrationKind.Range, 0);
        cal.Cancel();
        Assert.Null(cal.Feed(1, 1, 10_000));
        Assert.Null(new StickCalibrator().Feed(1, 1, 0));
    }
}

public class StickTuningViewModelTests
{
    private static (MainViewModel Vm, Fakes.Pad Pad) New()
    {
        var pad = new Fakes.Pad();
        return (new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles()), pad);
    }

    [Fact]
    public void TheSlidersAndChoices_WriteIntoTheProfile()
    {
        var (vm, _) = New();
        var settings = vm.SelectedProfile.LeftStick;

        vm.LeftStickSettings.DeadzoneMin = 0.2;
        vm.LeftStickSettings.DeadzoneMax = 0.8;
        vm.LeftStickSettings.CurveIndex = (int)ResponseCurveType.Smooth;
        vm.LeftStickSettings.Sensitivity = 1.5;
        vm.LeftStickSettings.InvertX = true;
        vm.LeftStickSettings.InvertY = true;

        Assert.Equal(0.2, settings.DeadzoneMin);
        Assert.Equal(0.8, settings.DeadzoneMax);
        Assert.Equal(ResponseCurveType.Smooth, settings.CurveType);
        Assert.Equal(1.5, settings.Sensitivity);
        Assert.True(settings.InvertX);
        Assert.True(settings.InvertY);
        Assert.Equal(new StickSettings().DeadzoneMin, vm.SelectedProfile.RightStick.DeadzoneMin);   // the other stick is untouched
    }

    [Fact]
    public void TheOuterEdge_NeverSitsInsideTheInnerOne()
    {
        var (vm, _) = New();
        vm.LeftStickSettings.DeadzoneMax = 0.5;

        vm.LeftStickSettings.DeadzoneMin = 0.55;

        Assert.True(vm.SelectedProfile.LeftStick.DeadzoneMax >= 0.6 - 1e-9);
    }

    [Fact]
    public void Reset_PutsTheDefaultsBack_AndSwitchingProfileShowsEachOnesOwn()
    {
        var (vm, _) = New();
        var first = vm.SelectedProfile;
        vm.LeftStickSettings.DeadzoneMin = 0.3;
        vm.LeftStickSettings.InvertY = true;

        vm.NewProfileCommand.Execute(null);
        Assert.Equal(new StickSettings().DeadzoneMin, vm.LeftStickSettings.DeadzoneMin);
        Assert.False(vm.LeftStickSettings.InvertY);

        vm.SelectedProfile = first;
        Assert.Equal(0.3, vm.LeftStickSettings.DeadzoneMin);
        Assert.True(vm.LeftStickSettings.InvertY);

        vm.LeftStickSettings.ResetTuningCommand.Execute(null);
        Assert.Equal(new StickSettings().DeadzoneMin, first.LeftStick.DeadzoneMin);
        Assert.False(first.LeftStick.InvertY);
    }

    [Fact]
    public void TheCurveChoices_MatchWhatTheEngineKnows()
    {
        Assert.Equal(4, StickViewModel.CurveNames.Count);
        Assert.Equal(ResponseCurveType.Instant, (ResponseCurveType)(StickViewModel.CurveNames.Count - 1));
    }

    [Fact]
    public void Calibrating_NeedsAController_AndStartsWithOne()
    {
        var (vm, pad) = New();

        vm.LeftStickSettings.CalibrateRestCommand.Execute(null);
        Assert.Equal("Connect a controller first", vm.LeftStickSettings.CalibrationText);
        Assert.False(vm.LeftStickSettings.IsCalibrating);

        pad.State = new PhysicalState(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);
        vm.RightStickSettings.CalibrateRangeCommand.Execute(null);
        Assert.True(vm.RightStickSettings.IsCalibrating);
        Assert.Contains("Roll the stick", vm.RightStickSettings.CalibrationText);
        Assert.False(vm.LeftStickSettings.IsCalibrating);

        vm.LiveTickForTests();                   // feeding it a reading is harmless
        Assert.True(vm.RightStickSettings.IsCalibrating);
    }

    [Fact]
    public void TheLiveReadout_ShowsTheStick_OnlyWhilePreferencesIsOpen()
    {
        var (vm, pad) = New();
        pad.State = new PhysicalState(new HashSet<ControllerButton>(), 0.5, -0.25, 0, 0, 0, 0);

        for (int i = 0; i < 8; i++) vm.LiveTickForTests();
        Assert.Equal("", vm.LeftStickSettings.LiveText);              // the other page is showing: no work done

        vm.ToggleSettingsCommand.Execute(null);
        for (int i = 0; i < 8; i++) vm.LiveTickForTests();
        Assert.Contains("x +0.50", vm.LeftStickSettings.LiveText);
        Assert.Contains("y -0.25", vm.LeftStickSettings.LiveText);

        pad.State = null;
        for (int i = 0; i < 8; i++) vm.LiveTickForTests();
        Assert.Equal("no controller", vm.LeftStickSettings.LiveText);
    }
}
