using System.Globalization;
using System.Text;

namespace ReWASD.UI.Tester;

/// <summary>The whole test as text, for copying or saving.</summary>
public static class TesterReport
{
    public static string Build(TesterSnapshot s, string controllerName, DateTime when)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Open-Macro controller test report");
        sb.AppendLine($"Controller: {controllerName}");
        sb.AppendLine($"Date: {when.ToString("yyyy-MM-dd HH:mm", c)}");
        var span = TimeSpan.FromMilliseconds(s.ElapsedMs);
        sb.AppendLine($"Test length: {(int)span.TotalMinutes} min {span.Seconds} s");
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
