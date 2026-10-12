using System.Globalization;
using System.Text;

namespace ReWASD.UI.Tester;

public enum Grade
{
    /// <summary>Not enough has been tested to say.</summary>
    Unknown,
    Good,
    Fair,
    Poor
}

/// <summary>One check of the health report.</summary>
public sealed record HealthItem(string Name, Grade Grade, string Detail)
{
    public int Score => Grade switch { Grade.Good => 100, Grade.Fair => 70, Grade.Poor => 30, _ => 0 };
}

/// <summary>What the checks add up to. <see cref="Score"/> is null until at least one check has enough to go on.</summary>
public sealed record HealthReport(IReadOnlyList<HealthItem> Items, int? Score, int Checked, string Verdict);

/// <summary>
/// Turns the numbers of a test into plain verdicts: is the stick drifting, is it round, do the triggers reach the ends, do the
/// switches bounce, does the pad report evenly. Each check only speaks once it has seen enough.
/// </summary>
public static class TesterHealth
{
    public const int MinRestSamples = 50;
    public const int MinChangesForRate = 60;

    public static HealthReport Evaluate(TesterSnapshot s)
    {
        var items = new List<HealthItem>
        {
            Drift("Left stick drift", s.Left),
            Drift("Right stick drift", s.Right),
            Roundness("Left stick roundness", s.Left),
            Roundness("Right stick roundness", s.Right),
            Triggers(s),
            Switches(s),
            Reports(s)
        };

        var known = items.Where(i => i.Grade != Grade.Unknown).ToList();
        int? score = known.Count == 0 ? null : (int)Math.Round(known.Average(i => i.Score));

        string verdict = score switch
        {
            null => "Test the controller to get a verdict",
            >= 90 => "In great shape",
            >= 75 => "Healthy, with small things to watch",
            >= 55 => "Showing wear",
            _ => "Has problems"
        };
        return new HealthReport(items, score, known.Count, verdict);
    }

    private static HealthItem Drift(string name, StickSnapshot s)
    {
        if (s.RestSamples < MinRestSamples) return new(name, Grade.Unknown, "Let go of the stick for a moment");
        string detail = $"rests {s.WorstRestDrift:P1} off centre at worst";

        // Games ignore the middle of a stick (a deadzone of 10 – 25 % is usual), so a stick that rests within a few percent of centre
        // is fine; one that rests where a game would notice it is drifting
        return new(name, s.WorstRestDrift < 0.08 ? Grade.Good : s.WorstRestDrift < 0.15 ? Grade.Fair : Grade.Poor, detail);
    }

    private static HealthItem Roundness(string name, StickSnapshot s)
    {
        if (!s.HasCircle) return new(name, Grade.Unknown, "Roll the stick slowly around its edge, all the way out");
        string detail = $"{s.CircularityError:P1} average error, reaches {s.MinRadius:0.00} – {s.MaxRadius:0.00}";

        // A healthy stick is within about 10 % of a perfect circle all the way round (pads with a square-ish gate overshoot a little
        // on the diagonals); a worn or damaged one falls well short somewhere
        return new(name, s.CircularityError <= 0.10 ? Grade.Good : s.CircularityError <= 0.18 ? Grade.Fair : Grade.Poor, detail);
    }

    private static HealthItem Triggers(TesterSnapshot s)
    {
        const string name = "Trigger travel";
        double reach = Math.Min(s.LeftTrigger.Peak, s.RightTrigger.Peak);
        if (s.LeftTrigger.Presses == 0 || s.RightTrigger.Presses == 0 || reach < 0.6) return new(name, Grade.Unknown, "Pull both triggers all the way");

        double start = Math.Max(s.LeftTrigger.FirstMovement, s.RightTrigger.FirstMovement);
        string detail = $"reach {reach:P0}, wake up at {start:P1}";
        if (reach < 0.9) return new(name, Grade.Fair, detail);
        return new(name, reach >= 0.97 && start <= 0.08 ? Grade.Good : Grade.Fair, detail);
    }

    private static HealthItem Switches(TesterSnapshot s)
    {
        const string name = "Button switches";
        long presses = s.Buttons.Sum(b => b.Presses);
        int bounces = s.Buttons.Sum(b => b.Bounces);
        if (presses < 10) return new(name, Grade.Unknown, "Press the buttons a few times each");

        string detail = bounces == 0 ? $"{presses} presses, no bounce" : $"{bounces} bounce{(bounces == 1 ? "" : "s")} in {presses} presses";
        if (bounces == 0) return new(name, Grade.Good, detail);
        return new(name, bounces >= 3 || bounces / (double)presses > 0.05 ? Grade.Poor : Grade.Fair, detail);
    }

