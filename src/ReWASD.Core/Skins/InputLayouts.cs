using ReWASD.Core.Models;

namespace ReWASD.Core.Skins;

/// <summary>One key (or mouse button) on a drawing: its profile name, what is printed on it, and where it sits.</summary>
/// <summary>
/// One key (or mouse button) on a drawing: its profile name, what is printed on it, and where it sits.
/// A source with <see cref="ShapeData"/> is drawn as that shape instead of a box, optionally cut down to
/// <see cref="ClipData"/>, so a button can be a real part of a larger outline (a mouse's left button, for example).
/// For those sources <see cref="Place"/> only says where the legend is printed.
/// </summary>
public sealed record SourcePlacement(string Name, string Legend, Placement Place, IconShape Shape, double Radius = 6, double FontSize = 12,
    string? ShapeData = null, string? ClipData = null);

/// <summary>The keyboard and mouse drawings that sources are picked from.</summary>
public static class InputLayouts
{
    private const double Unit = 36;
    private const double KeyGap = 3;
    private const double OriginX = 18;
    private const double OriginY = 96;

    /// <summary>A key placed on a grid of key-widths: (x, y) of the top-left corner and width, all in units.</summary>
    private static SourcePlacement Key(string name, string legend, double ux, double uy, double uw = 1, double fontSize = 11)
    {
        double w = uw * Unit - KeyGap;
        double h = Unit - KeyGap;
        double cx = OriginX + ux * Unit + w / 2;
        double cy = OriginY + uy * Unit + h / 2;
        return new SourcePlacement(name, legend, new Placement(cx, cy, w, h), IconShape.KeyCap, 5, fontSize);
    }

    public static IReadOnlyList<SourcePlacement> Keyboard { get; } = BuildKeyboard();

    private static IReadOnlyList<SourcePlacement> BuildKeyboard()
    {
        var keys = new List<SourcePlacement>
        {
            Key("Escape", "Esc", 0, 0)
        };

        // Function row in groups of four
        double[] fx = { 2, 3, 4, 5, 6.5, 7.5, 8.5, 9.5, 11, 12, 13, 14 };
        for (int i = 0; i < 12; i++) keys.Add(Key("F" + (i + 1), "F" + (i + 1), fx[i], 0));

        // Number row (digits only) and Backspace
        for (int i = 0; i < 10; i++)
        {
            string d = ((i + 1) % 10).ToString();
            keys.Add(Key(d, d, i, 1.4));
        }
        keys.Add(Key("Back", "Bksp", 10, 1.4, 2));

        // QWERTY rows
        keys.Add(Key("Tab", "Tab", 0, 2.4, 1.5));
        AddRow(keys, "QWERTYUIOP", 1.5, 2.4);

        AddRow(keys, "ASDFGHJKL", 1.75, 3.4);
        keys.Add(Key("Enter", "Enter", 10.75, 3.4, 2.25));

        keys.Add(Key("ShiftKey", "Shift", 0, 4.4, 2.25));
        AddRow(keys, "ZXCVBNM", 2.25, 4.4);

        keys.Add(Key("ControlKey", "Ctrl", 0, 5.4, 1.5));
        keys.Add(Key("Alt", "Alt", 1.75, 5.4, 1.5));
        keys.Add(Key("Space", "Space", 3.5, 5.4, 6.25));

        // Navigation block and arrows
        keys.Add(Key("Insert", "Ins", 15.5, 1.4)); keys.Add(Key("Home", "Home", 16.5, 1.4)); keys.Add(Key("PageUp", "PgUp", 17.5, 1.4));
        keys.Add(Key("Delete", "Del", 15.5, 2.4)); keys.Add(Key("End", "End", 16.5, 2.4)); keys.Add(Key("PageDown", "PgDn", 17.5, 2.4));
        keys.Add(Key("Up", "▲", 16.5, 4.4)); keys.Add(Key("Left", "◀", 15.5, 5.4));
        keys.Add(Key("Down", "▼", 16.5, 5.4)); keys.Add(Key("Right", "▶", 17.5, 5.4));

        return keys;
    }

    private static void AddRow(List<SourcePlacement> keys, string letters, double startX, double y)
    {
        for (int i = 0; i < letters.Length; i++) keys.Add(Key(letters[i].ToString(), letters[i].ToString(), startX + i, y));
    }

