using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using ReWASD.Core.Skins.Art;

namespace ReWASD.UI.Mac.Art;

/// <summary>Turns an <see cref="ArtScene"/> into Avalonia visuals. The drawing is built once; only transforms and opacities change afterwards.</summary>
internal sealed class ArtBuilder
{
    private readonly ArtScene _scene;
    private readonly Dictionary<(ArtPaint, double), IBrush> _brushes = new();

    /// <summary>The canvas built for each group, so parts can be animated and wired up.</summary>
    public Dictionary<ArtGroup, Canvas> Groups { get; } = new();

    /// <summary>The control built for each path.</summary>
    public Dictionary<ArtPath, Avalonia.Controls.Shapes.Path> Paths { get; } = new();

    public ArtBuilder(ArtScene scene) => _scene = scene;

    public Canvas BuildRoot()
    {
        var root = BuildGroup(_scene.Root);

        // Shapes are authored in the viewBox's coordinates; slide the visible area to the canvas's top-left
        if (_scene.OriginX != 0 || _scene.OriginY != 0)
        {
            root.RenderTransformOrigin = RelativePoint.TopLeft;
            root.RenderTransform = new TranslateTransform(-_scene.OriginX, -_scene.OriginY);
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
            Control? c = child switch
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

    private Avalonia.Controls.Shapes.Path BuildPath(ArtPath p)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(p.D),
            Fill = Brush(p.Fill, p.FillOpacity),
            Stroke = Brush(p.Stroke, p.StrokeOpacity),
            StrokeThickness = p.StrokeWidth,
            StrokeLineCap = p.LineCap switch { ArtLineCap.Round => PenLineCap.Round, ArtLineCap.Square => PenLineCap.Square, _ => PenLineCap.Flat },
            StrokeJoin = p.LineJoin switch { ArtLineJoin.Round => PenLineJoin.Round, ArtLineJoin.Bevel => PenLineJoin.Bevel, _ => PenLineJoin.Miter }
        };

        // Avalonia measures dashes in multiples of the stroke width; SVG measures them in drawing units
        if (p.DashArray != null && p.StrokeWidth > 0)
            path.StrokeDashArray = new AvaloniaList<double>(p.DashArray.Select(d => d / p.StrokeWidth));

        Apply(path, p);
        Paths[p] = path;

        // Glows, shadows and other soft effects must never catch a click that belongs to the button next to them
        if (p.Blur > 0 || p.IsGlow) path.IsHitTestVisible = false;

        // A glow shows at most this strong (data-max), so a lit button brightens instead of turning into a flat colour
        if (p.IsGlow) path.Tag = double.TryParse(p.Get("max"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var max) ? max : 1.0;
        return path;
    }

    // Text is laid out in a box this wide around its anchor, and as tall as two lines around its middle
    private const double TextBox = 600;

    /// <summary>A legend: a text block centred on the line's middle, starting, centred or ending at its anchor.</summary>
    private Control BuildText(ArtText t)
    {
        var block = new TextBlock
        {
            Text = t.Text,
            FontSize = t.FontSize,
            FontWeight = (FontWeight)Math.Clamp(t.FontWeight, 100, 950),
            Foreground = new SolidColorBrush(Parse(t.Fill, 1)),
            TextAlignment = t.Anchor switch { ArtTextAnchor.Middle => TextAlignment.Center, ArtTextAnchor.End => TextAlignment.Right, _ => TextAlignment.Left },
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Width = TextBox
        };
        if (!string.IsNullOrWhiteSpace(t.FontFamily)) block.FontFamily = new FontFamily(t.FontFamily);

        var box = new Border { Width = TextBox, Height = t.FontSize * 2, Child = block, IsHitTestVisible = false };
        Canvas.SetLeft(box, t.Anchor switch { ArtTextAnchor.Middle => t.X - TextBox / 2, ArtTextAnchor.End => t.X - TextBox, _ => t.X });
        Canvas.SetTop(box, t.CenterY - t.FontSize);
        Apply(box, t);
        return box;
    }

    private static void Apply(Control c, ArtNode n)
    {
        c.Opacity = n.Opacity;
        if (!n.Transform.IsIdentity)
        {
            c.RenderTransformOrigin = RelativePoint.TopLeft; // Avalonia defaults to the centre; SVG transforms are about the drawing's origin
            c.RenderTransform = new MatrixTransform(ToMatrix(n.Transform));
        }
        if (n.Blur > 0) c.Effect = new BlurEffect { Radius = n.Blur };
        if (n.ClipPath != null) c.Clip = Geometry.Parse(n.ClipPath);
    }

    public static Matrix ToMatrix(ArtMatrix m) => new(m.A, m.B, m.C, m.D, m.E, m.F);

    private IBrush? Brush(ArtPaint? paint, double opacity)
    {
        if (paint == null) return null;
        var key = (paint, opacity);
        if (_brushes.TryGetValue(key, out var cached)) return cached;

        IBrush brush = paint switch
        {
            ArtSolid s => new SolidColorBrush(Parse(s.Color, opacity)),
            ArtLinearGradient l => new LinearGradientBrush
            {
                StartPoint = new RelativePoint(l.X1, l.Y1, l.UserSpace ? RelativeUnit.Absolute : RelativeUnit.Relative),
                EndPoint = new RelativePoint(l.X2, l.Y2, l.UserSpace ? RelativeUnit.Absolute : RelativeUnit.Relative),
                GradientStops = Stops(l.Stops, opacity)
            },
            ArtRadialGradient r => new RadialGradientBrush
            {
                Center = new RelativePoint(r.Cx, r.Cy, r.UserSpace ? RelativeUnit.Absolute : RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(r.Fx, r.Fy, r.UserSpace ? RelativeUnit.Absolute : RelativeUnit.Relative),
                RadiusX = new RelativeScalar(r.Radius, r.UserSpace ? RelativeUnit.Absolute : RelativeUnit.Relative),
                RadiusY = new RelativeScalar(r.Radius, r.UserSpace ? RelativeUnit.Absolute : RelativeUnit.Relative),
                GradientStops = Stops(r.Stops, opacity)
            },
            _ => Brushes.Transparent
        };

        _brushes[key] = brush;
        return brush;
    }

    private static GradientStops Stops(IReadOnlyList<ArtStop> stops, double opacity)
    {
        var result = new GradientStops();
        foreach (var s in stops) result.Add(new GradientStop(Parse(s.Color, s.Opacity * opacity), s.Offset));
        return result;
    }

    internal static Color Parse(string color, double opacity)
    {
        var c = Color.TryParse(color, out var parsed) ? parsed : Colors.Transparent;
        return opacity >= 1 ? c : Color.FromArgb((byte)Math.Round(c.A * Math.Clamp(opacity, 0, 1)), c.R, c.G, c.B);
    }
}
