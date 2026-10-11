using System.Globalization;
using System.Text.RegularExpressions;
using ReWASD.Core.Models;

namespace ReWASD.Core.Skins;

/// <summary>Which controller picture to draw. This is purely visual; the emulated device is chosen separately.</summary>
public enum SkinKind
{
    Xbox,
    PlayStation4,
    PlayStation5,
    SwitchPro
}

/// <summary>How a button's face is drawn.</summary>
public enum IconShape
{
    Circle,
    Pill,
    KeyCap,
    Glyph
}

/// <summary>What is printed on a button and how it is shaped and coloured.</summary>
public readonly record struct ButtonFace(string Legend, string Color, IconShape Shape);

/// <summary>Where a button sits on the 700x440 drawing, by its centre.</summary>
public readonly record struct Placement(double Cx, double Cy, double W, double H);

/// <summary>A background shape of the drawing (the body, a touchpad, a stick well, a keyboard plate...).</summary>
public sealed record DecorShape(string Data, string Fill, string Stroke, double StrokeThickness = 1.5);

public sealed record SkinOption(SkinKind Kind, string Name);

public sealed class ControllerSkin
{
    public required SkinKind Kind { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyDictionary<ControllerButton, Placement> Layout { get; init; }
    public required IReadOnlyList<DecorShape> Decor { get; init; }
}

/// <summary>Vector controller pictures drawn for this project. They are generic drawings, not manufacturer artwork.</summary>
public static class ControllerSkins
{
    public const double CanvasWidth = 700;
    public const double CanvasHeight = 440;

    public static IReadOnlyList<SkinOption> Options { get; } = new[]
    {
        new SkinOption(SkinKind.Xbox, "Xbox"),
        new SkinOption(SkinKind.PlayStation4, "PlayStation 4"),
        new SkinOption(SkinKind.PlayStation5, "PlayStation 5"),
        new SkinOption(SkinKind.SwitchPro, "Switch Pro")
    };

    private static readonly Dictionary<SkinKind, ControllerSkin> Cache = new();

    public static ControllerSkin Get(SkinKind kind)
    {
        if (!Cache.TryGetValue(kind, out var skin))
        {
            skin = kind switch
            {
                SkinKind.PlayStation4 => BuildPlayStation(SkinKind.PlayStation4),
                SkinKind.PlayStation5 => BuildPlayStation(SkinKind.PlayStation5),
                SkinKind.SwitchPro => BuildSwitchPro(),
                _ => BuildXbox()
            };
            Cache[kind] = skin;
        }
        return skin;
    }

    // ---- Button faces ---------------------------------------------------------------------------------------------

    private const string Neutral = "#E0E6EE";
    private const string Muted = "#B8C0CC";

    public static ButtonFace Face(SkinKind kind, ControllerButton button)
    {
        switch (button)
        {
            case ControllerButton.DpadUp: return new("▲", Muted, IconShape.Circle);
            case ControllerButton.DpadDown: return new("▼", Muted, IconShape.Circle);
            case ControllerButton.DpadLeft: return new("◀", Muted, IconShape.Circle);
            case ControllerButton.DpadRight: return new("▶", Muted, IconShape.Circle);
        }

        return kind switch
        {
            SkinKind.PlayStation4 or SkinKind.PlayStation5 => PlayStationFace(kind, button),
            SkinKind.SwitchPro => SwitchFace(button),
            _ => XboxFace(button)
        };
    }

    private static ButtonFace XboxFace(ControllerButton b) => b switch
    {
        ControllerButton.A => new("A", "#6DBE45", IconShape.Circle),
        ControllerButton.B => new("B", "#E8483F", IconShape.Circle),
        ControllerButton.X => new("X", "#3D8BE8", IconShape.Circle),
        ControllerButton.Y => new("Y", "#F5C400", IconShape.Circle),
        ControllerButton.LeftBumper => new("LB", Neutral, IconShape.Pill),
        ControllerButton.RightBumper => new("RB", Neutral, IconShape.Pill),
        ControllerButton.LeftTrigger => new("LT", Neutral, IconShape.Pill),
        ControllerButton.RightTrigger => new("RT", Neutral, IconShape.Pill),
        ControllerButton.LeftStickClick => new("LS", Neutral, IconShape.Circle),
        ControllerButton.RightStickClick => new("RS", Neutral, IconShape.Circle),
        ControllerButton.View => new("⧉", Neutral, IconShape.Circle),
        ControllerButton.Menu => new("☰", Neutral, IconShape.Circle),
        ControllerButton.Guide => new("Ⓧ", "#7DD35A", IconShape.Circle),
        _ => new(b.ToString(), Neutral, IconShape.Pill)
    };