    public static IReadOnlyList<DecorShape> KeyboardDecor { get; } =
        ControllerSkins.Neon(ControllerSkins.RoundRect(OriginX - 12, OriginY - 40, 18.5 * Unit + 24, 7.2 * Unit + 36, 16), 2.2, fill: "#0E0F13").ToArray();

    // ---- Mouse ----------------------------------------------------------------------------------------------------
    // The body is one outline. Each button is that same outline cut down to its own region, so the buttons are the mouse
    // itself rather than boxes placed over a picture of one.

    private const string MouseBody =
        "M 350 52 C 300 52 268 80 262 130 C 254 190 248 240 250 290 C 252 350 290 402 350 402 " +
        "C 410 402 448 350 450 290 C 452 240 446 190 438 130 C 432 80 400 52 350 52 Z";

    // Left and right buttons: everything above the palm line, either side of the wheel slot and the centre seam
    private const string LeftButtonClip = "M 0,0 L 330,0 L 330,176 L 346,176 L 346,206 C 310,214 270,216 0,216 Z";
    private const string RightButtonClip = "M 700,0 L 370,0 L 370,176 L 354,176 L 354,206 C 390,214 430,216 700,216 Z";

    // The thumb buttons on the left flank
    private static readonly string ForwardClip = ControllerSkins.RoundRect(228, 224, 70, 34, 11);
    private static readonly string BackClip = ControllerSkins.RoundRect(228, 266, 70, 34, 11);

    // The scroll wheel, in three parts: scroll up, press, scroll down
    private const string WheelUp = "M 337,99 L 337,86 C 337,79 343,75 350,75 C 357,75 363,79 363,86 L 363,99 Z";
    private const string WheelMiddle = "M 337,102 L 363,102 L 363,138 L 337,138 Z";
    private const string WheelDown = "M 337,141 L 337,154 C 337,161 343,165 350,165 C 357,165 363,161 363,154 L 363,141 Z";

    public static IReadOnlyList<SourcePlacement> Mouse { get; } = new[]
    {
        new SourcePlacement("Left Click", "Left", new Placement(290, 125, 56, 20), IconShape.KeyCap, 0, 14, MouseBody, LeftButtonClip),
        new SourcePlacement("Right Click", "Right", new Placement(410, 125, 56, 20), IconShape.KeyCap, 0, 14, MouseBody, RightButtonClip),
        new SourcePlacement("Wheel Up", "▲", new Placement(350, 87, 24, 18), IconShape.KeyCap, 0, 10, WheelUp),
        new SourcePlacement("Middle Click", "●", new Placement(350, 120, 24, 18), IconShape.KeyCap, 0, 12, WheelMiddle),
        new SourcePlacement("Wheel Down", "▼", new Placement(350, 153, 24, 18), IconShape.KeyCap, 0, 10, WheelDown),
        new SourcePlacement("Mouse Button 5", "Fwd", new Placement(268, 241, 40, 16), IconShape.KeyCap, 0, 10, MouseBody, ForwardClip),
        new SourcePlacement("Mouse Button 4", "Back", new Placement(268, 283, 40, 16), IconShape.KeyCap, 0, 10, MouseBody, BackClip)
    };

    public static IReadOnlyList<DecorShape> MouseDecor { get; } = BuildMouseDecor();

    private static IReadOnlyList<DecorShape> BuildMouseDecor()
    {
        var decor = new List<DecorShape>();
        decor.AddRange(ControllerSkins.Neon(MouseBody, 2.4, fill: "#0E0F13"));

        // The recess the wheel sits in, open at the front
        decor.AddRange(ControllerSkins.Neon("M 330 52 L 330 160 C 330 172 337 178 350 178 C 363 178 370 172 370 160 L 370 52", 1.6, dim: true));

        // The seam between the buttons and the line where the buttons end and the palm begins
        decor.AddRange(ControllerSkins.Neon("M 350 178 L 350 206", 1.6, dim: true));
        const string palmLine = "M 252 216 C 270 216 310 214 346 206";
        decor.AddRange(ControllerSkins.Neon(palmLine, 1.6, dim: true));
        decor.AddRange(ControllerSkins.Neon(ControllerSkins.Mirror(palmLine), 1.6, dim: true));
        return decor;
    }
}