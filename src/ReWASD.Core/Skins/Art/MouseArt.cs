using System.Globalization;
using System.Text;

namespace ReWASD.Core.Skins.Art;

/// <summary>
/// Draws a wired gaming mouse seen from above, as an SVG in the same style as the controller drawings: a matte domed shell, two main
/// buttons split by a seam, a ribbed wheel in a recessed slot, rubber grips and two thumb buttons on the left flank. Every button is a
/// part (<c>data-key</c> with the mouse source name) that sinks and lights when pressed; the wheel's top and bottom light for a notch
/// up or down.
/// </summary>
public static class MouseArt
{
    private const double Cx = 300;          // the mouse is symmetric about this line
    private const double SeamHalf = 3;      // half the gap between the two main buttons
    private const double ButtonInner = 29;  // the main buttons stop this far either side of the centre, leaving room for the wheel slot
    private const double SlotTop = 134, SlotBottom = 322, SlotHalf = 23;
    private const double WheelTop = 150, WheelBottom = 306, WheelHalf = 13;
    private const double Gap = 3;           // half the seam between the buttons and the palm

    /// <summary>Where the buttons end and the palm begins: a gentle curve, flat across the middle.</summary>
    public static double PalmY(double x)
    {
        double d = Math.Max(0, Math.Abs(x - Cx) - ButtonInner);
        return 352 + 0.0021 * d * d;
    }

    // The outline, clockwise from the front centre: (start, control 1, control 2, end) for each cubic segment
    private static readonly (double X, double Y)[][] Segments =
    {
        new[] { (300.0, 78.0), (390.0, 78.0), (452.0, 120.0), (468.0, 215.0) },
        new[] { (468.0, 215.0), (482.0, 300.0), (486.0, 430.0), (478.0, 540.0) },
        new[] { (478.0, 540.0), (470.0, 680.0), (410.0, 822.0), (300.0, 822.0) },
        new[] { (300.0, 822.0), (190.0, 822.0), (130.0, 680.0), (122.0, 540.0) },
        new[] { (122.0, 540.0), (114.0, 430.0), (118.0, 300.0), (132.0, 215.0) },
        new[] { (132.0, 215.0), (148.0, 120.0), (210.0, 78.0), (300.0, 78.0) }
    };

    /// <summary>Points along the outline of the body, clockwise from the front centre (the first point is not repeated at the end).</summary>
    public static IReadOnlyList<(double X, double Y)> Outline { get; } = BuildOutline();

    private static List<(double X, double Y)> BuildOutline()
    {
        const int PerSegment = 64;
        var points = new List<(double, double)>();
        foreach (var s in Segments)
        {
            for (int i = 0; i < PerSegment; i++)
            {
                double t = (double)i / PerSegment, u = 1 - t;
                double x = u * u * u * s[0].X + 3 * u * u * t * s[1].X + 3 * u * t * t * s[2].X + t * t * t * s[3].X;
                double y = u * u * u * s[0].Y + 3 * u * u * t * s[1].Y + 3 * u * t * t * s[2].Y + t * t * t * s[3].Y;
                points.Add((x, y));
            }
        }
        return points;
    }

    /// <summary>The left main button's outline. The right one is its mirror image.</summary>
    public static IReadOnlyList<(double X, double Y)> LeftButton { get; } = BuildLeftButton();

    public static IReadOnlyList<(double X, double Y)> RightButton { get; } = LeftButton.Select(p => (2 * Cx - p.X, p.Y)).Reverse().ToList();

    private static List<(double X, double Y)> BuildLeftButton()
    {
        double seamX = Cx - SeamHalf, innerX = Cx - ButtonInner;
        var outline = Outline;
        var pts = new List<(double X, double Y)>();

        // Along the outline from the front centre, going left (anticlockwise), until the palm line
        int n = outline.Count;
        double crossX = innerX;
        for (int k = 0; k < n; k++)
        {
            var p = outline[(n - k) % n];
            if (p.X >= seamX) continue;                  // still right of the seam at the very front
            if (p.Y >= PalmY(p.X) - Gap) { crossX = p.X; break; }
            pts.Add(p);
        }

        // Back along the palm line to the wheel slot, up the side of the slot, and across to the seam
        for (double x = crossX; x < innerX; x += 3) pts.Add((x, PalmY(x) - Gap));
        pts.Add((innerX, PalmY(innerX) - Gap));
        pts.Add((innerX, SlotTop - 6));
        pts.Add((seamX, SlotTop - 6));
        pts.Insert(0, (seamX, TopAt(seamX)));
        return pts;
    }

