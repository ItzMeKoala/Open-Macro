using System.Globalization;
using System.Security;
using System.Text;

namespace ReWASD.Core.Skins.Art;

/// <summary>
/// Draws a tenkeyless keyboard as an SVG in the same style as the controller drawings: an aluminium case, a recessed plate, and
/// sculpted two-tone keycaps with their shadows. Every key that can be remapped is a part (<c>data-key</c>) that sinks when pressed;
/// a key this computer cannot use (and Fn, which no app ever sees) is drawn the same way with a quieter legend and does nothing when
/// clicked.
/// </summary>
public static class KeyboardArt
{
    /// <summary>Size of one key unit, in drawing units.</summary>
    public const double Unit = 58;

    /// <summary>Space between neighbouring keycaps.</summary>
    public const double Gap = 5;

    private const double KeysX = 34, KeysY = 34;
    private const double CasePad = 30;
    private const string Font = "Inter, SF Pro Text, Segoe UI, Helvetica Neue, Arial, sans-serif";

    private const string LegendColor = "#ECEEF2";
    private const string SubLegendColor = "#A7ADB8";
    private const string QuietColor = "#7D838E";

    /// <summary>How a key is drawn: its legend style and its cap colour.</summary>
    public enum KeyStyle
    {
        /// <summary>One character in the middle (letters).</summary>
        Letter,

        /// <summary>A symbol above a character (digits and punctuation).</summary>
        Pair,

        /// <summary>A word, in the middle (function keys and the navigation block).</summary>
        Word,

        /// <summary>A word in the lower left corner (wide modifiers).</summary>
        Corner,

        /// <summary>An arrow drawn as a triangle.</summary>
        Arrow
    }

    /// <summary>One key of the drawing, in key units. <see cref="Name"/> is the profile name when the key can be remapped.</summary>
    public sealed record KeyDef(string? Name, string Legend, double X, double Y, double Width, KeyStyle Style, bool Modifier, string? Upper = null, string? Title = null);

    /// <summary>Every key on the board, in reading order.</summary>
    public static IReadOnlyList<KeyDef> Keys { get; } = BuildKeys();