    private static ButtonFace PlayStationFace(SkinKind kind, ControllerButton b) => b switch
    {
        ControllerButton.A => new("✕", "#7FA8F5", IconShape.Circle),
        ControllerButton.B => new("○", "#F27272", IconShape.Circle),
        ControllerButton.X => new("□", "#E895CB", IconShape.Circle),
        ControllerButton.Y => new("△", "#5FD6A8", IconShape.Circle),
        ControllerButton.LeftBumper => new("L1", Neutral, IconShape.Pill),
        ControllerButton.RightBumper => new("R1", Neutral, IconShape.Pill),
        ControllerButton.LeftTrigger => new("L2", Neutral, IconShape.Pill),
        ControllerButton.RightTrigger => new("R2", Neutral, IconShape.Pill),
        ControllerButton.LeftStickClick => new("L3", Neutral, IconShape.Circle),
        ControllerButton.RightStickClick => new("R3", Neutral, IconShape.Circle),
        ControllerButton.View => new(kind == SkinKind.PlayStation5 ? "CREATE" : "SHARE", Muted, IconShape.Pill),
        ControllerButton.Menu => new("OPTIONS", Muted, IconShape.Pill),
        ControllerButton.Guide => new("PS", Neutral, IconShape.Circle),
        _ => new(b.ToString(), Neutral, IconShape.Pill)
    };

    private static ButtonFace SwitchFace(ControllerButton b) => b switch
    {
        ControllerButton.A => new("A", Neutral, IconShape.Circle),
        ControllerButton.B => new("B", Neutral, IconShape.Circle),
        ControllerButton.X => new("X", Neutral, IconShape.Circle),
        ControllerButton.Y => new("Y", Neutral, IconShape.Circle),
        ControllerButton.LeftBumper => new("L", Neutral, IconShape.Pill),
        ControllerButton.RightBumper => new("R", Neutral, IconShape.Pill),
        ControllerButton.LeftTrigger => new("ZL", Neutral, IconShape.Pill),
        ControllerButton.RightTrigger => new("ZR", Neutral, IconShape.Pill),
        ControllerButton.LeftStickClick => new("LS", Neutral, IconShape.Circle),
        ControllerButton.RightStickClick => new("RS", Neutral, IconShape.Circle),
        ControllerButton.View => new("−", Neutral, IconShape.Circle),
        ControllerButton.Menu => new("+", Neutral, IconShape.Circle),
        ControllerButton.Guide => new("⌂", Neutral, IconShape.Circle),
        _ => new(b.ToString(), Neutral, IconShape.Pill)
    };

    // ---- Geometry helpers -----------------------------------------------------------------------------------------

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Circle(double cx, double cy, double r) =>
        $"M {N(cx - r)},{N(cy)} a {N(r)},{N(r)} 0 1 0 {N(2 * r)},0 a {N(r)},{N(r)} 0 1 0 {N(-2 * r)},0 Z";

    public static string RoundRect(double x, double y, double w, double h, double r) =>
        $"M {N(x + r)},{N(y)} h {N(w - 2 * r)} a {N(r)},{N(r)} 0 0 1 {N(r)},{N(r)} v {N(h - 2 * r)} " +
        $"a {N(r)},{N(r)} 0 0 1 {N(-r)},{N(r)} h {N(-(w - 2 * r))} a {N(r)},{N(r)} 0 0 1 {N(-r)},{N(-r)} " +
        $"v {N(-(h - 2 * r))} a {N(r)},{N(r)} 0 0 1 {N(r)},{N(-r)} Z";

