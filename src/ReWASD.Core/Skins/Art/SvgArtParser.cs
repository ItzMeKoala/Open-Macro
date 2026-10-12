using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReWASD.Core.Models;

namespace ReWASD.Core.Skins.Art;

/// <summary>
/// Reads the small, toolkit-neutral subset of SVG the controller drawings use: groups, paths, basic shapes, solid and
/// gradient paints, transforms, one clip path and a Gaussian blur per node. Anything else is ignored, so a drawing
/// that looks right in a browser can be shown by any front end that renders this model.
///
/// Parts are groups with a <c>data-button</c> attribute naming a <see cref="ControllerButton"/>, or a <c>data-key</c> attribute
/// naming a keyboard key; see <see cref="ArtPart"/>.
/// </summary>
public static class SvgArtParser
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    public static ArtScene Parse(string svg)
    {
        var doc = XDocument.Parse(svg);
        var root = doc.Root ?? throw new FormatException("The drawing has no root element");

        var (originX, originY, width, height) = ReadSize(root);
        var defs = new Defs(root);

        var rootGroup = new ArtGroup { Id = root.Attribute("id")?.Value };
        foreach (var child in root.Elements())
        {
            var node = ParseNode(child, defs);
            if (node != null) rootGroup.Children.Add(node);
        }

        var parts = new List<ArtPart>();
        Collect(rootGroup, parts);
        return new ArtScene { OriginX = originX, OriginY = originY, Width = width, Height = height, Root = rootGroup, Parts = parts };
    }

    // ---- defs --------------------------------------------------------------------------------------------------------

    private sealed class Defs
    {
        public readonly Dictionary<string, ArtPaint> Paints = new();
        public readonly Dictionary<string, double> Blurs = new();
        public readonly Dictionary<string, string> Clips = new();

        public Defs(XElement root)
        {
            foreach (var e in root.Descendants())
            {
                string? id = e.Attribute("id")?.Value;
                if (id == null) continue;

                switch (e.Name.LocalName)
                {
                    case "linearGradient":
                        Paints[id] = new ArtLinearGradient(
                            Frac(e, "x1", 0), Frac(e, "y1", 0), Frac(e, "x2", 1), Frac(e, "y2", 0), UserSpace(e), Stops(e));
                        break;
                    case "radialGradient":
                    {
                        double cx = Frac(e, "cx", .5), cy = Frac(e, "cy", .5);
                        Paints[id] = new ArtRadialGradient(cx, cy, Frac(e, "r", .5), Frac(e, "fx", cx), Frac(e, "fy", cy), UserSpace(e), Stops(e));
                        break;
                    }
                    case "filter":
                    {
                        var blur = e.Elements().FirstOrDefault(c => c.Name.LocalName == "feGaussianBlur");
                        if (blur != null) Blurs[id] = Num(blur.Attribute("stdDeviation")?.Value, 0);
                        break;
                    }
                    case "clipPath":
                    {
                        var shape = e.Elements().Select(c => ShapeData(c)).FirstOrDefault(d => d != null);
                        if (shape != null) Clips[id] = shape;
                        break;
                    }
                }
            }
        }

        private static bool UserSpace(XElement e) => e.Attribute("gradientUnits")?.Value == "userSpaceOnUse";

        private static double Frac(XElement e, string name, double fallback)
        {
            string? v = e.Attribute(name)?.Value;
            if (v == null) return fallback;
            return v.EndsWith('%') ? Num(v[..^1], 0) / 100 : Num(v, fallback);
        }

        private static List<ArtStop> Stops(XElement e) =>
            e.Elements().Where(c => c.Name.LocalName == "stop").Select(c =>
            {
                double offset = c.Attribute("offset")?.Value is { } o ? (o.EndsWith('%') ? Num(o[..^1], 0) / 100 : Num(o, 0)) : 0;
                return new ArtStop(offset, c.Attribute("stop-color")?.Value ?? "#000000", Num(c.Attribute("stop-opacity")?.Value, 1));
            }).ToList();
    }

    // ---- nodes -------------------------------------------------------------------------------------------------------

    private static ArtNode? ParseNode(XElement e, Defs defs)
    {
        string name = e.Name.LocalName;
        if (name is "defs" or "title" or "desc" or "style" or "linearGradient" or "radialGradient" or "filter" or "clipPath") return null;

        var data = e.Attributes().Where(a => a.Name.LocalName.StartsWith("data-", StringComparison.Ordinal))
            .ToDictionary(a => a.Name.LocalName[5..], a => a.Value);
        string? id = e.Attribute("id")?.Value;
        string? cls = e.Attribute("class")?.Value;
        double opacity = Num(e.Attribute("opacity")?.Value, 1);
        var transform = ParseTransform(e.Attribute("transform")?.Value);
        string? clip = Ref(e.Attribute("clip-path")?.Value) is { } clipId && defs.Clips.TryGetValue(clipId, out var clipData) ? clipData : null;
        double blur = Ref(e.Attribute("filter")?.Value) is { } filterId && defs.Blurs.TryGetValue(filterId, out var b) ? b : 0;

        if (name == "g")
        {
            var group = new ArtGroup { Id = id, Class = cls, Opacity = opacity, Transform = transform, ClipPath = clip, Blur = blur, Data = data };
            foreach (var child in e.Elements())
            {
                var node = ParseNode(child, defs);
                if (node != null) group.Children.Add(node);
            }
            return group;
        }

        if (name == "text")
        {
            string text = e.Value.Trim();
            if (text.Length == 0) return null;
            return new ArtText
            {
                Id = id, Class = cls, Opacity = opacity, Transform = transform, ClipPath = clip, Blur = blur, Data = data,
                Text = text,
                X = Num(e.Attribute("x")?.Value, 0),
                Y = Num(e.Attribute("y")?.Value, 0),
                FontSize = Num(e.Attribute("font-size")?.Value, 12),
                FontWeight = (int)Num(e.Attribute("font-weight")?.Value switch { "bold" => "700", "normal" => "400", var w => w }, 400),
                Fill = e.Attribute("fill")?.Value ?? "#000000",
                FontFamily = e.Attribute("font-family")?.Value,
                Anchor = e.Attribute("text-anchor")?.Value switch { "middle" => ArtTextAnchor.Middle, "end" => ArtTextAnchor.End, _ => ArtTextAnchor.Start },
                Central = e.Attribute("dominant-baseline")?.Value is "central" or "middle"
            };
        }

        string? d = ShapeData(e);
        if (d == null) return null;

        var fill = ParsePaint(e.Attribute("fill")?.Value ?? "#000000", defs);
        var stroke = ParsePaint(e.Attribute("stroke")?.Value ?? "none", defs);

        return new ArtPath
        {
            Id = id, Class = cls, Opacity = opacity, Transform = transform, ClipPath = clip, Blur = blur, Data = data,
            D = d,
            Fill = fill,
            FillOpacity = Num(e.Attribute("fill-opacity")?.Value, 1),
            Stroke = stroke,
            StrokeOpacity = Num(e.Attribute("stroke-opacity")?.Value, 1),
            StrokeWidth = Num(e.Attribute("stroke-width")?.Value, 1),
            LineCap = e.Attribute("stroke-linecap")?.Value switch { "round" => ArtLineCap.Round, "square" => ArtLineCap.Square, _ => ArtLineCap.Butt },
            LineJoin = e.Attribute("stroke-linejoin")?.Value switch { "round" => ArtLineJoin.Round, "bevel" => ArtLineJoin.Bevel, _ => ArtLineJoin.Miter },
            DashArray = ParseDash(e.Attribute("stroke-dasharray")?.Value)
        };
    }

    /// <summary>The outline of a shape element as absolute path data, or null if the element is not a shape.</summary>
    private static string? ShapeData(XElement e)
    {
        double A(string n, double fb = 0) => Num(e.Attribute(n)?.Value, fb);
        string N(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        switch (e.Name.LocalName)
        {
            case "path":
                return e.Attribute("d")?.Value;
            case "circle":
                return Ellipse(A("cx"), A("cy"), A("r"), A("r"));
            case "ellipse":
                return Ellipse(A("cx"), A("cy"), A("rx"), A("ry"));
            case "rect":
            {
                double x = A("x"), y = A("y"), w = A("width"), h = A("height");
                double rx = Math.Min(A("rx", A("ry")), w / 2), ry = Math.Min(A("ry", A("rx")), h / 2);
                if (rx <= 0 || ry <= 0) return $"M {N(x)} {N(y)} L {N(x + w)} {N(y)} L {N(x + w)} {N(y + h)} L {N(x)} {N(y + h)} Z";
                const double K = 0.5522847498;
                double kx = rx * K, ky = ry * K;
                return $"M {N(x + rx)} {N(y)} L {N(x + w - rx)} {N(y)} C {N(x + w - rx + kx)} {N(y)} {N(x + w)} {N(y + ry - ky)} {N(x + w)} {N(y + ry)} " +
                       $"L {N(x + w)} {N(y + h - ry)} C {N(x + w)} {N(y + h - ry + ky)} {N(x + w - rx + kx)} {N(y + h)} {N(x + w - rx)} {N(y + h)} " +
                       $"L {N(x + rx)} {N(y + h)} C {N(x + rx - kx)} {N(y + h)} {N(x)} {N(y + h - ry + ky)} {N(x)} {N(y + h - ry)} " +
                       $"L {N(x)} {N(y + ry)} C {N(x)} {N(y + ry - ky)} {N(x + rx - kx)} {N(y)} {N(x + rx)} {N(y)} Z";
            }
            case "line":
                return $"M {N(A("x1"))} {N(A("y1"))} L {N(A("x2"))} {N(A("y2"))}";
            case "polygon":
            case "polyline":
            {
                var pts = Regex.Matches(e.Attribute("points")?.Value ?? "", @"-?\d*\.?\d+(?:[eE][-+]?\d+)?")
                    .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
                if (pts.Count < 4) return null;
                var sb = new System.Text.StringBuilder($"M {N(pts[0])} {N(pts[1])}");
                for (int i = 2; i + 1 < pts.Count; i += 2) sb.Append($" L {N(pts[i])} {N(pts[i + 1])}");
                if (e.Name.LocalName == "polygon") sb.Append(" Z");
                return sb.ToString();
            }
            default:
                return null;
        }

        string Ellipse(double cx, double cy, double rx, double ry)
        {
            const double K = 0.5522847498;
            double kx = rx * K, ky = ry * K;
            return $"M {N(cx + rx)} {N(cy)} C {N(cx + rx)} {N(cy + ky)} {N(cx + kx)} {N(cy + ry)} {N(cx)} {N(cy + ry)} " +
                   $"C {N(cx - kx)} {N(cy + ry)} {N(cx - rx)} {N(cy + ky)} {N(cx - rx)} {N(cy)} " +
                   $"C {N(cx - rx)} {N(cy - ky)} {N(cx - kx)} {N(cy - ry)} {N(cx)} {N(cy - ry)} " +
                   $"C {N(cx + kx)} {N(cy - ry)} {N(cx + rx)} {N(cy - ky)} {N(cx + rx)} {N(cy)} Z";
        }
    }

    // ---- attribute values ---------------------------------------------------------------------------------------------

    private static ArtPaint? ParsePaint(string value, Defs defs)
    {
        value = value.Trim();
        if (value is "" or "none") return null;
        if (Ref(value) is { } id) return defs.Paints.TryGetValue(id, out var p) ? p : null;
        return new ArtSolid(value);
    }

    /// <summary>The id inside <c>url(#id)</c>, or null.</summary>
    private static string? Ref(string? value)
    {
        if (value == null) return null;
        var m = Regex.Match(value, @"url\(\s*#([^)\s]+)\s*\)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static double Num(string? s, double fallback) =>
        s != null && double.TryParse(s.Trim().TrimEnd('p', 'x'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static IReadOnlyList<double>? ParseDash(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "none") return null;
        var list = Regex.Matches(s, @"-?\d*\.?\d+").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        return list.Count == 0 ? null : list;
    }

    private static (double X, double Y, double W, double H) ReadSize(XElement root)
    {
        var vb = root.Attribute("viewBox")?.Value;
        if (vb != null)
        {
            var n = Regex.Matches(vb, @"-?\d*\.?\d+").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            if (n.Count == 4) return (n[0], n[1], n[2], n[3]);
        }
        return (0, 0, Num(root.Attribute("width")?.Value, 100), Num(root.Attribute("height")?.Value, 100));
    }

    public static ArtMatrix ParseTransform(string? s)
    {
        var m = ArtMatrix.Identity;
        if (string.IsNullOrWhiteSpace(s)) return m;

        foreach (Match call in Regex.Matches(s, @"(\w+)\s*\(([^)]*)\)"))
        {
            var a = Regex.Matches(call.Groups[2].Value, @"-?\d*\.?\d+(?:[eE][-+]?\d+)?")
                .Select(x => double.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray();
            ArtMatrix t = call.Groups[1].Value switch
            {
                "translate" => ArtMatrix.Translate(a.ElementAtOrDefault(0), a.ElementAtOrDefault(1)),
                "scale" => ArtMatrix.Scale(a.ElementAtOrDefault(0), a.Length > 1 ? a[1] : a.ElementAtOrDefault(0)),
                "rotate" when a.Length >= 3 => ArtMatrix.Translate(a[1], a[2]) * ArtMatrix.Rotate(a[0]) * ArtMatrix.Translate(-a[1], -a[2]),
                "rotate" => ArtMatrix.Rotate(a.ElementAtOrDefault(0)),
                "matrix" when a.Length == 6 => new ArtMatrix(a[0], a[1], a[2], a[3], a[4], a[5]),
                _ => ArtMatrix.Identity
            };
            m *= t; // later transforms apply to the coordinates first, as in SVG
        }
        return m;
    }

    // ---- parts -------------------------------------------------------------------------------------------------------

    private static void Collect(ArtGroup group, List<ArtPart> parts)
    {
        foreach (var child in group.Children)
        {
            if (child is not ArtGroup g) continue;

            string? button = g.Get("button");
            string? key = g.Get("key");
            if (button != null || key != null)
            {
                var b = ControllerButton.None;
                if (button != null && !Enum.TryParse(button, out b)) throw new FormatException($"Part '{g.Id}' names an unknown button '{button}'");

                var hits = new List<ArtPath>();
                FindHits(g, hits);
                if (hits.Count == 0) throw new FormatException($"Part '{g.Id}' has no shape with class \"hit\"");

                var bounds = hits.Select(h => HitBox(h)).Aggregate(ArtRect.Union);
                var origin = (g.Get("origin") ?? $"{bounds.CenterX} {bounds.CenterY}")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => Num(v, 0)).ToArray();

                parts.Add(new ArtPart
                {
                    Id = g.Id ?? button ?? key!,
                    Name = g.Get("name") ?? button ?? key!,
                    Button = b,
                    Key = key,
                    Motion = g.Get("anim") switch { "trigger" => ArtMotion.Trigger, "stick" => ArtMotion.Stick, _ => ArtMotion.Press },
                    Group = g,
                    Origin = (origin.ElementAtOrDefault(0), origin.ElementAtOrDefault(1)),
                    PressScale = Num(g.Get("press-scale"), 0.94),
                    PressDy = Num(g.Get("press-dy"), 1.5),
                    Travel = Num(g.Get("travel"), 0),
                    HitBounds = bounds
                });
            }
            else
            {
                Collect(g, parts);
            }
        }
    }

    private static void FindHits(ArtGroup group, List<ArtPath> hits)
    {
        foreach (var child in group.Children)
        {
            if (child is ArtPath p && p.IsHit) hits.Add(p);
            else if (child is ArtGroup g) FindHits(g, hits);
        }
    }

    private static ArtRect HitBox(ArtPath p)
    {
        var r = PathBounds.Of(p.D);
        if (p.Transform.IsIdentity) return r;
        var corners = new[] { p.Transform.Apply(r.X, r.Y), p.Transform.Apply(r.Right, r.Y), p.Transform.Apply(r.X, r.Bottom), p.Transform.Apply(r.Right, r.Bottom) };
        double x = corners.Min(c => c.X), y = corners.Min(c => c.Y);
        return new ArtRect(x, y, corners.Max(c => c.X) - x, corners.Max(c => c.Y) - y);
    }
}
