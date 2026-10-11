using System.Globalization;
using ReWASD.Core.Models;

namespace ReWASD.UI.Skins;

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

    private const string BodyFill = "#171A21";
    private const string BodyStroke = "#3A4254";
    private const string WellFill = "#10131A";
    private const string WellStroke = "#2A3140";
    private const string Accent = "#1F232D";

    private static Dictionary<ControllerButton, Placement> Layout(params (ControllerButton Button, double Cx, double Cy, double W, double H)[] items) =>
        items.ToDictionary(i => i.Button, i => new Placement(i.Cx, i.Cy, i.W, i.H));

    // ---- Xbox -----------------------------------------------------------------------------------------------------

    private static ControllerSkin BuildXbox() => new()
    {
        Kind = SkinKind.Xbox,
        Name = "Xbox",
        Layout = Layout(
            (ControllerButton.LeftTrigger, 212, 43, 75, 36), (ControllerButton.RightTrigger, 477, 43, 75, 36),
            (ControllerButton.LeftBumper, 212, 91, 66, 30), (ControllerButton.RightBumper, 477, 91, 66, 30),
            (ControllerButton.LeftStickClick, 209, 164, 50, 50), (ControllerButton.RightStickClick, 394, 249, 50, 50),
            (ControllerButton.DpadUp, 262, 222, 34, 34), (ControllerButton.DpadDown, 262, 292, 34, 34),
            (ControllerButton.DpadLeft, 227, 257, 34, 34), (ControllerButton.DpadRight, 297, 257, 34, 34),
            (ControllerButton.View, 292, 159, 32, 26), (ControllerButton.Menu, 402, 159, 32, 26),
            (ControllerButton.Guide, 346, 143, 42, 42),
            (ControllerButton.Y, 463, 143, 38, 38), (ControllerButton.X, 428, 173, 38, 38),
            (ControllerButton.B, 498, 173, 38, 38), (ControllerButton.A, 463, 203, 38, 38)),
        Decor = new[]
        {
            new DecorShape("M 230 70 C 270 65, 430 65, 470 70 C 510 70, 560 90, 580 140 C 600 190, 615 280, 585 365 C 565 420, 500 420, 470 340 C 445 270, 395 240, 350 240 C 305 240, 255 270, 230 340 C 200 420, 135 420, 115 365 C 85 280, 100 190, 120 140 C 140 90, 190 70, 230 70 Z", BodyFill, BodyStroke, 2),
            new DecorShape("M 125 210 C 115 260, 130 330, 155 360 C 140 330, 130 270, 135 220 Z", Accent, "#282F3D", 1),
            new DecorShape("M 575 210 C 585 260, 570 330, 545 360 C 560 330, 570 270, 565 220 Z", Accent, "#282F3D", 1),
            new DecorShape(Circle(209, 164, 36), WellFill, WellStroke),
            new DecorShape(Circle(394, 249, 36), WellFill, WellStroke),
            new DecorShape(RoundRect(205, 200, 114, 114, 12), WellFill, WellStroke, 1),
            new DecorShape(Circle(463, 173, 56), WellFill, WellStroke, 1),
            new DecorShape(Circle(346, 143, 30), WellFill, "#3A5F3A", 1.5)
        }
    };

    // ---- PlayStation 4 / 5 ----------------------------------------------------------------------------------------

    private static ControllerSkin BuildPlayStation(SkinKind kind)
    {
        bool ps5 = kind == SkinKind.PlayStation5;

        string body = ps5
            ? "M 205 88 C 255 70 445 70 495 88 C 545 98 590 125 612 185 C 632 245 645 320 632 372 C 622 412 565 422 543 388 C 525 360 503 347 470 345 C 430 343 270 343 230 345 C 197 347 175 360 157 388 C 135 422 78 412 68 372 C 55 320 68 245 88 185 C 110 125 155 98 205 88 Z"
            : "M 215 90 C 265 75 435 75 485 90 C 540 100 585 120 605 175 C 625 235 640 310 628 365 C 618 410 560 420 538 385 C 520 358 500 345 470 342 C 430 340 270 340 230 342 C 200 345 180 358 162 385 C 140 420 82 410 72 365 C 60 310 75 235 95 175 C 115 120 160 100 215 90 Z";

        var decor = new List<DecorShape>
        {
            new(body, ps5 ? "#1C2029" : BodyFill, BodyStroke, 2),
            new(RoundRect(ps5 ? 270 : 285, 98, ps5 ? 160 : 130, 78, 14), "#12151C", "#323A4A", 1.5),
            new(Circle(185, 185, 44), WellFill, WellStroke, 1),
            new(Circle(515, 185, 50), WellFill, WellStroke, 1),
            new(Circle(285, 285, 37), WellFill, WellStroke),
            new(Circle(415, 285, 37), WellFill, WellStroke)
        };

        if (ps5)
        {
            // DualSense: light strips either side of the touchpad
            decor.Add(new(RoundRect(262, 100, 4, 74, 2), "#4D8DFF", "#4D8DFF", 0.5));
            decor.Add(new(RoundRect(434, 100, 4, 74, 2), "#4D8DFF", "#4D8DFF", 0.5));
        }
        else
        {
            // DualShock 4: the light bar across the top
            decor.Add(new(RoundRect(318, 76, 64, 5, 2.5), "#3E7BFF", "#3E7BFF", 0.5));
        }

        return new ControllerSkin
        {
            Kind = kind,
            Name = ps5 ? "PlayStation 5" : "PlayStation 4",
            Layout = Layout(
                (ControllerButton.LeftTrigger, 200, 45, 76, 34), (ControllerButton.RightTrigger, 500, 45, 76, 34),
                (ControllerButton.LeftBumper, 205, 84, 80, 28), (ControllerButton.RightBumper, 495, 84, 80, 28),
                (ControllerButton.View, 238, 128, 58, 22), (ControllerButton.Menu, 462, 128, 64, 22),
                (ControllerButton.Guide, 350, 218, 38, 38),
                (ControllerButton.DpadUp, 185, 157, 30, 30), (ControllerButton.DpadDown, 185, 213, 30, 30),
                (ControllerButton.DpadLeft, 157, 185, 30, 30), (ControllerButton.DpadRight, 213, 185, 30, 30),
                (ControllerButton.Y, 515, 157, 32, 32), (ControllerButton.A, 515, 213, 32, 32),
                (ControllerButton.X, 487, 185, 32, 32), (ControllerButton.B, 543, 185, 32, 32),
                (ControllerButton.LeftStickClick, 285, 285, 52, 52), (ControllerButton.RightStickClick, 415, 285, 52, 52)),
            Decor = decor
        };
    }

    // ---- Switch Pro -----------------------------------------------------------------------------------------------

    private static ControllerSkin BuildSwitchPro() => new()
    {
        Kind = SkinKind.SwitchPro,
        Name = "Switch Pro",
        Layout = Layout(
            (ControllerButton.LeftTrigger, 212, 43, 75, 36), (ControllerButton.RightTrigger, 477, 43, 75, 36),
            (ControllerButton.LeftBumper, 212, 91, 66, 30), (ControllerButton.RightBumper, 477, 91, 66, 30),
            (ControllerButton.LeftStickClick, 205, 160, 50, 50), (ControllerButton.RightStickClick, 405, 258, 50, 50),
            (ControllerButton.DpadUp, 262, 222, 34, 34), (ControllerButton.DpadDown, 262, 292, 34, 34),
            (ControllerButton.DpadLeft, 227, 257, 34, 34), (ControllerButton.DpadRight, 297, 257, 34, 34),
            (ControllerButton.View, 300, 135, 28, 28), (ControllerButton.Menu, 400, 135, 28, 28),
            (ControllerButton.Guide, 372, 178, 30, 30),
            // Nintendo's face buttons sit in a different order from Xbox: X on top, B below, Y left, A right
            (ControllerButton.X, 463, 143, 38, 38), (ControllerButton.B, 463, 203, 38, 38),
            (ControllerButton.Y, 428, 173, 38, 38), (ControllerButton.A, 498, 173, 38, 38)),
        Decor = new[]
        {
            new DecorShape("M 225 78 C 270 70 430 70 475 78 C 520 80 565 100 585 150 C 605 205 625 285 595 365 C 575 415 505 415 478 345 C 455 280 400 252 350 252 C 300 252 245 280 222 345 C 195 415 125 415 105 365 C 75 285 95 205 115 150 C 135 100 180 80 225 78 Z", "#1B1D24", BodyStroke, 2),
            new DecorShape(Circle(205, 160, 36), WellFill, WellStroke),
            new DecorShape(Circle(405, 258, 36), WellFill, WellStroke),
            new DecorShape(RoundRect(205, 200, 114, 114, 12), WellFill, WellStroke, 1),
            new DecorShape(Circle(463, 173, 56), WellFill, WellStroke, 1),
            new DecorShape(RoundRect(318, 190, 22, 14, 3), WellFill, WellStroke, 1)
        }
    };
}