    private static IReadOnlyList<KeyDef> BuildKeys()
    {
        var k = new List<KeyDef>();
        void Add(string? name, string legend, double x, double y, double w, KeyStyle style, bool mod, string? upper = null, string? title = null) =>
            k.Add(new KeyDef(name, legend, x, y, w, style, mod, upper, title));

        // Function row
        const double fr = 0;
        Add("Escape", "Esc", 0, fr, 1, KeyStyle.Word, true, title: "Escape");
        double[] fx = { 2, 3, 4, 5, 6.5, 7.5, 8.5, 9.5, 11, 12, 13, 14 };
        for (int i = 0; i < 12; i++) Add("F" + (i + 1), "F" + (i + 1), fx[i], fr, 1, KeyStyle.Word, true);
        Add("PrintScreen", "PrtSc", 15.25, fr, 1, KeyStyle.Word, true, title: "Print Screen");
        Add("ScrollLock", "ScrLk", 16.25, fr, 1, KeyStyle.Word, true, title: "Scroll Lock");
        Add("Pause", "Pause", 17.25, fr, 1, KeyStyle.Word, true);

        // Number row
        const double r1 = 1.3;
        Add("Backtick", "`", 0, r1, 1, KeyStyle.Pair, false, "~", "Backtick `");
        string shifted = "!@#$%^&*()";
        for (int i = 0; i < 10; i++)
        {
            string d = ((i + 1) % 10).ToString(CultureInfo.InvariantCulture);
            Add(d, d, 1 + i, r1, 1, KeyStyle.Pair, false, shifted[i].ToString());
        }
        Add("Minus", "-", 11, r1, 1, KeyStyle.Pair, false, "_", "Minus -");
        Add("Equals", "=", 12, r1, 1, KeyStyle.Pair, false, "+", "Equals =");
        Add("Back", "Backspace", 13, r1, 2, KeyStyle.Corner, true, title: "Backspace");
        Add("Insert", "Ins", 15.25, r1, 1, KeyStyle.Word, true, title: "Insert");
        Add("Home", "Home", 16.25, r1, 1, KeyStyle.Word, true);
        Add("PageUp", "PgUp", 17.25, r1, 1, KeyStyle.Word, true, title: "Page Up");

        // Top letter row
        const double r2 = 2.3;
        Add("Tab", "Tab", 0, r2, 1.5, KeyStyle.Corner, true);
        Letters(k, "QWERTYUIOP", 1.5, r2);
        Add("LeftBracket", "[", 11.5, r2, 1, KeyStyle.Pair, false, "{", "Left Bracket [");
        Add("RightBracket", "]", 12.5, r2, 1, KeyStyle.Pair, false, "}", "Right Bracket ]");
        Add("Backslash", "\\", 13.5, r2, 1.5, KeyStyle.Pair, false, "|", "Backslash \\");
        Add("Delete", "Del", 15.25, r2, 1, KeyStyle.Word, true, title: "Delete");
        Add("End", "End", 16.25, r2, 1, KeyStyle.Word, true);
        Add("PageDown", "PgDn", 17.25, r2, 1, KeyStyle.Word, true, title: "Page Down");

        // Home row
        const double r3 = 3.3;
        Add("CapsLock", "Caps Lock", 0, r3, 1.75, KeyStyle.Corner, true);
        Letters(k, "ASDFGHJKL", 1.75, r3);
        Add("Semicolon", ";", 10.75, r3, 1, KeyStyle.Pair, false, ":", "Semicolon ;");
        Add("Quote", "'", 11.75, r3, 1, KeyStyle.Pair, false, "\"", "Quote '");
        Add("Enter", "Enter", 12.75, r3, 2.25, KeyStyle.Corner, true);

        // Bottom letter row
        const double r4 = 4.3;
        Add("ShiftKey", "Shift", 0, r4, 2.25, KeyStyle.Corner, true, title: "Left Shift");
        Letters(k, "ZXCVBNM", 2.25, r4);
        Add("Comma", ",", 9.25, r4, 1, KeyStyle.Pair, false, "<", "Comma ,");
        Add("Period", ".", 10.25, r4, 1, KeyStyle.Pair, false, ">", "Period .");
        Add("Slash", "/", 11.25, r4, 1, KeyStyle.Pair, false, "?", "Slash /");
        Add("RightShift", "Shift", 12.25, r4, 2.75, KeyStyle.Corner, true, title: "Right Shift");
        Add("Up", "Up", 16.25, r4, 1, KeyStyle.Arrow, true, title: "Up Arrow");

        // Space row
        const double r5 = 5.3;
        Add("ControlKey", "Ctrl", 0, r5, 1.25, KeyStyle.Corner, true, title: "Left Ctrl");
        Add("Win", "Win", 1.25, r5, 1.25, KeyStyle.Corner, true, title: "Windows key (Command on a Mac)");
        Add("Alt", "Alt", 2.5, r5, 1.25, KeyStyle.Corner, true, title: "Left Alt");
        Add("Space", "", 3.75, r5, 6.25, KeyStyle.Word, false, title: "Space");
        Add("RightAlt", "Alt", 10, r5, 1.25, KeyStyle.Corner, true, title: "Right Alt");
        Add(null, "Fn", 11.25, r5, 1.25, KeyStyle.Corner, true);      // handled inside the keyboard: no app ever sees it
        Add("Apps", "Menu", 12.5, r5, 1.25, KeyStyle.Corner, true, title: "Menu key");
        Add("RightCtrl", "Ctrl", 13.75, r5, 1.25, KeyStyle.Corner, true, title: "Right Ctrl");
        Add("Left", "Left", 15.25, r5, 1, KeyStyle.Arrow, true, title: "Left Arrow");
        Add("Down", "Down", 16.25, r5, 1, KeyStyle.Arrow, true, title: "Down Arrow");
        Add("Right", "Right", 17.25, r5, 1, KeyStyle.Arrow, true, title: "Right Arrow");

        return k;
    }

    private static void Letters(List<KeyDef> keys, string letters, double x, double y)
    {
        for (int i = 0; i < letters.Length; i++)
        {
            string l = letters[i].ToString();
            keys.Add(new KeyDef(l, l, x + i, y, 1, KeyStyle.Letter, false));
        }
    }

    /// <summary>The keycap rectangle of a key, in drawing units.</summary>
    public static ArtRect CapOf(KeyDef key) =>
        new(KeysX + key.X * Unit + Gap / 2, KeysY + key.Y * Unit + Gap / 2, key.Width * Unit - Gap, Unit - Gap);

    /// <summary>The drawing, as SVG, with every named key live.</summary>
    public static string Svg() => Svg(_ => true);

