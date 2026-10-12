using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReWASD.UI.Mac.Tester;

/// <summary>
/// A bar chart of how often each value came up. Used for the gaps between reports and for where along its pull a trigger reported.
/// With <see cref="FlagGaps"/> a bar missing between two present ones is marked red: that part of the travel was skipped.
/// </summary>
public sealed class Histogram : Control
{
    public static readonly StyledProperty<IEnumerable?> ValuesProperty = AvaloniaProperty.Register<Histogram, IEnumerable?>(nameof(Values));
    public static readonly StyledProperty<string> StartLabelProperty = AvaloniaProperty.Register<Histogram, string>(nameof(StartLabel), "");
    public static readonly StyledProperty<string> EndLabelProperty = AvaloniaProperty.Register<Histogram, string>(nameof(EndLabel), "");
    public static readonly StyledProperty<bool> FlagGapsProperty = AvaloniaProperty.Register<Histogram, bool>(nameof(FlagGaps));
    public static readonly StyledProperty<string> BarColorProperty = AvaloniaProperty.Register<Histogram, string>(nameof(BarColor), "#E9C46A");

    static Histogram() => AffectsRender<Histogram>(ValuesProperty, StartLabelProperty, EndLabelProperty, FlagGapsProperty, BarColorProperty);

    public IEnumerable? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public string StartLabel { get => GetValue(StartLabelProperty); set => SetValue(StartLabelProperty, value); }
    public string EndLabel { get => GetValue(EndLabelProperty); set => SetValue(EndLabelProperty, value); }
    public bool FlagGaps { get => GetValue(FlagGapsProperty); set => SetValue(FlagGapsProperty, value); }
    public string BarColor { get => GetValue(BarColorProperty); set => SetValue(BarColorProperty, value); }

    /// <summary>The values as numbers (for tests and for drawing).</summary>
    internal static double[] ToArray(IEnumerable? values) =>
        values == null ? Array.Empty<double>() : values.Cast<object>().Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();

    /// <summary>The bins that lie between two reported ones and are themselves empty.</summary>
    internal static List<int> SkippedBins(double[] data)
    {
        int first = Array.FindIndex(data, v => v > 0), last = Array.FindLastIndex(data, v => v > 0);
        var gaps = new List<int>();
        if (first < 0) return gaps;
        for (int i = first + 1; i < last; i++) if (data[i] <= 0) gaps.Add(i);
        return gaps;
    }

    private static FormattedText Label(string text, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Menlo, Consolas, monospace"), 9.5, brush);

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 30 || h < 30) return;

        double labelHeight = 14;
        var plot = new Rect(0, 0, w, h - labelHeight);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#14171D")), new Pen(new SolidColorBrush(Color.Parse("#2A2F3A")), 1), plot, 4, 4);
        foreach (double f in new[] { 0.25, 0.5, 0.75 })
            context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#222630")), 1), new Point(0, plot.Height * f), new Point(w, plot.Height * f));

        var data = ToArray(Values);
        var muted = new SolidColorBrush(Color.Parse("#566173"));
        context.DrawText(Label(StartLabel, muted), new Point(2, h - labelHeight + 2));
        var end = Label(EndLabel, muted);
        context.DrawText(end, new Point(w - end.Width - 2, h - labelHeight + 2));

        if (data.Length == 0) return;
        double max = data.Max();
        if (max <= 0)
        {
            var none = Label("nothing measured yet", muted);
            context.DrawText(none, new Point((w - none.Width) / 2, plot.Height / 2 - none.Height / 2));
            return;
        }

        var color = Color.Parse(BarColor);
        var fill = new SolidColorBrush(color);
        double slot = (w - 8) / data.Length;
        double barWidth = Math.Max(1.5, slot * 0.72);
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] <= 0) continue;
            double barHeight = Math.Max(2, (plot.Height - 8) * Math.Sqrt(data[i] / max));      // square root keeps small counts visible
            var rect = new Rect(4 + i * slot + (slot - barWidth) / 2, plot.Height - 4 - barHeight, barWidth, barHeight);
            context.DrawRectangle(fill, null, rect, 2, 2);
        }

        if (FlagGaps)
        {
            var red = new SolidColorBrush(Color.Parse("#FF6B6B"));
            foreach (int i in SkippedBins(data))
                context.DrawRectangle(red, null, new Rect(4 + i * slot + (slot - barWidth) / 2, plot.Height - 8, barWidth, 4), 1, 1);
        }
    }
}
