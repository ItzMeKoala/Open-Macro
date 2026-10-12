using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.UI.Tester;

namespace ReWASD.UI.Mac.Tests;

public class TesterModelTests
{
    private readonly TesterModel _m = new(() => new DateTime(2026, 1, 1, 12, 0, 0, 0));

    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

    // ---- buttons --------------------------------------------------------------------------------------------------

    [Fact]
    public void APress_IsCountedAndTimed()
    {
        _m.Feed(Pad(), 0);
        _m.Feed(Pad(down: ControllerButton.A), 100);
        _m.Feed(Pad(down: ControllerButton.A), 240);
        var held = _m.Snapshot().Buttons.First(b => b.Button == ControllerButton.A);
        Assert.True(held.IsDown);
        Assert.Equal(140, held.HeldMs);

        _m.Feed(Pad(), 300);
        var done = _m.Snapshot().Buttons.First(b => b.Button == ControllerButton.A);
        Assert.False(done.IsDown);
        Assert.Equal(1, done.Presses);
        Assert.Equal(200, done.LastHoldMs);
        Assert.Equal(200, done.ShortestHoldMs);
        Assert.Equal(200, done.LongestHoldMs);
        Assert.Equal(0, done.HeldMs);
    }

    [Fact]
    public void ShortestAndLongestHolds_AreTracked()
    {
        double t = 0;
        foreach (double hold in new[] { 120.0, 40.0, 300.0 })
        {
            _m.Feed(Pad(down: ControllerButton.B), t);
            _m.Feed(Pad(), t + hold);
            t += hold + 500;
        }

        var b = _m.Snapshot().Buttons.First(x => x.Button == ControllerButton.B);
        Assert.Equal(3, b.Presses);
        Assert.Equal(40, b.ShortestHoldMs);
        Assert.Equal(300, b.LongestHoldMs);
        Assert.Equal(300, b.LastHoldMs);
        Assert.Equal(0, b.Bounces);
    }

    [Fact]
    public void ARapidReleaseAndPress_IsReportedAsBounce_ButASlowOneIsNot()
    {
        _m.Feed(Pad(down: ControllerButton.X), 0);
        _m.Feed(Pad(), 100);
        _m.Feed(Pad(down: ControllerButton.X), 108);          // 8 ms later: chatter
        _m.Feed(Pad(), 200);
        _m.Feed(Pad(down: ControllerButton.X), 400);          // a person pressing again
        _m.Feed(Pad(), 500);

        var snap = _m.Snapshot();
        var x = snap.Buttons.First(b => b.Button == ControllerButton.X);
        Assert.Equal(3, x.Presses);
        Assert.Equal(1, x.Bounces);
        Assert.Contains(snap.Log, l => l.Contains("bounce") && l.Contains("8 ms"));
    }

    [Fact]
    public void EveryListedButton_IsTrackedSeparately()
    {
        _m.Feed(Pad(down: new[] { ControllerButton.DpadUp, ControllerButton.Guide }), 0);

        var down = _m.Snapshot().Buttons.Where(b => b.IsDown).Select(b => b.Button).ToList();

        Assert.Equal(new[] { ControllerButton.DpadUp, ControllerButton.Guide }.OrderBy(x => x), down.OrderBy(x => x));
        Assert.Equal(TesterModel.ListedButtons.Length, _m.Snapshot().Buttons.Count);
        Assert.DoesNotContain(_m.Snapshot().Buttons, b => b.Button is ControllerButton.LeftTrigger or ControllerButton.LeftStickUp);
    }

    // ---- connection -------------------------------------------------------------------------------------------------

    [Fact]
    public void Connecting_AndDisconnecting_AreLogged_AndLetGoOfEverythingHeld()
    {
        _m.Feed(null, 0);
        Assert.False(_m.Snapshot().Connected);
        Assert.Empty(_m.Snapshot().Log);

        _m.Feed(Pad(down: ControllerButton.A), 100);
        Assert.True(_m.Snapshot().Connected);

        _m.Feed(null, 600);

        var snap = _m.Snapshot();
        Assert.False(snap.Connected);
        Assert.False(snap.Buttons.First(b => b.Button == ControllerButton.A).IsDown);
        Assert.Equal(500, snap.Buttons.First(b => b.Button == ControllerButton.A).LastHoldMs);
        Assert.Contains(snap.Log, l => l.EndsWith("Controller disconnected"));
        Assert.Contains(snap.Log, l => l.EndsWith("Controller connected"));
        Assert.Equal(0, snap.ElapsedMs);
    }

