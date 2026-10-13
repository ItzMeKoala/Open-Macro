using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.UI.Tester;

namespace ReWASD.UI.Mac.Tests;

public class TesterMeasurementTests
{
    private static PhysicalState Pad(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0, params ControllerButton[] down) =>
        new(new HashSet<ControllerButton>(down), lx, ly, rx, ry, lt, rt);

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

        string text = TesterReport.Build(snap, "Xbox Wireless Controller", new DateTime(2026, 3, 4, 14, 5, 0));

        Assert.StartsWith("Open-Macro controller test report", text);
        Assert.Contains("Controller: Xbox Wireless Controller", text);
        Assert.Contains("Date: 2026-03-04 14:05", text);
        Assert.DoesNotContain("Health", text);
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

        string text = TesterReport.Build(snap, "No controller", new DateTime(2026, 1, 1));

        Assert.Contains("circle: not measured", text);
    }
}