    /// <summary>
    /// The drawing, as SVG. Only the keys <paramref name="canRemap"/> accepts are live parts; the rest (a key this computer cannot
    /// read or press, such as Caps Lock on a Mac) are drawn the same way with quieter legends.
    /// </summary>
    public static string Svg(Func<string, bool> canRemap)
    {
        double keysW = 18.25 * Unit, keysH = 6.3 * Unit;
        double caseX = KeysX - CasePad, caseY = KeysY - CasePad, caseW = keysW + 2 * CasePad, caseH = keysH + 2 * CasePad + 4;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{N(caseX - 26)} {N(caseY - 20)} {N(caseW + 52)} {N(caseH + 64)}\" id=\"keyboard\">\n");
        sb.Append("<!-- Tenkeyless keyboard, drawn for ReWASD from its key list. -->\n");
        sb.Append("<defs>\n");
        sb.Append("<linearGradient id=\"g-case\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#4A4E57\"/><stop offset=\"0.08\" stop-color=\"#3A3D45\"/><stop offset=\"0.7\" stop-color=\"#2B2E34\"/><stop offset=\"1\" stop-color=\"#1F2126\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-case-front\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#202227\"/><stop offset=\"1\" stop-color=\"#121316\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-plate\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#0B0C0E\"/><stop offset=\"1\" stop-color=\"#16171B\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-skirt-a\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#30333A\"/><stop offset=\"0.55\" stop-color=\"#25272D\"/><stop offset=\"1\" stop-color=\"#17181C\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-top-a\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#474B55\"/><stop offset=\"1\" stop-color=\"#383B43\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-skirt-m\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#23252A\"/><stop offset=\"0.55\" stop-color=\"#1A1B1F\"/><stop offset=\"1\" stop-color=\"#101113\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-top-m\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#34373E\"/><stop offset=\"1\" stop-color=\"#282A30\"/></linearGradient>\n");
        sb.Append("<linearGradient id=\"g-dish\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#FFFFFF\" stop-opacity=\"0.10\"/><stop offset=\"0.45\" stop-color=\"#FFFFFF\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#000000\" stop-opacity=\"0.10\"/></linearGradient>\n");
        sb.Append("<filter id=\"f-case\"><feGaussianBlur stdDeviation=\"14\"/></filter>\n");
        sb.Append("<filter id=\"f-key\"><feGaussianBlur stdDeviation=\"2\"/></filter>\n");
        sb.Append("<filter id=\"f-inner\"><feGaussianBlur stdDeviation=\"3\"/></filter>\n");
        double plateX = KeysX - 8, plateY = KeysY - 8, plateW = keysW + 16, plateH = keysH + 16;
        sb.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"c-plate\"><rect x=\"{N(plateX)}\" y=\"{N(plateY)}\" width=\"{N(plateW)}\" height=\"{N(plateH)}\" rx=\"10\"/></clipPath>\n");
        sb.Append("</defs>\n");

        // The case: a soft shadow under it, the front lip, the top with a bevel, and the plate the keys sit in
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(caseX + 10)}\" y=\"{N(caseY + 22)}\" width=\"{N(caseW - 20)}\" height=\"{N(caseH)}\" rx=\"26\" fill=\"#000000\" opacity=\"0.6\" filter=\"url(#f-case)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(caseX)}\" y=\"{N(caseY + 8)}\" width=\"{N(caseW)}\" height=\"{N(caseH)}\" rx=\"22\" fill=\"url(#g-case-front)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(caseX)}\" y=\"{N(caseY)}\" width=\"{N(caseW)}\" height=\"{N(caseH)}\" rx=\"22\" fill=\"url(#g-case)\" stroke=\"#0D0E10\" stroke-width=\"1.5\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(caseX + 1.5)}\" y=\"{N(caseY + 1.5)}\" width=\"{N(caseW - 3)}\" height=\"{N(caseH - 3)}\" rx=\"20.5\" fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.10\" stroke-width=\"1.5\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(plateX)}\" y=\"{N(plateY)}\" width=\"{N(plateW)}\" height=\"{N(plateH)}\" rx=\"10\" fill=\"url(#g-plate)\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#c-plate)\"><rect x=\"{N(plateX)}\" y=\"{N(plateY)}\" width=\"{N(plateW)}\" height=\"{N(plateH)}\" rx=\"10\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.8\" stroke-width=\"8\" filter=\"url(#f-inner)\"/></g>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(plateX - 0.75)}\" y=\"{N(plateY - 0.75)}\" width=\"{N(plateW + 1.5)}\" height=\"{N(plateH + 1.5)}\" rx=\"10.75\" fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.06\" stroke-width=\"1\"/>\n");

        // Shadows under the caps stay on the plate when a key goes down
        sb.Append("<g id=\"key-shadows\">\n");
        foreach (var key in Keys)
        {
            var c = CapOf(key);
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(c.X + 1)}\" y=\"{N(c.Y + 3.5)}\" width=\"{N(c.Width - 2)}\" height=\"{N(c.Height)}\" rx=\"7\" fill=\"#000000\" opacity=\"0.75\" filter=\"url(#f-key)\"/>\n");
        }
        sb.Append("</g>\n");

        foreach (var key in Keys) AppendKey(sb, key, key.Name != null && canRemap(key.Name));

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static void AppendKey(StringBuilder sb, KeyDef key, bool live)
    {
        var c = CapOf(key);
        string tone = key.Modifier ? "m" : "a";
        string legend = live ? LegendColor : QuietColor;
        string sub = live ? SubLegendColor : QuietColor;

        if (live)
        {
            string id = "key-" + key.Name!.ToLowerInvariant();
            string title = Esc(key.Title ?? key.Legend);
            sb.Append(CultureInfo.InvariantCulture,
                $"<g id=\"{id}\" data-key=\"{Esc(key.Name)}\" data-name=\"{title}\" data-anim=\"press\" data-origin=\"{N(c.CenterX)} {N(c.Bottom)}\" data-press-scale=\"0.985\" data-press-dy=\"3\">\n");
        }
        else
        {
            sb.Append("<g>\n");
        }

        // The cap: its sides, then the sculpted top a little smaller and higher, as seen from just above
        double tx = c.X + 5, ty = c.Y + 3, tw = c.Width - 10, th = c.Height - 12;
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(c.X)}\" y=\"{N(c.Y)}\" width=\"{N(c.Width)}\" height=\"{N(c.Height)}\" rx=\"7\" fill=\"url(#g-skirt-{tone})\" stroke=\"#08090B\" stroke-width=\"1\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(tx)}\" y=\"{N(ty)}\" width=\"{N(tw)}\" height=\"{N(th)}\" rx=\"5.5\" fill=\"url(#g-top-{tone})\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(tx)}\" y=\"{N(ty)}\" width=\"{N(tw)}\" height=\"{N(th)}\" rx=\"5.5\" fill=\"url(#g-dish)\" stroke=\"#FFFFFF\" stroke-opacity=\"0.07\" stroke-width=\"1\"/>\n");

        // Lit while held
        if (live)
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(tx)}\" y=\"{N(ty)}\" width=\"{N(tw)}\" height=\"{N(th)}\" rx=\"5.5\" fill=\"#E9C46A\" data-fx=\"glow\" data-max=\"0.34\" opacity=\"0\"/>\n");

        double cx = tx + tw / 2, cy = ty + th / 2;
        switch (key.Style)
        {
            case KeyStyle.Letter:
                Text(sb, key.Legend, cx, cy, 18, 600, legend, "middle");
                break;
            case KeyStyle.Pair:
                Text(sb, key.Upper ?? "", cx, ty + th * 0.30, 12, 500, sub, "middle");
                Text(sb, key.Legend, cx, ty + th * 0.70, 15, 600, legend, "middle");
                break;
            case KeyStyle.Word:
                if (key.Legend.Length > 0) Text(sb, key.Legend, cx, cy, key.Legend.Length <= 3 ? 12.5 : 11, 600, legend, "middle");
                break;
            case KeyStyle.Corner:
                Text(sb, key.Legend, tx + 8, ty + th - 10, 11, 600, legend, "start");
                break;
            case KeyStyle.Arrow:
                sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{Arrow(key.Legend, cx, cy, 6.5)}\" fill=\"{legend}\"/>\n");
                break;
        }

        // What a click lands on
        if (live)
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(c.X)}\" y=\"{N(c.Y)}\" width=\"{N(c.Width)}\" height=\"{N(c.Height)}\" rx=\"7\" fill=\"transparent\" class=\"hit\"/>\n");

        sb.Append("</g>\n");
    }

    private static void Text(StringBuilder sb, string text, double x, double y, double size, int weight, string fill, string anchor)
    {
        if (text.Length == 0) return;
        sb.Append(CultureInfo.InvariantCulture,
            $"<text x=\"{N(x)}\" y=\"{N(y)}\" font-size=\"{N(size)}\" font-weight=\"{weight}\" fill=\"{fill}\" text-anchor=\"{anchor}\" dominant-baseline=\"central\" font-family=\"{Font}\">{Esc(text)}</text>\n");
    }

    /// <summary>A small solid triangle pointing the way of an arrow key.</summary>
    private static string Arrow(string direction, double cx, double cy, double r)
    {
        (double X, double Y)[] pts = direction switch
        {
            "Up" => new[] { (cx, cy - r), (cx + r, cy + r * 0.7), (cx - r, cy + r * 0.7) },
            "Down" => new[] { (cx, cy + r), (cx - r, cy - r * 0.7), (cx + r, cy - r * 0.7) },
            "Left" => new[] { (cx - r, cy), (cx + r * 0.7, cy - r), (cx + r * 0.7, cy + r) },
            _ => new[] { (cx + r, cy), (cx - r * 0.7, cy + r), (cx - r * 0.7, cy - r) }
        };
        return $"M {N(pts[0].X)} {N(pts[0].Y)} L {N(pts[1].X)} {N(pts[1].Y)} L {N(pts[2].X)} {N(pts[2].Y)} Z";
    }

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) => SecurityElement.Escape(s) ?? s;
}