    /// <summary>The palm: everything behind the palm line.</summary>
    public static IReadOnlyList<(double X, double Y)> Palm { get; } = BuildPalm();

    private static List<(double X, double Y)> BuildPalm()
    {
        var below = Outline.Where(p => p.Y > PalmY(p.X) + Gap).ToList();     // contiguous: down the right side, round the back, up the left
        double leftX = below.Last().X, rightX = below.First().X;
        var pts = new List<(double X, double Y)>(below);
        for (double x = leftX; x < rightX; x += 3) pts.Add((x, PalmY(x) + Gap));
        return pts;
    }

    /// <summary>The y of the front of the outline at a given x (the top edge).</summary>
    private static double TopAt(double x)
    {
        var front = Outline.Where(p => p.Y < 200).OrderBy(p => Math.Abs(p.X - x)).First();
        return front.Y;
    }

    /// <summary>The x of the left edge of the outline at a given y.</summary>
    public static double LeftEdgeAt(double y) => Outline.Where(p => p.X < Cx).OrderBy(p => Math.Abs(p.Y - y)).First().X;

    /// <summary>A thumb button's rectangle: (x, y, width, height). Forward is the front one.</summary>
    public static (double X, double Y, double W, double H) ThumbButton(bool forward)
    {
        double top = forward ? 412 : 490, h = 68;
        double x = Math.Max(LeftEdgeAt(top), LeftEdgeAt(top + h)) + 5;
        return (x, top, 19, h);
    }

    // The three parts of the wheel, top to bottom: a notch up, a press, a notch down
    public static (double Top, double Bottom) WheelUpBand => (WheelTop, 202);
    public static (double Top, double Bottom) WheelPressBand => (202, 254);
    public static (double Top, double Bottom) WheelDownBand => (254, WheelBottom);

