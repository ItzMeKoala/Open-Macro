using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReWASD.UI.Mac.Tester;

/// <summary>A ring that fills as a score rises, with the score in the middle. Green when high, amber in the middle, red when low.</summary>
public sealed class RingGauge : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<RingGauge, double>(nameof(Value));
    public static readonly StyledProperty<bool> HasValueProperty = AvaloniaProperty.Register<RingGauge, bool>(nameof(HasValue));
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<RingGauge, string>(nameof(Text), "—");
    public static readonly StyledProperty<string> CaptionProperty = AvaloniaProperty.Register<RingGauge, string>(nameof(Caption), "HEALTH");

    static RingGauge() => AffectsRender<RingGauge>(ValueProperty, HasValueProperty, TextProperty, CaptionProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool HasValue { get => GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>The colour for a score: how healthy it looks at a glance.</summary>
    public static Color ColorFor(double score, bool hasValue) =>
        !hasValue ? Color.Parse("#566173") : score >= 75 ? Color.Parse("#2EF2A0") : score >= 55 ? Color.Parse("#FFC24B") : Color.Parse("#FF6B6B");

    private static FormattedText Text_(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Menlo, Consolas, monospace", FontStyle.Normal, weight), size, brush);

    public override void Render(DrawingContext context)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size < 30) return;

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double thickness = Math.Max(6, size * 0.1);
        double r = size / 2 - thickness / 2 - 2;
        var accent = ColorFor(Value, HasValue);

        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#262A33")), thickness), centre, r, r);

        if (HasValue && Value > 0.5)
        {
            double sweep = Math.Clamp(Value, 0, 100) / 100.0 * 2 * Math.PI;
            double start = -Math.PI / 2;
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                var p0 = new Point(centre.X + r * Math.Cos(start), centre.Y + r * Math.Sin(start));
                var p1 = new Point(centre.X + r * Math.Cos(start + Math.Min(sweep, 2 * Math.PI - 0.001)), centre.Y + r * Math.Sin(start + Math.Min(sweep, 2 * Math.PI - 0.001)));
                c.BeginFigure(p0, false);
                c.ArcTo(p1, new Size(r, r), 0, sweep > Math.PI, SweepDirection.Clockwise);
                c.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B)), thickness + 8, lineCap: PenLineCap.Round), geometry);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(accent), thickness, lineCap: PenLineCap.Round), geometry);
        }

        var big = Text_(Text, size * 0.34, new SolidColorBrush(HasValue ? Colors.White : Color.Parse("#8E9BAE")), FontWeight.Bold);
        context.DrawText(big, new Point(centre.X - big.Width / 2, centre.Y - big.Height / 2 - size * 0.04));
        var small = Text_(Caption, Math.Max(8, size * 0.075), new SolidColorBrush(Color.Parse("#8E9BAE")), FontWeight.Bold);
        context.DrawText(small, new Point(centre.X - small.Width / 2, centre.Y + size * 0.16));
    }
}