    private static HealthItem Reports(TesterSnapshot s)
    {
        const string name = "Report rate";
        var r = s.Rate;
        if (r.TotalChanges < MinChangesForRate || r.MedianIntervalMs <= 0) return new(name, Grade.Unknown, "Move a stick steadily for a few seconds");

        // The typical gap between reports while a stick moves. How evenly they come depends as much on the hand as on the pad, so only
        // the usual gap is graded: 60 a second or more is smooth, under 30 a second is sluggish
        double perSecond = 1000 / r.MedianIntervalMs;
        string detail = $"about {perSecond:0} reports a second (usual gap {r.MedianIntervalMs:0.#} ms)";
        return new(name, r.MedianIntervalMs <= 17 ? Grade.Good : r.MedianIntervalMs <= 34 ? Grade.Fair : Grade.Poor, detail);
    }
}

/// <summary>The whole test as text, for copying or saving.</summary>
public static class TesterReport
{
    public static string Build(TesterSnapshot s, string controllerName, HealthReport health, DateTime when)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("ReWASD controller test report");
        sb.AppendLine($"Controller: {controllerName}");
        sb.AppendLine($"Date: {when.ToString("yyyy-MM-dd HH:mm", c)}");
        var span = TimeSpan.FromMilliseconds(s.ElapsedMs);
        sb.AppendLine($"Test length: {(int)span.TotalMinutes} min {span.Seconds} s");
        sb.AppendLine();

        sb.AppendLine(health.Score is { } score ? $"Health: {score}/100 — {health.Verdict} ({health.Checked} of {health.Items.Count} checks done)" : "Health: not enough tested yet");
        foreach (var item in health.Items) sb.AppendLine($"  [{GradeText(item.Grade)}] {item.Name}: {item.Detail}");
        sb.AppendLine();

        sb.AppendLine("Sticks");
        Stick(sb, "Left", s.Left, c);
        Stick(sb, "Right", s.Right, c);
        sb.AppendLine();

        sb.AppendLine("Triggers");
        Trigger(sb, "Left", s.LeftTrigger, c);
        Trigger(sb, "Right", s.RightTrigger, c);
        sb.AppendLine();

        sb.AppendLine("Buttons (presses, shortest – longest hold, bounces)");
        foreach (var b in s.Buttons)
        {
            string hold = b.Presses > 0 ? $"{b.ShortestHoldMs:0} – {b.LongestHoldMs:0} ms" : "—";
            sb.AppendLine($"  {TesterModel.NameOf(b.Button),-18} {b.Presses,5}   {hold,-18} {(b.Bounces > 0 ? b.Bounces + " bounce(s)" : "")}".TrimEnd());
        }
        sb.AppendLine();

        var r = s.Rate;
        sb.AppendLine("Reports");
        sb.AppendLine($"  {r.TotalChanges} changes, peak {r.PeakChangesPerSecond:0} per second, usual gap {r.MedianIntervalMs.ToString("0.0", c)} ms, average gap {r.AverageIntervalMs.ToString("0.0", c)} ms, worst {r.WorstIntervalMs.ToString("0.0", c)} ms, jitter {r.JitterMs.ToString("0.0", c)} ms, steady {r.SteadyShare.Pct(0)}");
        if (s.Reaction.Attempts > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Reaction test: best {s.Reaction.BestMs:0} ms, average {s.Reaction.AverageMs:0} ms over {s.Reaction.Attempts} tries");
        }
        return sb.ToString();
    }

    // Invariant culture writes percentages as "100.0 %"; reports use "100.0%"
    private static string Pct(this double value, int decimals) =>
        (value * 100).ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), CultureInfo.InvariantCulture) + "%";

    private static string GradeText(Grade g) => g switch { Grade.Good => "good", Grade.Fair => "fair", Grade.Poor => "POOR", _ => "n/a " };

    private static void Stick(StringBuilder sb, string name, StickSnapshot s, CultureInfo c)
    {
        sb.AppendLine($"  {name}: peak {s.PeakMagnitude.ToString("0.000", c)}, rest offset x {s.RestX.ToString("+0.000;-0.000;0.000", c)} y {s.RestY.ToString("+0.000;-0.000;0.000", c)} (worst drift {s.WorstRestDrift.ToString("0.000", c)}), {s.StepsX} x steps, {s.StepsY} y steps");
        sb.AppendLine(s.HasCircle
            ? $"    circle: average error {s.CircularityError.Pct(1)}, coverage {s.Coverage.Pct(0)}, radius min {s.MinRadius.ToString("0.000", c)} mean {s.MeanRadius.ToString("0.000", c)} max {s.MaxRadius.ToString("0.000", c)}"
            : $"    circle: not measured (coverage {s.Coverage.Pct(0)})");
    }

    private static void Trigger(StringBuilder sb, string name, TriggerSnapshot t, CultureInfo c) =>
        sb.AppendLine($"  {name}: peak {t.Peak.Pct(0)}, wakes up at {t.FirstMovement.Pct(1)}, {t.Presses} pulls, {t.Steps} distinct readings");
}