    public static string Svg()
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"70 -50 460 940\" id=\"mouse\">\n");
        sb.Append("<!-- Wired gaming mouse, top view, drawn for ReWASD. -->\n");
        sb.Append("<defs>\n");
        sb.Append("<radialGradient id=\"g-palm\" cx=\"0.46\" cy=\"0.42\" r=\"0.75\"><stop offset=\"0\" stop-color=\"#383B43\"/><stop offset=\"0.55\" stop-color=\"#25272C\"/><stop offset=\"1\" stop-color=\"#141518\"/></radialGradient>\n");
        sb.Append("<linearGradient id=\"g-button-l\" x1=\"0.2\" y1=\"0\" x2=\"0.8\" y2=\"1\"><stop offset=\"0\" stop-color=\"#3A3D45\"/><stop offset=\"0.6\" stop-color=\"#2A2C32\"/><stop offset=\"1\" stop-color=\"#1E2024\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-button-r\" x1=\"0.8\" y1=\"0\" x2=\"0.2\" y2=\"1\"><stop offset=\"0\" stop-color=\"#373A42\"/><stop offset=\"0.6\" stop-color=\"#282A30\"/><stop offset=\"1\" stop-color=\"#1C1E22\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-wheel\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#0E0F11\"/><stop offset=\"0.16\" stop-color=\"#2A2C31\"/><stop offset=\"0.5\" stop-color=\"#43464D\"/><stop offset=\"0.84\" stop-color=\"#2A2C31\"/><stop offset=\"1\" stop-color=\"#0E0F11\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-wheel-side\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#000000\" stop-opacity=\"0.55\"/><stop offset=\"0.3\" stop-color=\"#000000\" stop-opacity=\"0\"/><stop offset=\"0.7\" stop-color=\"#000000\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#000000\" stop-opacity=\"0.55\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-thumb\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#22242A\"/><stop offset=\"0.5\" stop-color=\"#4A4E57\"/><stop offset=\"1\" stop-color=\"#2C2E34\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-grip\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#111215\"/><stop offset=\"1\" stop-color=\"#1F2125\" stop-opacity=\"0\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-grip-r\" x1=\"1\" y1=\"0\" x2=\"0\" y2=\"0\"><stop offset=\"0\" stop-color=\"#111215\"/><stop offset=\"1\" stop-color=\"#1F2125\" stop-opacity=\"0\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-cable\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#0B0C0E\"/><stop offset=\"0.45\" stop-color=\"#2B2D32\"/><stop offset=\"1\" stop-color=\"#0B0C0E\"/></linearGradient>\n");
        sb.Append("<filter id=\"f-shadow\"><feGaussianBlur stdDeviation=\"22\"/></filter>\n");
        sb.Append("<filter id=\"f-contact\"><feGaussianBlur stdDeviation=\"6\"/></filter>\n");
        sb.Append("<filter id=\"f-soft\"><feGaussianBlur stdDeviation=\"16\"/></filter>\n");
        sb.Append("<filter id=\"f-inner\"><feGaussianBlur stdDeviation=\"3\"/></filter>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"c-body\"><path d=\"{Poly(Outline)}\"/></clipPath>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"c-slot\"><rect x=\"{N(Cx - SlotHalf)}\" y=\"{N(SlotTop)}\" width=\"{N(2 * SlotHalf)}\" height=\"{N(SlotBottom - SlotTop)}\" rx=\"{N(SlotHalf)}\"/></clipPath>\n");
        sb.Append("</defs>\n");

        string body = Poly(Outline);

        // The cable, leaving from the front, and its strain relief
        sb.Append("<path d=\"M 300 70 C 300 26 312 -6 334 -60\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.5\" stroke-width=\"14\" stroke-linecap=\"round\" filter=\"url(#f-contact)\"/>\n");
        sb.Append("<path d=\"M 300 70 C 300 26 312 -6 334 -60\" fill=\"none\" stroke=\"#121316\" stroke-width=\"10\" stroke-linecap=\"round\"/>\n");
        sb.Append("<path d=\"M 297.5 70 C 297.5 26 309.5 -6 331.5 -60\" fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.10\" stroke-width=\"2\" stroke-linecap=\"round\"/>\n");
        sb.Append("<rect x=\"290\" y=\"56\" width=\"20\" height=\"28\" rx=\"7\" fill=\"#18191C\" stroke=\"#070708\" stroke-width=\"1\"/>\n");

        // Shadows: a wide soft one and a tight one where the mouse meets the desk
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{body}\" transform=\"translate(0 22)\" fill=\"#000000\" opacity=\"0.7\" filter=\"url(#f-shadow)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{body}\" transform=\"translate(0 6)\" fill=\"#000000\" opacity=\"0.85\" filter=\"url(#f-contact)\"/>\n");

        // The base under the shell: what shows through the seams
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{body}\" fill=\"#060607\" stroke=\"#000000\" stroke-width=\"1.5\"/>\n");

        // Palm, with rubber grips down both flanks
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{Poly(Palm)}\" fill=\"url(#g-palm)\"/>\n");
        sb.Append("<g clip-path=\"url(#c-body)\">\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{Grip(left: true)}\" fill=\"url(#g-grip)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{Grip(left: false)}\" fill=\"url(#g-grip-r)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{GripTexture(left: true)}\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.3\" stroke-width=\"1\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{GripTexture(left: false)}\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.3\" stroke-width=\"1\"/>\n");
        sb.Append("</g>\n");

        // The wheel slot, sunk into the shell
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(Cx - SlotHalf)}\" y=\"{N(SlotTop)}\" width=\"{N(2 * SlotHalf)}\" height=\"{N(SlotBottom - SlotTop)}\" rx=\"{N(SlotHalf)}\" fill=\"#030304\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#c-slot)\"><rect x=\"{N(Cx - SlotHalf)}\" y=\"{N(SlotTop)}\" width=\"{N(2 * SlotHalf)}\" height=\"{N(SlotBottom - SlotTop)}\" rx=\"{N(SlotHalf)}\" fill=\"none\" stroke=\"#000000\" stroke-width=\"8\" filter=\"url(#f-inner)\"/></g>\n");

        // The two main buttons
        AppendButton(sb, "Left Click", "Left button", LeftButton, "url(#g-button-l)", "200 350");
        AppendButton(sb, "Right Click", "Right button", RightButton, "url(#g-button-r)", "400 350");

        // The wheel: the whole wheel is the press; its top and bottom are a notch up and down
        double wl = Cx - WheelHalf, ww = 2 * WheelHalf, wh = WheelBottom - WheelTop;
        sb.Append(CultureInfo.InvariantCulture,
            $"<g id=\"wheel-press\" data-key=\"Middle Click\" data-name=\"Wheel press\" data-anim=\"press\" data-origin=\"{N(Cx)} {N(WheelBottom)}\" data-press-scale=\"0.97\" data-press-dy=\"3\">\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(wl)}\" y=\"{N(WheelTop)}\" width=\"{N(ww)}\" height=\"{N(wh)}\" rx=\"12\" fill=\"url(#g-wheel)\" stroke=\"#000000\" stroke-width=\"1\"/>\n");
        for (double y = WheelTop + 7; y < WheelBottom - 4; y += 7)
        {
            double edge = Math.Min(y - WheelTop, WheelBottom - y) / (wh / 2);          // ridges fade toward the ends, where the wheel turns away
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{N(wl + 2)}\" y1=\"{N(y)}\" x2=\"{N(wl + ww - 2)}\" y2=\"{N(y)}\" stroke=\"#08090A\" stroke-opacity=\"{N(0.35 + 0.5 * edge)}\" stroke-width=\"2.2\"/>\n");
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{N(wl + 2)}\" y1=\"{N(y + 1.6)}\" x2=\"{N(wl + ww - 2)}\" y2=\"{N(y + 1.6)}\" stroke=\"#FFFFFF\" stroke-opacity=\"{N(0.04 + 0.06 * edge)}\" stroke-width=\"1\"/>\n");
        }
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(wl)}\" y=\"{N(WheelTop)}\" width=\"{N(ww)}\" height=\"{N(wh)}\" rx=\"12\" fill=\"url(#g-wheel-side)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(wl)}\" y=\"{N(WheelTop)}\" width=\"{N(ww)}\" height=\"{N(wh)}\" rx=\"12\" fill=\"#E9C46A\" data-fx=\"glow\" data-max=\"0.38\" opacity=\"0\"/>\n");
        Hit(sb, wl - 6, WheelPressBand.Top, ww + 12, WheelPressBand.Bottom - WheelPressBand.Top, 0);
        sb.Append("</g>\n");

        AppendWheelNotch(sb, "Wheel Up", "Wheel up", WheelUpBand, wl, ww);
        AppendWheelNotch(sb, "Wheel Down", "Wheel down", WheelDownBand, wl, ww);

        // Thumb buttons on the left flank
        AppendThumb(sb, "Mouse Button 5", "Forward (side button)", forward: true);
        AppendThumb(sb, "Mouse Button 4", "Back (side button)", forward: false);

        // The dome: light across the top from the upper left, the shell turning away into shadow at its edges, and a faint rim of light
        sb.Append("<g clip-path=\"url(#c-body)\">\n");
        sb.Append("<ellipse cx=\"268\" cy=\"330\" rx=\"120\" ry=\"260\" fill=\"#FFFFFF\" opacity=\"0.035\" filter=\"url(#f-soft)\"/>\n");
        sb.Append("<ellipse cx=\"240\" cy=\"230\" rx=\"38\" ry=\"90\" fill=\"#FFFFFF\" opacity=\"0.035\" filter=\"url(#f-soft)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{body}\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.55\" stroke-width=\"34\" filter=\"url(#f-soft)\"/>\n");
        sb.Append("</g>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{body}\" fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.07\" stroke-width=\"1.5\"/>\n");

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static void AppendButton(StringBuilder sb, string key, string name, IReadOnlyList<(double X, double Y)> shape, string fill, string origin)
    {
        string d = Poly(shape);
        sb.Append(CultureInfo.InvariantCulture,
            $"<g id=\"{Id(key)}\" data-key=\"{key}\" data-name=\"{name}\" data-anim=\"press\" data-origin=\"{origin}\" data-press-scale=\"0.995\" data-press-dy=\"2.5\">\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{d}\" fill=\"{fill}\" stroke=\"#050506\" stroke-width=\"1\"/>\n");
        // The front lip catches the light; the edge by the seam is in shadow
        sb.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#c-body)\"><path d=\"{d}\" fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.06\" stroke-width=\"2\"/></g>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{d}\" fill=\"#E9C46A\" data-fx=\"glow\" data-max=\"0.2\" opacity=\"0\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{d}\" fill=\"transparent\" class=\"hit\"/>\n");
        sb.Append("</g>\n");
    }

    private static void AppendWheelNotch(StringBuilder sb, string key, string name, (double Top, double Bottom) band, double wl, double ww)
    {
        sb.Append(CultureInfo.InvariantCulture,
            $"<g id=\"{Id(key)}\" data-key=\"{key}\" data-name=\"{name}\" data-anim=\"press\" data-origin=\"{N(Cx)} {N(band.Bottom)}\" data-press-scale=\"1\" data-press-dy=\"0\">\n");
        double r = key == "Wheel Up" ? 12 : 0;
        sb.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"{N(wl)}\" y=\"{N(band.Top)}\" width=\"{N(ww)}\" height=\"{N(band.Bottom - band.Top)}\" rx=\"{N(Math.Max(r, 12))}\" fill=\"#E9C46A\" data-fx=\"glow\" data-max=\"0.42\" opacity=\"0\"/>\n");
        Hit(sb, wl - 6, band.Top, ww + 12, band.Bottom - band.Top, 0);
        sb.Append("</g>\n");
    }

    private static void AppendThumb(StringBuilder sb, string key, string name, bool forward)
    {
        var (x, y, w, h) = ThumbButton(forward);
        sb.Append(CultureInfo.InvariantCulture,
            $"<g id=\"{Id(key)}\" data-key=\"{key}\" data-name=\"{name}\" data-anim=\"press\" data-origin=\"{N(x)} {N(y + h / 2)}\" data-press-scale=\"0.96\" data-press-dy=\"0\">\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(x - 1)}\" y=\"{N(y + 3)}\" width=\"{N(w + 2)}\" height=\"{N(h)}\" rx=\"{N(w / 2)}\" fill=\"#000000\" opacity=\"0.8\" filter=\"url(#f-contact)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(w / 2)}\" fill=\"url(#g-thumb)\" stroke=\"#050506\" stroke-width=\"1\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(x + 3)}\" y=\"{N(y + 4)}\" width=\"{N(w - 6)}\" height=\"{N(h - 8)}\" rx=\"{N(w / 2 - 3)}\" fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.10\" stroke-width=\"1\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(w / 2)}\" fill=\"#E9C46A\" data-fx=\"glow\" data-max=\"0.45\" opacity=\"0\"/>\n");
        Hit(sb, x - 4, y - 2, w + 8, h + 4, w / 2);
        sb.Append("</g>\n");
    }

    private static void Hit(StringBuilder sb, double x, double y, double w, double h, double r) =>
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(r)}\" fill=\"transparent\" class=\"hit\"/>\n");

    /// <summary>A rubber grip down one flank of the palm: the outline there, and a line 26 units inside it.</summary>
    private static string Grip(bool left)
    {
        var edge = Outline.Where(p => (left ? p.X < Cx : p.X > Cx) && p.Y > PalmY(p.X) + 30 && p.Y < 640).OrderBy(p => p.Y).ToList();
        var pts = new List<(double X, double Y)>(edge);
        for (int i = edge.Count - 1; i >= 0; i--) pts.Add((edge[i].X + (left ? 26 : -26), edge[i].Y));
        return Poly(pts);
    }

    /// <summary>Short slanted ribs across a grip.</summary>
    private static string GripTexture(bool left)
    {
        var sb = new StringBuilder();
        foreach (var p in Outline.Where(p => (left ? p.X < Cx : p.X > Cx) && p.Y > PalmY(p.X) + 36 && p.Y < 630).Where((_, i) => i % 2 == 0))
        {
            double x0 = p.X + (left ? 4 : -4), x1 = p.X + (left ? 24 : -24);
            sb.Append(CultureInfo.InvariantCulture, $"M {N(x0)} {N(p.Y + 3)} L {N(x1)} {N(p.Y - 3)} ");
        }
        return sb.ToString().TrimEnd();
    }

    private static string Poly(IReadOnlyList<(double X, double Y)> pts)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"M {N(pts[0].X)} {N(pts[0].Y)}");
        for (int i = 1; i < pts.Count; i++) sb.Append(CultureInfo.InvariantCulture, $" L {N(pts[i].X)} {N(pts[i].Y)}");
        sb.Append(" Z");
        return sb.ToString();
    }

    private static string Id(string key) => key.ToLowerInvariant().Replace(' ', '-');
    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