    /// <summary>A plus-shaped D-pad outline: <paramref name="half"/> is the length of each arm from the centre, <paramref name="arm"/> its half width.</summary>
    public static string Cross(double cx, double cy, double half, double arm) =>
        $"M {N(cx - arm)},{N(cy - half)} L {N(cx + arm)},{N(cy - half)} L {N(cx + arm)},{N(cy - arm)} L {N(cx + half)},{N(cy - arm)} " +
        $"L {N(cx + half)},{N(cy + arm)} L {N(cx + arm)},{N(cy + arm)} L {N(cx + arm)},{N(cy + half)} L {N(cx - arm)},{N(cy + half)} " +
        $"L {N(cx - arm)},{N(cy + arm)} L {N(cx - half)},{N(cy + arm)} L {N(cx - half)},{N(cy - arm)} L {N(cx - arm)},{N(cy - arm)} Z";

    private static readonly Regex PathToken = new(@"[MLCZmlcz]|-?\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// Reflects a path across the drawing's vertical centre line, so a left-hand shape is written once. Works on paths made
    /// of absolute M, L and C commands whose numbers come in x, y pairs, written with spaces or commas (not on
    /// <see cref="Circle"/> or <see cref="RoundRect"/>, which use relative arcs).
    /// </summary>
    public static string Mirror(string data)
    {
        var parts = new List<string>();
        int numberIndex = 0;

        foreach (Match token in PathToken.Matches(data))
        {
            if (char.IsLetter(token.Value[0]))
            {
                parts.Add(token.Value);
                numberIndex = 0;
                continue;
            }

            double value = double.Parse(token.Value, CultureInfo.InvariantCulture);
            parts.Add(N(numberIndex % 2 == 0 ? CanvasWidth - value : value)); // every first number of a pair is an x
            numberIndex++;
        }

        return string.Join(' ', parts);
    }

    // ---- Neon outline style ---------------------------------------------------------------------------------------

    private const string Clear = "Transparent";
    private const string Line = "#E9C46A";
    private const string Glow = "#2EE9C46A";
    private const string Dim = "#8F7B45";
    private const string DimGlow = "#16E9C46A";
    private const string BodyFill = "#0E0F13";

    /// <summary>An outline drawn as a soft wide stroke under a crisp one, which reads as a glowing line.</summary>
    public static IEnumerable<DecorShape> Neon(string data, double width = 2, bool dim = false, string fill = Clear)
    {
        if (fill != Clear) yield return new DecorShape(data, fill, Clear, 0);
        yield return new DecorShape(data, Clear, dim ? DimGlow : Glow, width + 6);
        yield return new DecorShape(data, Clear, dim ? Dim : Line, width);
    }

    private static IEnumerable<DecorShape> NeonPair(string left, double width = 2, bool dim = false) =>
        Neon(left, width, dim).Concat(Neon(Mirror(left), width, dim));

    private static Dictionary<ControllerButton, Placement> Layout(params (ControllerButton Button, double Cx, double Cy, double W, double H)[] items) =>
        items.ToDictionary(i => i.Button, i => new Placement(i.Cx, i.Cy, i.W, i.H));

    // Shapes every controller shares: the trigger humps, the bumpers and the grip highlights
    private const string TriggerHump = "M 176 90 C 172 62 190 44 216 44 C 242 44 256 62 254 90";
    private const string Bumper = "M 168 108 C 178 92 236 82 262 96 C 270 102 264 114 250 114 C 224 116 188 118 168 108 Z";
    private const string GripHighlight = "M 128 238 C 122 290 126 336 144 370";

    private static IEnumerable<DecorShape> Shoulders() =>
        NeonPair(TriggerHump).Concat(NeonPair(Bumper));

    private static IEnumerable<DecorShape> StickWell(double cx, double cy) =>
        Neon(Circle(cx, cy, 41), 2).Concat(Neon(Circle(cx, cy, 33), 1.5, dim: true));

    private static IEnumerable<DecorShape> FaceRings(params (double X, double Y)[] centres) =>
        centres.SelectMany(c => Neon(Circle(c.X, c.Y, 21), 1.8));

    // Layout every controller shares for the shoulder buttons
    private static IEnumerable<(ControllerButton Button, double Cx, double Cy, double W, double H)> ShoulderLayout() => new[]
    {
        (ControllerButton.LeftTrigger, 214.0, 62.0, 72.0, 32.0), (ControllerButton.RightTrigger, 486.0, 62.0, 72.0, 32.0),
        (ControllerButton.LeftBumper, 210.0, 98.0, 80.0, 26.0), (ControllerButton.RightBumper, 490.0, 98.0, 80.0, 26.0)
    };

    private static Dictionary<ControllerButton, Placement> WithShoulders(params (ControllerButton Button, double Cx, double Cy, double W, double H)[] rest) =>
        Layout(ShoulderLayout().Concat(rest).ToArray());

    // ---- Xbox -----------------------------------------------------------------------------------------------------

    private const string XboxBody =
        "M 205 108 C 250 92 450 92 495 108 C 545 125 580 165 595 225 C 610 290 618 345 596 388 C 582 416 545 412 528 384 " +
        "C 505 346 482 322 440 318 C 410 315 290 315 260 318 C 218 322 195 346 172 384 C 155 412 118 416 104 388 " +
        "C 82 345 90 290 105 225 C 120 165 155 125 205 108 Z";

    private static ControllerSkin BuildXbox()
    {
        var decor = new List<DecorShape>();
        decor.AddRange(Neon(XboxBody, 2.4, fill: BodyFill));
        decor.AddRange(Shoulders());
        decor.AddRange(NeonPair(GripHighlight, 1.5, dim: true));
        decor.AddRange(StickWell(209, 164));
        decor.AddRange(StickWell(394, 249));
        decor.AddRange(Neon(Cross(262, 257, 38, 14), 2));
        decor.AddRange(Neon(Circle(463, 173, 62), 1.5, dim: true));
        decor.AddRange(FaceRings((463, 135), (425, 173), (501, 173), (463, 211)));
        decor.AddRange(Neon(Circle(350, 140, 29), 1.8));

        return new ControllerSkin
        {
            Kind = SkinKind.Xbox,
            Name = "Xbox",
            Layout = WithShoulders(
                (ControllerButton.LeftStickClick, 209, 164, 50, 50), (ControllerButton.RightStickClick, 394, 249, 50, 50),
                (ControllerButton.DpadUp, 262, 222, 34, 34), (ControllerButton.DpadDown, 262, 292, 34, 34),
                (ControllerButton.DpadLeft, 227, 257, 34, 34), (ControllerButton.DpadRight, 297, 257, 34, 34),
                (ControllerButton.View, 310, 152, 32, 26), (ControllerButton.Menu, 390, 152, 32, 26),
                (ControllerButton.Guide, 350, 140, 42, 42),
                (ControllerButton.Y, 463, 135, 36, 36), (ControllerButton.X, 425, 173, 36, 36),
                (ControllerButton.B, 501, 173, 36, 36), (ControllerButton.A, 463, 211, 36, 36)),
            Decor = decor
        };
    }

    // ---- PlayStation 4 / 5 ----------------------------------------------------------------------------------------

    private const string PlayStation5Body =
        "M 205 98 C 235 88 290 86 350 86 C 410 86 465 88 495 98 C 540 112 575 150 590 215 C 604 280 612 340 592 385 " +
        "C 578 415 540 412 522 385 C 500 350 478 335 450 332 C 410 330 290 330 250 332 C 222 335 200 350 178 385 " +
        "C 160 412 122 415 108 385 C 88 340 96 280 110 215 C 125 150 160 112 205 98 Z";

    private const string PlayStation4Body =
        "M 215 96 C 265 82 435 82 485 96 C 538 108 578 140 596 205 C 612 270 624 335 604 382 C 590 414 548 414 530 384 " +
        "C 510 352 488 338 452 336 C 412 334 288 334 248 336 C 212 338 190 352 170 384 C 152 414 110 414 96 382 " +
        "C 76 335 88 270 104 205 C 122 140 162 108 215 96 Z";

    private static ControllerSkin BuildPlayStation(SkinKind kind)
    {
        bool ps5 = kind == SkinKind.PlayStation5;

        var decor = new List<DecorShape>();
        decor.AddRange(Neon(ps5 ? PlayStation5Body : PlayStation4Body, 2.4, fill: BodyFill));
        decor.AddRange(Shoulders());
        decor.AddRange(NeonPair(GripHighlight, 1.5, dim: true));

        // Touchpad; the DualSense has light strips down either side of it, the DualShock a light bar above
        decor.AddRange(Neon(ps5
            ? "M 276 112 L 424 112 C 436 112 442 120 440 132 L 432 180 C 430 190 424 194 414 194 L 286 194 C 276 194 270 190 268 180 L 260 132 C 258 120 264 112 276 112 Z"
            : "M 268 108 L 432 108 C 444 108 450 116 448 128 L 440 180 C 438 190 432 196 420 196 L 280 196 C 268 196 262 190 260 180 L 252 128 C 250 116 256 108 268 108 Z", 2));
        if (ps5) decor.AddRange(NeonPair("M 252 120 C 246 146 248 172 258 196", 1.6, dim: true));
        else decor.AddRange(Neon("M 322 90 L 378 90", 3, dim: true));

        decor.AddRange(StickWell(285, 278));
        decor.AddRange(StickWell(415, 278));
        decor.AddRange(Neon(Cross(180, 190, 38, 14), 2));
        decor.AddRange(FaceRings((520, 160), (520, 220), (490, 190), (550, 190)));
        decor.AddRange(Neon(Circle(350, 220, 23), 1.6, dim: true));
        decor.AddRange(Neon(RoundRect(332, 243, 36, 9, 4.5), 1.6, dim: true));

        return new ControllerSkin
        {
            Kind = kind,
            Name = ps5 ? "PlayStation 5" : "PlayStation 4",
            Layout = WithShoulders(
                (ControllerButton.View, 230, 130, 58, 22), (ControllerButton.Menu, 470, 130, 64, 22),
                (ControllerButton.Guide, 350, 220, 36, 36),
                (ControllerButton.DpadUp, 180, 160, 30, 30), (ControllerButton.DpadDown, 180, 220, 30, 30),
                (ControllerButton.DpadLeft, 150, 190, 30, 30), (ControllerButton.DpadRight, 210, 190, 30, 30),
                (ControllerButton.Y, 520, 160, 30, 30), (ControllerButton.A, 520, 220, 30, 30),
                (ControllerButton.X, 490, 190, 30, 30), (ControllerButton.B, 550, 190, 30, 30),
                (ControllerButton.LeftStickClick, 285, 278, 52, 52), (ControllerButton.RightStickClick, 415, 278, 52, 52)),
            Decor = decor
        };
    }

    // ---- Switch Pro -----------------------------------------------------------------------------------------------

    private const string SwitchBody =
        "M 215 104 C 255 94 445 94 485 104 C 535 116 575 156 590 222 C 604 290 616 350 596 392 C 582 418 546 414 530 388 " +
        "C 508 350 486 326 446 322 C 410 319 290 319 254 322 C 214 326 192 350 170 388 C 154 414 118 418 104 392 " +
        "C 84 350 96 290 110 222 C 125 156 165 116 215 104 Z";

    private static ControllerSkin BuildSwitchPro()
    {
        var decor = new List<DecorShape>();
        decor.AddRange(Neon(SwitchBody, 2.4, fill: BodyFill));
        decor.AddRange(Shoulders());
        decor.AddRange(NeonPair(GripHighlight, 1.5, dim: true));
        decor.AddRange(StickWell(205, 165));
        decor.AddRange(StickWell(405, 262));
        decor.AddRange(Neon(Cross(262, 257, 38, 14), 2));
        decor.AddRange(Neon(Circle(463, 173, 62), 1.5, dim: true));
        decor.AddRange(FaceRings((463, 135), (463, 211), (425, 173), (501, 173)));
        decor.AddRange(Neon(RoundRect(316, 188, 24, 14, 3), 1.5, dim: true)); // the capture button

        return new ControllerSkin
        {
            Kind = SkinKind.SwitchPro,
            Name = "Switch Pro",
            Layout = WithShoulders(
                (ControllerButton.LeftStickClick, 205, 165, 50, 50), (ControllerButton.RightStickClick, 405, 262, 50, 50),
                (ControllerButton.DpadUp, 262, 222, 34, 34), (ControllerButton.DpadDown, 262, 292, 34, 34),
                (ControllerButton.DpadLeft, 227, 257, 34, 34), (ControllerButton.DpadRight, 297, 257, 34, 34),
                (ControllerButton.View, 304, 140, 28, 28), (ControllerButton.Menu, 396, 140, 28, 28),
                (ControllerButton.Guide, 372, 195, 30, 30),
                // Nintendo's face buttons sit in a different order from Xbox: X on top, B below, Y left, A right
                (ControllerButton.X, 463, 135, 36, 36), (ControllerButton.B, 463, 211, 36, 36),
                (ControllerButton.Y, 425, 173, 36, 36), (ControllerButton.A, 501, 173, 36, 36)),
            Decor = decor
        };
    }
}
