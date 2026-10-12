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

    private static IBrush Solid(string hex) => new SolidColorBrush(Color.Parse(hex)).ToImmutable();
    private static IPen Stroke(string hex, double width, DashStyle? dash = null) => new Pen(Solid(hex), width, dash).ToImmutable();

    private static readonly IBrush Face = Solid("#15181E");
    private static readonly IPen Rim = Stroke("#5A6070", 1.8);
    private static readonly IPen Tick = Stroke("#3E4452", 1);
    private static readonly IPen TickMajor = Stroke("#6A7184", 1.6);
    private static readonly IPen Grid = Stroke("#2A2F3A", 1);
    private static readonly IPen Axis = Stroke("#3A4050", 1);
    private static readonly IPen Guide = Stroke("#2EC4B655", 1, new DashStyle(new double[] { 2, 4 }, 0));
    private static readonly IBrush InnerFill = Solid("#22FFAA00");
    private static readonly IPen InnerRing = Stroke("#FFAA00", 1.2, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly IPen OuterRing = Stroke("#5BC0EB", 1.2, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly IBrush ShapeFill = Solid("#2EE9C46A");
    private static readonly IPen ShapeEdge = Stroke("#E9C46A", 2.2);
    private static readonly IBrush Dot = Solid("#2EF2A0");
    private static readonly IPen DotRing = Stroke("#0B2E1B", 2);
    private static readonly IPen Lead = Stroke("#802EF2A0", 1.5);

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
        double r = size / 2 - 10;

        context.DrawEllipse(Face, Rim, centre, r, r);

        // A scale around the rim: a tick every 15 degrees, longer ones at the four directions
        for (int deg = 0; deg < 360; deg += 15)
        {
            double a = deg * Math.PI / 180;
            bool major = deg % 90 == 0;
            double inner = r + (major ? 1 : 2), outer = r + (major ? 8 : 5);
            context.DrawLine(major ? TickMajor : Tick, new Point(centre.X + Math.Cos(a) * inner, centre.Y - Math.Sin(a) * inner), new Point(centre.X + Math.Cos(a) * outer, centre.Y - Math.Sin(a) * outer));
        }

        foreach (double ring in new[] { 0.25, 0.5, 0.75 }) context.DrawEllipse(null, Grid, centre, r * ring, r * ring);
        context.DrawLine(Axis, new Point(centre.X - r, centre.Y), new Point(centre.X + r, centre.Y));
        context.DrawLine(Axis, new Point(centre.X, centre.Y - r), new Point(centre.X, centre.Y + r));

        // The deadzones from the config: inside the inner ring nothing counts; the outer ring is where a full push is reached
        if (DeadzoneMin > 0.005) context.DrawEllipse(InnerFill, InnerRing, centre, r * DeadzoneMin, r * DeadzoneMin);
        if (DeadzoneMax < 0.995) context.DrawEllipse(null, OuterRing, centre, r * DeadzoneMax, r * DeadzoneMax);

        var s = Snapshot;
        if (s == null) return;

        // The shape the stick has made so far, with a soft glow under it
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
            double f = (double)i / s.Trail.Length;
            var a = ToScreen(s.Trail[i - 1], centre, r);
            var b = ToScreen(s.Trail[i], centre, r);
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(40 + 190 * f), 46, 242, 160)), 1.6), a, b);
        }

        var now = ToScreen((s.X, s.Y), centre, r);
        if (s.Magnitude > 0.02)
        {
            // Faint guide lines from the stick out to the two axes, like the cursor of a scope
            context.DrawLine(Guide, new Point(centre.X - r, now.Y), new Point(centre.X + r, now.Y));
            context.DrawLine(Guide, new Point(now.X, centre.Y - r), new Point(now.X, centre.Y + r));
        }
        context.DrawLine(Lead, centre, now);
        context.DrawEllipse(Dot, DotRing, now, 7.5, 7.5);
    }

    private static Point ToScreen((double X, double Y) p, Point centre, double r) => new(centre.X + p.X * r, centre.Y - p.Y * r);   // up is positive
}
