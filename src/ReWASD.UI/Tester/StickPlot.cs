using System.Windows;
using System.Windows.Media;
using ReWASD.UI.Tester;

namespace ReWASD.UI.TesterControls;

/// <summary>
/// A stick drawn as a circle: where it is now, the path it just took, the shape it made when rolled around its edge (the circularity
/// test), and the deadzone rings from the config.
/// </summary>
public sealed class StickPlot : FrameworkElement
{
    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(nameof(Snapshot), typeof(StickSnapshot), typeof(StickPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DeadzoneMinProperty = DependencyProperty.Register(nameof(DeadzoneMin), typeof(double), typeof(StickPlot),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DeadzoneMaxProperty = DependencyProperty.Register(nameof(DeadzoneMax), typeof(double), typeof(StickPlot),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static Brush B(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static Pen P(string hex, double width, DashStyle? dash = null)
    {
        var pen = new Pen(B(hex), width);
        if (dash != null) pen.DashStyle = dash;
        pen.Freeze();
        return pen;
    }

    private static readonly Brush Face = B("#17191E");
    private static readonly Pen Rim = P("#4A4F5C", 1.6);
    private static readonly Pen Grid = P("#2C303A", 1);
    private static readonly Pen Axis = P("#383D49", 1);
    private static readonly Brush InnerFill = B("#26FFAA00");
    private static readonly Pen InnerRing = P("#FFAA00", 1.2, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly Pen OuterRing = P("#5BC0EB", 1.2, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly Brush ShapeFill = B("#30E9C46A");
    private static readonly Pen ShapeEdge = P("#E9C46A", 2);
    private static readonly Brush Dot = B("#00E676");
    private static readonly Pen DotRing = P("#0B2E1B", 2);
    private static readonly Pen Lead = P("#8000E676", 1.5);

    public StickSnapshot? Snapshot { get => (StickSnapshot?)GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    public double DeadzoneMin { get => (double)GetValue(DeadzoneMinProperty); set => SetValue(DeadzoneMinProperty, value); }
    public double DeadzoneMax { get => (double)GetValue(DeadzoneMaxProperty); set => SetValue(DeadzoneMaxProperty, value); }

    /// <summary>The screen points of the measured shape: one per covered angle, in drawing order.</summary>
    internal static List<Point> ShapePoints(StickSnapshot s, Point centre, double radius)
    {
        var points = new List<Point>();
        for (int i = 0; i < s.Extents.Length; i++)
        {
            if (s.Extents[i] < 0.05) continue;
            double angle = (i + 0.5) * (2 * Math.PI / s.Extents.Length);
            points.Add(new Point(centre.X + Math.Cos(angle) * s.Extents[i] * radius, centre.Y - Math.Sin(angle) * s.Extents[i] * radius));
        }
        return points;
    }

    protected override void OnRender(DrawingContext context)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 40) return;

        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = size / 2 - 8;

        context.DrawEllipse(Face, Rim, centre, r, r);
        foreach (double ring in new[] { 0.25, 0.5, 0.75 }) context.DrawEllipse(null, Grid, centre, r * ring, r * ring);
        context.DrawLine(Axis, new Point(centre.X - r, centre.Y), new Point(centre.X + r, centre.Y));
        context.DrawLine(Axis, new Point(centre.X, centre.Y - r), new Point(centre.X, centre.Y + r));

        // The deadzones from the config: inside the inner ring nothing counts; the outer ring is where a full push is reached
        if (DeadzoneMin > 0.005) context.DrawEllipse(InnerFill, InnerRing, centre, r * DeadzoneMin, r * DeadzoneMin);
        if (DeadzoneMax < 0.995) context.DrawEllipse(null, OuterRing, centre, r * DeadzoneMax, r * DeadzoneMax);

        var s = Snapshot;
        if (s == null) return;

        // The shape the stick has made so far
        var shape = ShapePoints(s, centre, r);
        if (shape.Count >= 3)
        {
            bool full = s.Coverage > 0.97;
            var figure = new PathFigure { StartPoint = shape[0], IsClosed = full, IsFilled = full };
            for (int i = 1; i < shape.Count; i++) figure.Segments.Add(new LineSegment(shape[i], true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            context.DrawGeometry(full ? ShapeFill : null, ShapeEdge, geometry);
        }

        // The path just taken, fading with age
        for (int i = 1; i < s.Trail.Length; i++)
        {
            byte alpha = (byte)(30 + 190.0 * i / s.Trail.Length);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 0, 230, 118));
            brush.Freeze();
            var pen = new Pen(brush, 1.4);
            pen.Freeze();
            context.DrawLine(pen, ToScreen(s.Trail[i - 1], centre, r), ToScreen(s.Trail[i], centre, r));
        }

        var now = ToScreen((s.X, s.Y), centre, r);
        context.DrawLine(Lead, centre, now);
        context.DrawEllipse(Dot, DotRing, now, 7, 7);
    }

    private static Point ToScreen((double X, double Y) p, Point centre, double r) => new(centre.X + p.X * r, centre.Y - p.Y * r);   // up is positive
}
