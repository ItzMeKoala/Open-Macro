using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReWASD.UI.Tester;

namespace ReWASD.UI.Mac.Tester;

/// <summary>
/// A stick drawn as a circle: where it is now, the path it just took, the shape it made when rolled around its edge (the circularity
/// test), and the deadzone rings from the config.
/// </summary>
public sealed class StickPlot : Control
{
    public static readonly StyledProperty<StickSnapshot?> SnapshotProperty = AvaloniaProperty.Register<StickPlot, StickSnapshot?>(nameof(Snapshot));
    public static readonly StyledProperty<double> DeadzoneMinProperty = AvaloniaProperty.Register<StickPlot, double>(nameof(DeadzoneMin));
    public static readonly StyledProperty<double> DeadzoneMaxProperty = AvaloniaProperty.Register<StickPlot, double>(nameof(DeadzoneMax), 1.0);

    private static readonly IBrush Face = new SolidColorBrush(Color.Parse("#17191E"));
    private static readonly IPen Rim = new Pen(new SolidColorBrush(Color.Parse("#4A4F5C")), 1.6);
    private static readonly IPen Grid = new Pen(new SolidColorBrush(Color.Parse("#2C303A")), 1);
    private static readonly IPen Axis = new Pen(new SolidColorBrush(Color.Parse("#383D49")), 1);
    private static readonly IBrush InnerFill = new SolidColorBrush(Color.Parse("#26FFAA00"));
    private static readonly IPen InnerRing = new Pen(new SolidColorBrush(Color.Parse("#FFAA00")), 1.2, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly IPen OuterRing = new Pen(new SolidColorBrush(Color.Parse("#5BC0EB")), 1.2, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly IBrush ShapeFill = new SolidColorBrush(Color.Parse("#30E9C46A"));
    private static readonly IPen ShapeEdge = new Pen(new SolidColorBrush(Color.Parse("#E9C46A")), 2);
    private static readonly IBrush Dot = new SolidColorBrush(Color.Parse("#00E676"));
    private static readonly IPen DotRing = new Pen(new SolidColorBrush(Color.Parse("#0B2E1B")), 2);
    private static readonly IPen Lead = new Pen(new SolidColorBrush(Color.Parse("#8000E676")), 1.5);

    static StickPlot()
    {
        AffectsRender<StickPlot>(SnapshotProperty, DeadzoneMinProperty, DeadzoneMaxProperty);
    }

    public StickSnapshot? Snapshot { get => GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    public double DeadzoneMin { get => GetValue(DeadzoneMinProperty); set => SetValue(DeadzoneMinProperty, value); }
    public double DeadzoneMax { get => GetValue(DeadzoneMaxProperty); set => SetValue(DeadzoneMaxProperty, value); }

    /// <summary>The screen points of the measured shape (for tests): one per covered angle, in drawing order.</summary>
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

    public override void Render(DrawingContext context)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size < 40) return;

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
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
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                c.BeginFigure(shape[0], full);
                for (int i = 1; i < shape.Count; i++) c.LineTo(shape[i]);
                c.EndFigure(full);
            }
            context.DrawGeometry(full ? ShapeFill : null, ShapeEdge, geometry);
        }

        // The path just taken, fading with age
        for (int i = 1; i < s.Trail.Length; i++)
        {
            byte alpha = (byte)(30 + 190.0 * i / s.Trail.Length);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 0, 230, 118)), 1.4);
            context.DrawLine(pen, ToScreen(s.Trail[i - 1], centre, r), ToScreen(s.Trail[i], centre, r));
        }

        var now = ToScreen((s.X, s.Y), centre, r);
        context.DrawLine(Lead, centre, now);
        context.DrawEllipse(Dot, DotRing, now, 7, 7);
    }

    private static Point ToScreen((double X, double Y) p, Point centre, double r) => new(centre.X + p.X * r, centre.Y - p.Y * r);   // up is positive
}