    [Fact]
    public void TheSessionTimer_RunsFromTheFirstReading()
    {
        _m.Feed(null, 0);
        _m.Feed(Pad(), 1000);
        _m.Feed(Pad(), 4500);

        Assert.Equal(3500, _m.Snapshot().ElapsedMs);
    }

    // ---- sticks --------------------------------------------------------------------------------------------------------

    [Fact]
    public void AStick_ReportsPositionMagnitudeAndAngle()
    {
        _m.Feed(Pad(lx: 0.6, ly: 0.8), 0);

        var s = _m.Snapshot().Left;
        Assert.Equal(0.6, s.X);
        Assert.Equal(0.8, s.Y);
        Assert.Equal(1.0, s.Magnitude, 6);
        Assert.Equal(Math.Atan2(0.8, 0.6) * 180 / Math.PI, s.AngleDegrees, 6);
        Assert.Equal(1.0, s.PeakMagnitude, 6);
        Assert.Equal(0, _m.Snapshot().Right.Magnitude);
    }

    [Fact]
    public void ARestingStick_ShowsItsDrift()
    {
        for (int i = 0; i < 100; i++) _m.Feed(Pad(rx: 0.03, ry: -0.02 + (i % 2) * 0.01), i * 5);

        var s = _m.Snapshot().Right;
        Assert.Equal(0.03, s.RestX, 6);
        Assert.Equal(-0.015, s.RestY, 6);
        Assert.Equal(100, s.RestSamples);
        Assert.Equal(Math.Sqrt(0.03 * 0.03 + 0.02 * 0.02), s.WorstRestDrift, 6);
    }

    [Fact]
    public void RollingTheStickAroundTheEdge_BuildsTheCircle()
    {
        // A perfect circle: every angle reached at full radius
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180;
            _m.Feed(Pad(lx: Math.Cos(a), ly: Math.Sin(a)), deg * 2);
        }

