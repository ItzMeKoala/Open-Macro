using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.UI.Tester;

namespace ReWASD.UI.Mac.Tests;

public class TesterHealthTests
{
    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

    private static HealthItem Item(HealthReport r, string name) => r.Items.Single(i => i.Name == name);

    /// <summary>A model that has been thoroughly and cleanly tested.</summary>
    private static TesterModel Healthy()
    {
        var m = new TesterModel();
        double t = 0;
        m.Feed(Pad(), t);
        for (int i = 0; i < 150; i++) m.Feed(Pad(lx: 0.004, rx: -0.003), t += 5);                 // resting, tiny drift
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180;
            m.Feed(Pad(lx: Math.Cos(a), ly: Math.Sin(a), rx: Math.Cos(a), ry: Math.Sin(a)), t += 8);   // steady 8 ms reports
        }
        for (int i = 0; i <= 100; i++) m.Feed(Pad(lt: i / 100.0, rt: i / 100.0), t += 8);
        m.Feed(Pad(), t += 8);
        foreach (var b in new[] { ControllerButton.A, ControllerButton.B })
            for (int i = 0; i < 6; i++)
            {
                m.Feed(Pad(down: b), t += 200);
                m.Feed(Pad(), t += 90);
            }
        return m;
    }

    [Fact]
    public void ANewModel_HasNoVerdictYet()
    {
        var report = TesterHealth.Evaluate(new TesterModel().Snapshot());

        Assert.Null(report.Score);
        Assert.Equal(0, report.Checked);
        Assert.Equal(7, report.Items.Count);
        Assert.All(report.Items, i => Assert.Equal(Grade.Unknown, i.Grade));
        Assert.Contains("Test the controller", report.Verdict);
    }

    [Fact]
    public void AHealthyController_ScoresInTheNinetiesOnEveryCheck()
    {
        var report = TesterHealth.Evaluate(Healthy().Snapshot());

        Assert.All(report.Items, i => Assert.True(i.Grade == Grade.Good, $"{i.Name}: {i.Grade} ({i.Detail})"));
        Assert.Equal(100, report.Score);
        Assert.Equal(7, report.Checked);
        Assert.Equal("In great shape", report.Verdict);
    }

    [Fact]
    public void ADriftingStick_IsGraded()
    {
        // Within a usual game deadzone is fine; past it, a game would see the stick move by itself
        foreach (var (drift, expected) in new[] { (0.01, Grade.Good), (0.06, Grade.Good), (0.1, Grade.Fair), (0.18, Grade.Poor) })
        {
            var m = new TesterModel();
            for (int i = 0; i < 200; i++) m.Feed(Pad(lx: drift), i * 5);
            var item = Item(TesterHealth.Evaluate(m.Snapshot()), "Left stick drift");
            Assert.Equal(expected, item.Grade);
        }

        var few = new TesterModel();
        for (int i = 0; i < 10; i++) few.Feed(Pad(lx: 0.2 - 0.001 * i), i);
        Assert.Equal(Grade.Unknown, Item(TesterHealth.Evaluate(few.Snapshot()), "Left stick drift").Grade);
    }

    [Fact]
    public void AnUnevenStick_IsGraded()
    {
        var m = new TesterModel();
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180, r = (deg % 90 is >= 30 and <= 60) ? 0.78 : 1.0;
            m.Feed(Pad(rx: r * Math.Cos(a), ry: r * Math.Sin(a)), deg);
        }

        var item = Item(TesterHealth.Evaluate(m.Snapshot()), "Right stick roundness");
        Assert.Equal(Grade.Fair, item.Grade);                 // short by a fifth on the diagonals
        Assert.Contains("average error", item.Detail);

        var worn = new TesterModel();
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180, r = (deg % 90 is >= 20 and <= 70) ? 0.72 : 1.0;
            worn.Feed(Pad(rx: r * Math.Cos(a), ry: r * Math.Sin(a)), deg);
        }
        Assert.Equal(Grade.Poor, Item(TesterHealth.Evaluate(worn.Snapshot()), "Right stick roundness").Grade);
    }

    [Fact]
    public void ATriggerThatNeverReachesTheEnd_IsPoor()
    {
        var m = new TesterModel();
        for (int i = 0; i <= 80; i++) m.Feed(Pad(lt: i / 100.0, rt: i / 100.0), i * 8);       // only to 80%
        m.Feed(Pad(), 1000);

        var item = Item(TesterHealth.Evaluate(m.Snapshot()), "Trigger travel");
        Assert.Equal(Grade.Fair, item.Grade);

        var light = new TesterModel();
        for (int i = 0; i <= 40; i++) light.Feed(Pad(lt: i / 100.0, rt: i / 100.0), i * 8);   // a light squeeze is not a test of the travel
        light.Feed(Pad(), 1000);
        Assert.Equal(Grade.Unknown, Item(TesterHealth.Evaluate(light.Snapshot()), "Trigger travel").Grade);

        var lazy = new TesterModel();
        for (int i = 0; i <= 100; i++) lazy.Feed(Pad(lt: i == 0 ? 0 : 0.08 + i * 0.0092, rt: i / 100.0), i * 8);   // wakes up at 8%
        lazy.Feed(Pad(), 1000);
        Assert.Equal(Grade.Fair, Item(TesterHealth.Evaluate(lazy.Snapshot()), "Trigger travel").Grade);
    }

    [Fact]
    public void BouncingSwitches_AreCaught()
    {
        var m = new TesterModel();
        double t = 0;
        for (int i = 0; i < 30; i++)
        {
            m.Feed(Pad(down: ControllerButton.X), t += 300);
            m.Feed(Pad(), t += 100);
        }
        Assert.Equal(Grade.Good, Item(TesterHealth.Evaluate(m.Snapshot()), "Button switches").Grade);

        m.Feed(Pad(down: ControllerButton.X), t += 300);
        m.Feed(Pad(), t += 50);
        m.Feed(Pad(down: ControllerButton.X), t += 6);              // chatter
        m.Feed(Pad(), t += 50);
        var one = Item(TesterHealth.Evaluate(m.Snapshot()), "Button switches");
        Assert.Equal(Grade.Fair, one.Grade);
        Assert.Contains("1 bounce in", one.Detail);

        for (int i = 0; i < 2; i++)
        {
            m.Feed(Pad(down: ControllerButton.X), t += 300);
            m.Feed(Pad(), t += 50);
            m.Feed(Pad(down: ControllerButton.X), t += 6);
            m.Feed(Pad(), t += 50);
        }
        Assert.Equal(Grade.Poor, Item(TesterHealth.Evaluate(m.Snapshot()), "Button switches").Grade);
    }

    [Fact]
    public void TheReportRate_IsGradedByTheUsualGap_NotByHowEvenTheHandIs()
    {
        static Grade Rate(Func<int, double> gap)
        {
            var m = new TesterModel();
            m.Feed(Pad(), 0);
            double t = 0;
            for (int i = 1; i <= 120; i++) m.Feed(Pad(lx: i * 0.001), t += gap(i));
            return Item(TesterHealth.Evaluate(m.Snapshot()), "Report rate").Grade;
        }

        Assert.Equal(Grade.Good, Rate(_ => 8));                              // 125 a second
        Assert.Equal(Grade.Good, Rate(i => i % 3 == 0 ? 14 : 5));           // uneven, as a moving hand makes it, but quick
        Assert.Equal(Grade.Fair, Rate(_ => 25));                             // 40 a second
        Assert.Equal(Grade.Poor, Rate(_ => 50));                             // 20 a second
        Assert.Equal(Grade.Good, Rate(i => i % 20 == 0 ? 500 : 8));         // the person paused: not the pad's fault

        var m = new TesterModel();
        m.Feed(Pad(), 0);
        for (int i = 1; i <= 120; i++) m.Feed(Pad(lx: i * 0.001), i * 8.0);
        Assert.Contains("about 125 reports a second", Item(TesterHealth.Evaluate(m.Snapshot()), "Report rate").Detail);
    }

    [Fact]
    public void MovingTheStickThroughTheMiddle_IsNotDrift()
    {
        var m = new TesterModel();
        double t = 0;
        for (int i = 0; i < 100; i++) m.Feed(Pad(), t += 5);                 // resting dead centre
        for (int lap = 0; lap < 20; lap++)                                     // swinging side to side through the middle
            for (int k = -10; k <= 10; k++) m.Feed(Pad(lx: k / 10.0), t += 4);
        for (int i = 0; i < 100; i++) m.Feed(Pad(), t += 5);

        var left = m.Snapshot().Left;
        Assert.Equal(0, left.WorstRestDrift, 6);
        Assert.Equal(Grade.Good, Item(TesterHealth.Evaluate(m.Snapshot()), "Left stick drift").Grade);
    }

    [Fact]
    public void ASquareGateOvershootingALittle_IsStillHealthy()
    {
        // Many pads report a little past 1.0 on the diagonals; that is how they are made, not wear
        var m = new TesterModel();
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180, r = 1 + 0.08 * Math.Abs(Math.Sin(2 * a));
            m.Feed(Pad(lx: r * Math.Cos(a), ly: r * Math.Sin(a)), deg);
        }
        Assert.Equal(Grade.Good, Item(TesterHealth.Evaluate(m.Snapshot()), "Left stick roundness").Grade);
    }

    [Fact]
    public void ALazyLap_DoesNotCountTheDirectionsThatWereNotPushedAllTheWay()
    {
        var m = new TesterModel();
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180, r = deg is >= 100 and <= 130 ? 0.5 : 1.0;    // one stretch only half pushed
            m.Feed(Pad(lx: r * Math.Cos(a), ly: r * Math.Sin(a)), deg);
        }
        var s = m.Snapshot().Left;
        Assert.True(s.HasCircle);
        Assert.Equal(0, s.CircularityError, 3);                                // the half-pushed directions are left out, not counted as short
        Assert.True(s.Coverage < 0.95);
    }

    [Fact]
    public void TheScore_AveragesOnlyWhatWasChecked()
    {
        var m = new TesterModel();
        for (int i = 0; i < 200; i++) m.Feed(Pad(lx: 0.1), i * 5);            // one check: fair drift

        var report = TesterHealth.Evaluate(m.Snapshot());

        Assert.Equal(2, report.Checked);                                     // the other stick rested too, at zero
        Assert.Equal(85, report.Score);                                      // (70 + 100) / 2
    }

    // ---- the new measurements ---------------------------------------------------------------------------------------------

    [Fact]
    public void StepsSeen_ShowHowFinelyAnAxisReports()
    {
        var m = new TesterModel();
        for (int i = 0; i < 256; i++) m.Feed(Pad(lx: (i - 128) / 128.0, rt: i / 255.0), i * 2);        // an 8-bit pad

        var snap = m.Snapshot();

        Assert.Equal(256, snap.Left.StepsX);
        Assert.Equal(1, snap.Left.StepsY);
        Assert.Equal(255, snap.RightTrigger.Steps);                      // the zero reading is the starting value, not a change
    }

    [Fact]
    public void TheTravelHistogram_ShowsWhereATriggerReported_AndWhereItSkipped()
    {
        var m = new TesterModel();
        m.Feed(Pad(), 0);
        for (int i = 1; i <= 100; i++)
        {
            if (i is >= 40 and < 60) continue;                           // a trigger that jumps over the middle
            m.Feed(Pad(lt: i / 100.0), i * 8);
        }

        var travel = m.Snapshot().LeftTrigger.Travel;

        Assert.Equal(TesterModel.TravelBins, travel.Length);
        Assert.True(travel[2] > 0 && travel[18] > 0);
        Assert.Equal(0, travel[10]);                                     // nothing between 50% and 55%
        Assert.Equal(0, m.Snapshot().RightTrigger.Travel.Sum());
    }

    [Fact]
    public void TheGapHistogram_CountsEachReportGap()
    {
        var m = new TesterModel();
        m.Feed(Pad(), 0);
        double t = 0;
        for (int i = 1; i <= 30; i++) m.Feed(Pad(lx: i * 0.01), t += 8);
        for (int i = 1; i <= 5; i++) m.Feed(Pad(lx: 0.5 + i * 0.01), t += 100);       // five long gaps

        var bins = m.Snapshot().Rate.Histogram;

        Assert.Equal(TesterModel.HistogramBins, bins.Length);
        Assert.Equal(29, bins[8]);                                                     // the first report has no gap before it
        Assert.Equal(5, bins[^1]);                                                     // everything over 40 ms lands in the last bin
        Assert.Equal(34, bins.Sum());

        m.Reset();
        Assert.Equal(0, m.Snapshot().Rate.Histogram.Sum());
    }

    // ---- the report text ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReport_HasEverySection_AndTheRealNumbers()
    {
        var m = Healthy();
        m.StartReaction(10_000, new Random(1));
        var snap = m.Snapshot();
        var health = TesterHealth.Evaluate(snap);

        string text = TesterReport.Build(snap, "Xbox Wireless Controller", health, new DateTime(2026, 3, 4, 14, 5, 0));

        Assert.StartsWith("Open-Macro controller test report", text);
        Assert.Contains("Controller: Xbox Wireless Controller", text);
        Assert.Contains("Date: 2026-03-04 14:05", text);
        Assert.Contains("Health: 100/100 — In great shape (7 of 7 checks done)", text);
        Assert.Contains("[good] Left stick drift", text);
        Assert.Contains("circle: average error 0.0%", text);
        Assert.Contains("Triggers", text);
        Assert.Contains("A ", text);
        Assert.Contains("Left bumper", text);
        Assert.Contains("Reports", text);
        Assert.DoesNotContain("Reaction test:", text);                   // none finished yet
    }

    [Fact]
    public void AnUntestedController_GetsAnHonestReport()
    {
        var snap = new TesterModel().Snapshot();

        string text = TesterReport.Build(snap, "No controller", TesterHealth.Evaluate(snap), new DateTime(2026, 1, 1));

        Assert.Contains("Health: not enough tested yet", text);
        Assert.Contains("circle: not measured", text);
        Assert.Contains("[n/a ]", text);
    }
}
