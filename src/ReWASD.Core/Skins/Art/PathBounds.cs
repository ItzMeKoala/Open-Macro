using System.Globalization;

namespace ReWASD.Core.Skins.Art;

/// <summary>Bounding boxes of SVG path data. Understands M, L, H, V, C, S, Q, Z in absolute and relative form.</summary>
public static class PathBounds
{
    public static ArtRect Of(string d)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        void Add(double x, double y)
        {
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }

        var t = Tokenize(d);
        int i = 0;
        double cx = 0, cy = 0, sx = 0, sy = 0, lastCtrlX = 0, lastCtrlY = 0;
        char cmd = 'M', prev = 'M';

        double Num() => double.Parse(t[i++], CultureInfo.InvariantCulture);

        while (i < t.Count)
        {
            if (char.IsLetter(t[i][0])) cmd = t[i++][0];
            bool rel = char.IsLower(cmd);
            char up = char.ToUpperInvariant(cmd);
            double ox = rel ? cx : 0, oy = rel ? cy : 0;

            switch (up)
            {
                case 'M':
                case 'L':
                {
                    double x = ox + Num(), y = oy + Num();
                    cx = x; cy = y; Add(x, y);
                    if (up == 'M') { sx = x; sy = y; cmd = rel ? 'l' : 'L'; }
                    break;
                }
                case 'H': cx = ox + Num(); Add(cx, cy); break;
                case 'V': cy = oy + Num(); Add(cx, cy); break;
                case 'C':
                {
                    double x1 = ox + Num(), y1 = oy + Num(), x2 = ox + Num(), y2 = oy + Num(), x = ox + Num(), y = oy + Num();
                    Cubic(cx, cy, x1, y1, x2, y2, x, y, Add);
                    lastCtrlX = x2; lastCtrlY = y2; cx = x; cy = y;
                    break;
                }
                case 'S':
                {
                    double x1 = prev is 'C' or 'S' ? 2 * cx - lastCtrlX : cx, y1 = prev is 'C' or 'S' ? 2 * cy - lastCtrlY : cy;
                    double x2 = ox + Num(), y2 = oy + Num(), x = ox + Num(), y = oy + Num();
                    Cubic(cx, cy, x1, y1, x2, y2, x, y, Add);
                    lastCtrlX = x2; lastCtrlY = y2; cx = x; cy = y;
                    break;
                }
                case 'Q':
                {
                    double x1 = ox + Num(), y1 = oy + Num(), x = ox + Num(), y = oy + Num();
                    Cubic(cx, cy, cx + 2.0 / 3 * (x1 - cx), cy + 2.0 / 3 * (y1 - cy), x + 2.0 / 3 * (x1 - x), y + 2.0 / 3 * (y1 - y), x, y, Add);
                    cx = x; cy = y;
                    break;
                }
                case 'Z':
                    cx = sx; cy = sy;
                    break;
                default:
                    throw new FormatException($"Unsupported path command '{cmd}'");
            }
            prev = up;
        }

        if (minX == double.MaxValue) return new ArtRect(0, 0, 0, 0);
        return new ArtRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void Cubic(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3, Action<double, double> add)
    {
        // Sampling is exact enough for layout (a box around a hit shape)
        const int Steps = 24;
        for (int k = 0; k <= Steps; k++)
        {
            double t = k / (double)Steps, m = 1 - t;
            add(m * m * m * x0 + 3 * m * m * t * x1 + 3 * m * t * t * x2 + t * t * t * x3,
                m * m * m * y0 + 3 * m * m * t * y1 + 3 * m * t * t * y2 + t * t * t * y3);
        }
    }

    private static List<string> Tokenize(string d)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < d.Length)
        {
            char c = d[i];
            if (char.IsLetter(c)) { tokens.Add(c.ToString()); i++; }
            else if (char.IsDigit(c) || c is '-' or '+' or '.')
            {
                int start = i++;
                bool dot = d[start] == '.';
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot) || ((d[i] is 'e' or 'E') && i + 1 < d.Length)))
                {
                    if (d[i] == '.') dot = true;
                    if (d[i] is 'e' or 'E') { i++; if (i < d.Length && d[i] is '-' or '+') i++; continue; }
                    i++;
                }
                tokens.Add(d.Substring(start, i - start));
            }
            else i++; // spaces and commas
        }
        return tokens;
    }
}
