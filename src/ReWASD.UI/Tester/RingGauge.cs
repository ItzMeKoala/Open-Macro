using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ReWASD.UI.TesterControls;

/// <summary>A ring that fills as a score rises, with the score in the middle. Green when high, amber in the middle, red when low.</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasValueProperty = DependencyProperty.Register(nameof(HasValue), typeof(bool), typeof(RingGauge),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(RingGauge),
        new FrameworkPropertyMetadata("—", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(RingGauge),
        new FrameworkPropertyMetadata("HEALTH", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool HasValue { get => (bool)GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>The colour for a score: how healthy it looks at a glance.</summary>
    public static Color ColorFor(double score, bool hasValue) =>
        !hasValue ? C("#566173") : score >= 75 ? C("#2EF2A0") : score >= 55 ? C("#FFC24B") : C("#FF6B6B");

    private FormattedText Format(string text, double size, Brush brush, FontWeight weight) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Consolas"), FontStyles.Normal, weight, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext context)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 30) return;

        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        double thickness = Math.Max(6, size * 0.1);
        double r = size / 2 - thickness / 2 - 2;
        var accent = ColorFor(Value, HasValue);

        context.DrawEllipse(null, new Pen(new SolidColorBrush(C("#262A33")), thickness), centre, r, r);

        if (HasValue && Value > 0.5)
        {
            double sweep = Math.Min(Math.Clamp(Value, 0, 100) / 100.0 * 2 * Math.PI, 2 * Math.PI - 0.001);
            double start = -Math.PI / 2;
            var figure = new PathFigure { StartPoint = new Point(centre.X + r * Math.Cos(start), centre.Y + r * Math.Sin(start)), IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(new Point(centre.X + r * Math.Cos(start + sweep), centre.Y + r * Math.Sin(start + sweep)), new Size(r, r), 0, sweep > Math.PI, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var glow = new Pen(new SolidColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B)), thickness + 8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var ring = new Pen(new SolidColorBrush(accent), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            context.DrawGeometry(null, glow, geometry);
            context.DrawGeometry(null, ring, geometry);
        }

        var big = Format(Text ?? "", size * 0.34, new SolidColorBrush(HasValue ? Colors.White : C("#8E9BAE")), FontWeights.Bold);
        context.DrawText(big, new Point(centre.X - big.Width / 2, centre.Y - big.Height / 2 - size * 0.04));
        var small = Format(Caption ?? "", Math.Max(8, size * 0.075), new SolidColorBrush(C("#8E9BAE")), FontWeights.Bold);
        context.DrawText(small, new Point(centre.X - small.Width / 2, centre.Y + size * 0.16));
    }
}
