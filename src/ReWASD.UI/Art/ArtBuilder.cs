using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ReWASD.Core.Skins.Art;

namespace ReWASD.UI.Art;

/// <summary>Turns an <see cref="ArtScene"/> into WPF visuals. The drawing is built once; only transforms and opacities change afterwards.</summary>
internal sealed class ArtBuilder
{
    // WPF's blur radius reaches further than an SVG standard deviation of the same number
    private const double BlurPerStdDev = 2.2;

    private readonly ArtScene _scene;
    private readonly Dictionary<(ArtPaint, double), Brush> _brushes = new();

    public Dictionary<ArtGroup, Canvas> Groups { get; } = new();
    public Dictionary<ArtPath, Path> Paths { get; } = new();

    public ArtBuilder(ArtScene scene) => _scene = scene;

    public Canvas BuildRoot()
    {
        var root = BuildGroup(_scene.Root);

        // Shapes are authored in the viewBox's coordinates; slide the visible area to the canvas's top-left
        if (_scene.OriginX != 0 || _scene.OriginY != 0)
        {
            var move = new TranslateTransform(-_scene.OriginX, -_scene.OriginY);
            move.Freeze();
            root.RenderTransform = move;
        }
        return root;
    }

    private Canvas BuildGroup(ArtGroup g)
    {
        var canvas = new Canvas { Width = _scene.Width, Height = _scene.Height };
        Apply(canvas, g);
        Groups[g] = canvas;

        foreach (var child in g.Children)
        {
            UIElement? c = child switch
            {
                ArtGroup sub => BuildGroup(sub),
                ArtPath p => BuildPath(p),
                ArtText t => BuildText(t),
                _ => null
            };
            if (c != null) canvas.Children.Add(c);
        }
        return canvas;
    }

    private Path BuildPath(ArtPath p)
    {
        var geometry = Geometry.Parse(p.D);
        geometry.Freeze();

        var path = new Path
        {
            Data = geometry,
            Fill = Brush(p.Fill, p.FillOpacity),
            Stroke = Brush(p.Stroke, p.StrokeOpacity),
            StrokeThickness = p.StrokeWidth,
            StrokeStartLineCap = Cap(p.LineCap),
            StrokeEndLineCap = Cap(p.LineCap),
            StrokeLineJoin = p.LineJoin switch { ArtLineJoin.Round => PenLineJoin.Round, ArtLineJoin.Bevel => PenLineJoin.Bevel, _ => PenLineJoin.Miter }
        };

        // WPF measures dashes in multiples of the stroke width; SVG measures them in drawing units
        if (p.DashArray != null && p.StrokeWidth > 0)
            path.StrokeDashArray = new DoubleCollection(p.DashArray.Select(d => d / p.StrokeWidth));

        Apply(path, p);
        Paths[p] = path;

        // Glows, shadows and other soft effects must never catch a click that belongs to the button next to them
        if (p.Blur > 0 || p.IsGlow) path.IsHitTestVisible = false;

        // A glow shows at most this strong (data-max), so a lit button brightens instead of turning into a flat colour
        if (p.IsGlow) path.Tag = double.TryParse(p.Get("max"), NumberStyles.Float, CultureInfo.InvariantCulture, out var max) ? max : 1.0;
        return path;
    }

    // Text is laid out in a box this wide around its anchor, and as tall as two lines around its middle
    private const double TextBox = 600;

    /// <summary>A legend: a text block centred on the line's middle, starting, centred or ending at its anchor.</summary>
    private UIElement BuildText(ArtText t)
    {
        var fill = new SolidColorBrush(Parse(t.Fill, 1));
        fill.Freeze();
        var block = new TextBlock
        {
            Text = t.Text,
            FontSize = t.FontSize,
            FontWeight = FontWeight.FromOpenTypeWeight(Math.Clamp(t.FontWeight, 1, 999)),
            Foreground = fill,
            TextAlignment = t.Anchor switch { ArtTextAnchor.Middle => TextAlignment.Center, ArtTextAnchor.End => TextAlignment.Right, _ => TextAlignment.Left },
            VerticalAlignment = VerticalAlignment.Center,
            Width = TextBox
        };
        if (!string.IsNullOrWhiteSpace(t.FontFamily)) block.FontFamily = new FontFamily(t.FontFamily);

        var box = new Border { Width = TextBox, Height = t.FontSize * 2, Child = block, IsHitTestVisible = false };
        Canvas.SetLeft(box, t.Anchor switch { ArtTextAnchor.Middle => t.X - TextBox / 2, ArtTextAnchor.End => t.X - TextBox, _ => t.X });
        Canvas.SetTop(box, t.CenterY - t.FontSize);
        Apply(box, t);
        return box;
    }

    private static PenLineCap Cap(ArtLineCap cap) => cap switch { ArtLineCap.Round => PenLineCap.Round, ArtLineCap.Square => PenLineCap.Square, _ => PenLineCap.Flat };

    private static void Apply(UIElement e, ArtNode n)
    {
        e.Opacity = n.Opacity;
        if (!n.Transform.IsIdentity)
        {
            var t = new MatrixTransform(ToMatrix(n.Transform));
            t.Freeze();
            e.RenderTransform = t;
        }
        if (n.Blur > 0) e.Effect = new BlurEffect { Radius = n.Blur * BlurPerStdDev, KernelType = KernelType.Gaussian };
        if (n.ClipPath != null)
        {
            var clip = Geometry.Parse(n.ClipPath);
            clip.Freeze();
            e.Clip = clip;
        }
    }

    public static Matrix ToMatrix(ArtMatrix m) => new(m.A, m.B, m.C, m.D, m.E, m.F);

    private Brush? Brush(ArtPaint? paint, double opacity)
    {
        if (paint == null) return null;
        var key = (paint, opacity);
        if (_brushes.TryGetValue(key, out var cached)) return cached;

        Brush brush = paint switch
        {
            ArtSolid s => new SolidColorBrush(Parse(s.Color, opacity)),
            ArtLinearGradient l => new LinearGradientBrush(Stops(l.Stops, opacity), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2))
            {
                MappingMode = l.UserSpace ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox
            },
            ArtRadialGradient r => new RadialGradientBrush(Stops(r.Stops, opacity))
            {
                MappingMode = r.UserSpace ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox,
                Center = new Point(r.Cx, r.Cy),
                GradientOrigin = new Point(r.Fx, r.Fy),
                RadiusX = r.Radius,
                RadiusY = r.Radius
            },
            _ => Brushes.Transparent
        };

        brush.Freeze();
        _brushes[key] = brush;
        return brush;
    }

    private static GradientStopCollection Stops(IReadOnlyList<ArtStop> stops, double opacity)
    {
        var result = new GradientStopCollection();
        foreach (var s in stops) result.Add(new GradientStop(Parse(s.Color, s.Opacity * opacity), s.Offset));
        return result;
    }

    internal static Color Parse(string color, double opacity)
    {
        Color c;
        try
        {
            c = (Color)ColorConverter.ConvertFromString(color);
        }
        catch (FormatException)
        {
            c = Colors.Transparent;
        }
        return opacity >= 1 ? c : Color.FromArgb((byte)Math.Round(c.A * Math.Clamp(opacity, 0, 1)), c.R, c.G, c.B);
    }
}
