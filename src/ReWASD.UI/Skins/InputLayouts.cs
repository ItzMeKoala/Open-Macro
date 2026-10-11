using ReWASD.Core.Models;
using ReWASD.Engine.Input;

namespace ReWASD.UI.Skins;

/// <summary>One key (or mouse button) on a drawing: its profile name, what is printed on it, and where it sits.</summary>
public sealed record SourcePlacement(string Name, string Legend, Placement Place, IconShape Shape, double Radius = 6, double FontSize = 12);

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

    public static IReadOnlyList<DecorShape> KeyboardDecor { get; } = new[]
    {
        new DecorShape(ControllerSkins.RoundRect(OriginX - 12, OriginY - 40, 18.5 * Unit + 24, 7.2 * Unit + 36, 16), "#12151B", "#2E3646", 2)
    };

    // ---- Mouse ----------------------------------------------------------------------------------------------------

    public static IReadOnlyList<SourcePlacement> Mouse { get; } = new[]
    {
        new SourcePlacement("Left Click", "Left", new Placement(303, 140, 86, 112), IconShape.KeyCap, 30, 13),
        new SourcePlacement("Right Click", "Right", new Placement(397, 140, 86, 112), IconShape.KeyCap, 30, 13),
        new SourcePlacement("Wheel Up", "▲", new Placement(350, 108, 26, 24), IconShape.KeyCap, 8, 10),
        new SourcePlacement("Middle Click", "●", new Placement(350, 140, 26, 34), IconShape.KeyCap, 8, 12),
        new SourcePlacement("Wheel Down", "▼", new Placement(350, 172, 26, 24), IconShape.KeyCap, 8, 10),
        new SourcePlacement("Mouse Button 4", "Back", new Placement(279, 242, 44, 34), IconShape.KeyCap, 8, 11),
        new SourcePlacement("Mouse Button 5", "Fwd", new Placement(279, 282, 44, 34), IconShape.KeyCap, 8, 11)
    };

    public static IReadOnlyList<DecorShape> MouseDecor { get; } = new[]
    {
        new DecorShape("M 350 62 C 272 62 250 118 250 190 L 250 292 C 250 362 292 400 350 400 C 408 400 450 362 450 292 L 450 190 C 450 118 428 62 350 62 Z", "#171A21", "#3A4254", 2),
        new DecorShape("M 350 70 L 350 205", "#00000000", "#2A3140", 1.5),
        new DecorShape("M 256 206 C 300 214 400 214 444 206", "#00000000", "#2A3140", 1.5)
    };
}