        var s = _m.Snapshot().Left;
        Assert.Equal(1.0, s.Coverage, 6);
        Assert.True(s.HasCircle);
        Assert.Equal(1.0, s.MaxRadius, 3);
        Assert.Equal(1.0, s.MinRadius, 3);
        Assert.Equal(1.0, s.Roundness, 3);
        Assert.Equal(1.0, s.MeanRadius, 3);
    }

    [Fact]
    public void ASquareishStick_ScoresLowerThanARoundOne()
    {
        // A stick whose gate is a square: reaches 1.0 on the axes but up to 1.41 on the diagonals (clamped to a circle by hardware would
        // be 1.0, so model a worn one: 0.8 on the diagonals)
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180;
            double r = (deg % 90 is >= 30 and <= 60) ? 0.8 : 1.0;
            _m.Feed(Pad(lx: r * Math.Cos(a), ly: r * Math.Sin(a)), deg * 2);
        }

        var s = _m.Snapshot().Left;
        Assert.Equal(1.0, s.MaxRadius, 3);
        Assert.Equal(0.8, s.MinRadius, 3);
        Assert.Equal(0.8, s.Roundness, 3);
        Assert.InRange(s.MeanRadius, 0.85, 0.95);
    }

    [Fact]
    public void AHalfFinishedCircle_SaysItNeedsMore()
    {
        for (int deg = 0; deg < 120; deg += 2)
        {
            double a = deg * Math.PI / 180;
            _m.Feed(Pad(rx: Math.Cos(a), ry: Math.Sin(a)), deg);
        }

        var s = _m.Snapshot().Right;
        Assert.InRange(s.Coverage, 0.3, 0.4);
        Assert.False(s.HasCircle);
    }

    [Fact]
    public void ResettingOneStick_LeavesTheOtherAlone()
    {
        _m.Feed(Pad(lx: 1, rx: 1), 0);
        _m.ResetStickMeasurements(left: true);

        var snap = _m.Snapshot();
        Assert.Equal(0, snap.Left.PeakMagnitude);
        Assert.Equal(1, snap.Right.PeakMagnitude);
    }

    [Fact]
    public void TheTrail_KeepsTheLatestPositions_AndSkipsStandingStill()
    {
        for (int i = 0; i < 300; i++) _m.Feed(Pad(lx: i / 300.0), i);
        for (int i = 0; i < 50; i++) _m.Feed(Pad(lx: 0.95), 300 + i);       // no movement: no new points

        var trail = _m.Snapshot().Left.Trail;
        Assert.Equal(TesterModel.TrailLength, trail.Length);
        Assert.Equal(0.95, trail[^1].X, 3);
        Assert.True(trail[0].X < trail[^1].X);
    }

    // ---- triggers (pressure) ----------------------------------------------------------------------------------------

    [Fact]
    public void ATrigger_ReportsPressurePeakAndWhereItStartsToMove()
    {
        _m.Feed(Pad(lt: 0), 0);
        _m.Feed(Pad(lt: 0.02), 10);
        _m.Feed(Pad(lt: 0.5), 100);
        _m.Feed(Pad(lt: 0.85), 200);
        _m.Feed(Pad(lt: 0.3), 400);

        var t = _m.Snapshot().LeftTrigger;
        Assert.Equal(0.3, t.Value);
        Assert.Equal(0.85, t.Peak);
        Assert.Equal(0.02, t.FirstMovement);
        Assert.Equal(1, t.Presses);
        Assert.Equal(300, t.HeldMs);              // counted from when it passed 5%

        _m.Feed(Pad(lt: 0), 500);
        Assert.Equal(0, _m.Snapshot().LeftTrigger.HeldMs);
        Assert.Contains(_m.Snapshot().Log, l => l.Contains("Left trigger released after 400 ms"));
        Assert.Equal(0, _m.Snapshot().RightTrigger.Peak);
    }

    // ---- how often it reports ----------------------------------------------------------------------------------------

    [Fact]
    public void TheReportRate_CountsOnlyRealChanges()
    {
        _m.Feed(Pad(), 0);
        for (int i = 1; i <= 250; i++)
        {
            _m.Feed(Pad(lx: i * 0.001), i * 8);                 // changes every 8 ms (125 per second)
            _m.Feed(Pad(lx: i * 0.001), i * 8 + 4);             // a repeat read of the same thing: not a change
        }

        var rate = _m.Snapshot().Rate;
        Assert.Equal(250, rate.TotalChanges);
        Assert.Equal(8, rate.AverageIntervalMs, 1);
        Assert.Equal(8, rate.WorstIntervalMs, 1);
        Assert.InRange(rate.ChangesPerSecond, 120, 126);
        Assert.True(rate.PeakChangesPerSecond >= rate.ChangesPerSecond);
    }

    [Fact]
    public void AStalledPad_ShowsAWorstIntervalAndTheRateFalls()
    {
        _m.Feed(Pad(), 0);
        for (int i = 1; i <= 20; i++) _m.Feed(Pad(lx: i * 0.01), i * 10);
        _m.Feed(Pad(lx: 0.5), 200 + 110);                       // a 110 ms hiccup
        _m.Feed(Pad(lx: 0.5), 3000);

        var rate = _m.Snapshot().Rate;
        Assert.Equal(110, rate.WorstIntervalMs, 1);
        Assert.Equal(0, rate.ChangesPerSecond);                 // nothing in the last second
    }

    // ---- the graph and the log ---------------------------------------------------------------------------------------

    [Fact]
    public void TheGraph_SamplesEveryTenMilliseconds_AndScrollsAfterSixSeconds()
    {
        for (int i = 0; i < 1000; i++) _m.Feed(Pad(lx: i / 1000.0, lt: 0.5), i * 2.5);      // 2.5 s of 400 Hz readings

        var scope = _m.Snapshot().Scope;
        Assert.Equal(6, scope.Length);
        Assert.InRange(scope[0].Length, 245, 255);              // one sample per 10 ms

        for (int i = 0; i < 4000; i++) _m.Feed(Pad(lx: 0.1), 2500 + i * 2.5);
        scope = _m.Snapshot().Scope;
        Assert.Equal(TesterModel.ScopeSamples, scope[0].Length);
        Assert.Equal(0.1, scope[0][^1]);
        Assert.Equal(0.5 * 0, scope[4][^1]);                    // the trigger channel follows too
    }

    [Fact]
    public void TheLog_IsNewestFirst_Capped_AndStamped()
    {
        _m.Feed(Pad(), 0);
        for (int i = 0; i < 150; i++)
        {
            _m.Feed(Pad(down: ControllerButton.A), i * 10 + 1);
            _m.Feed(Pad(), i * 10 + 5);
        }

        var snap = _m.Snapshot();
        Assert.Equal(TesterModel.MaxLog, snap.Log.Count);
        Assert.StartsWith("12:00:00.000  ", snap.Log[0]);
        Assert.Contains("A released", snap.Log[0]);
        Assert.Contains("A pressed", snap.Log[1]);
    }

    [Fact]
    public void Reset_ClearsTheCounts_ButKeepsTheConnection()
    {
        _m.Feed(Pad(down: ControllerButton.A, lx: 1, lt: 0.9), 0);
        _m.Feed(Pad(), 100);

        _m.Reset();

        var snap = _m.Snapshot();
        Assert.True(snap.Connected);
        Assert.Equal(0, snap.Buttons.First(b => b.Button == ControllerButton.A).Presses);
        Assert.Equal(0, snap.Left.PeakMagnitude);
        Assert.Equal(0, snap.LeftTrigger.Peak);
        Assert.Empty(snap.Log);
        Assert.Equal(0, snap.Rate.TotalChanges);
        Assert.Empty(snap.Scope[0]);
    }

    [Fact]
    public void TheNames_AreInControllerWords()
    {
        Assert.Equal("Left bumper", TesterModel.NameOf(ControllerButton.LeftBumper));
        Assert.Equal("A", TesterModel.NameOf(ControllerButton.A));
        Assert.All(TesterModel.ListedButtons, b => Assert.False(string.IsNullOrWhiteSpace(TesterModel.NameOf(b))));
    }

    // ---- jitter and the reaction game ----------------------------------------------------------------------------------

    [Fact]
    public void ASteadyPad_HasNoJitter_AndAnUnevenOneDoes()
    {
        _m.Feed(Pad(), 0);
        for (int i = 1; i <= 100; i++) _m.Feed(Pad(lx: i * 0.001), i * 8);
        var steady = _m.Snapshot().Rate;
        Assert.Equal(0, steady.JitterMs, 3);
        Assert.Equal(1.0, steady.SteadyShare, 3);

        var uneven = new TesterModel();
        uneven.Feed(Pad(), 0);
        double t = 0;
        for (int i = 1; i <= 100; i++) uneven.Feed(Pad(lx: i * 0.001), t += i % 2 == 0 ? 4 : 12);
        var rate = uneven.Snapshot().Rate;
        Assert.Equal(4, rate.JitterMs, 1);
        Assert.Equal(0, rate.SteadyShare, 3);
    }

    private sealed class FixedRandom : Random
    {
        private readonly int _value;
        public FixedRandom(int value) => _value = value;
        public override int Next(int minValue, int maxValue) => _value;
    }

    [Fact]
    public void TheReactionGame_WaitsThenTimesTheFirstPress()
    {
        _m.Feed(Pad(), 0);
        _m.StartReaction(100, new FixedRandom(2000));
        Assert.Equal(ReactionState.Waiting, _m.Snapshot().Reaction.State);

        _m.Feed(Pad(), 1000);
        Assert.Equal(ReactionState.Waiting, _m.Snapshot().Reaction.State);

        _m.Feed(Pad(), 2100);                                      // the signal
        Assert.Equal(ReactionState.Go, _m.Snapshot().Reaction.State);

        _m.Feed(Pad(), 2200);
        _m.Feed(Pad(down: ControllerButton.A), 2342);

        var r = _m.Snapshot().Reaction;
        Assert.Equal(ReactionState.Result, r.State);
        Assert.Equal(242, r.LastMs);
        Assert.Equal(242, r.BestMs);
        Assert.Equal(1, r.Attempts);
        Assert.Contains(_m.Snapshot().Log, l => l.EndsWith("Reaction test: 242 ms"));
    }

    [Fact]
    public void ApressBeforeTheSignal_IsTooEarly_AndATriggerCountsAsAPress()
    {
        _m.Feed(Pad(), 0);
        _m.StartReaction(0, new FixedRandom(3000));
        _m.Feed(Pad(down: ControllerButton.B), 500);
        Assert.Equal(ReactionState.TooEarly, _m.Snapshot().Reaction.State);
        Assert.Equal(0, _m.Snapshot().Reaction.Attempts);

        _m.Feed(Pad(), 600);
        _m.StartReaction(600, new FixedRandom(1000));
        _m.Feed(Pad(), 1700);
        Assert.Equal(ReactionState.Go, _m.Snapshot().Reaction.State);
        _m.Feed(Pad(rt: 0.9), 1850);
        Assert.Equal(150, _m.Snapshot().Reaction.LastMs);
    }

    [Fact]
    public void RoundsKeepBestAndAverage_AndAButtonHeldFromBeforeDoesNotCount()
    {
        _m.Feed(Pad(down: ControllerButton.A), 0);                 // already held when the game starts: not a new press
        _m.StartReaction(0, new FixedRandom(1000));
        _m.Feed(Pad(down: ControllerButton.A), 1000);              // the signal, with A still held
        Assert.Equal(ReactionState.Go, _m.Snapshot().Reaction.State);
        _m.Feed(Pad(), 1100);                                      // letting go is not a press either
        Assert.Equal(ReactionState.Go, _m.Snapshot().Reaction.State);
        _m.Feed(Pad(down: ControllerButton.X), 1300);              // 300 ms after the signal
        Assert.Equal(300, _m.Snapshot().Reaction.LastMs);
        _m.Feed(Pad(), 1400);

        double t = 2000;
        foreach (double reaction in new[] { 200.0, 250.0 })
        {
            _m.StartReaction(t, new FixedRandom(1000));
            _m.Feed(Pad(), t += 1000);                              // the signal
            _m.Feed(Pad(down: ControllerButton.X), t += reaction);
            _m.Feed(Pad(), t += 100);
            t += 500;
        }

        var r = _m.Snapshot().Reaction;
        Assert.Equal(3, r.Attempts);
        Assert.Equal(200, r.BestMs);
        Assert.Equal(250, r.AverageMs);
        Assert.Equal(250, r.LastMs);
    }

    [Fact]
    public void ResetAndCancel_EndTheGame()
    {
        _m.Feed(Pad(), 0);
        _m.StartReaction(0, new FixedRandom(1500));
        _m.CancelReaction();
        _m.Feed(Pad(), 5000);
        Assert.Equal(ReactionState.Idle, _m.Snapshot().Reaction.State);

        _m.StartReaction(5000, new FixedRandom(1000));
        _m.Reset();
        Assert.Equal(ReactionState.Idle, _m.Snapshot().Reaction.State);
        Assert.Equal(0, _m.Snapshot().Reaction.Attempts);
    }

    [Fact]
    public void ATimeThatGoesBackwards_NeverBreaksTheCounts()
    {
        _m.Feed(Pad(), 1000);
        _m.Feed(Pad(lx: 0.1), 1010);
        _m.Feed(Pad(lx: 0.2), 5);                        // the clock restarted
        _m.Feed(Pad(lx: 0.3), 15);

        var rate = _m.Snapshot().Rate;
        Assert.Equal(3, rate.TotalChanges);
        Assert.Equal(1, rate.Histogram.Sum());           // only the one sane gap was counted
    }
}
