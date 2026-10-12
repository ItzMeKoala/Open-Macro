using ReWASD.Core.Models;

namespace ReWASD.Core.Skins.Art;

/// <summary>A 2D affine transform in SVG's (a b c d e f) order: x' = a*x + c*y + e, y' = b*x + d*y + f.</summary>
public readonly record struct ArtMatrix(double A, double B, double C, double D, double E, double F)
{
    public static readonly ArtMatrix Identity = new(1, 0, 0, 1, 0, 0);

    public bool IsIdentity => this == Identity;

    public static ArtMatrix Translate(double x, double y) => new(1, 0, 0, 1, x, y);
    public static ArtMatrix Scale(double x, double y) => new(x, 0, 0, y, 0, 0);

    public static ArtMatrix Rotate(double degrees)
    {
        double r = degrees * Math.PI / 180, cos = Math.Cos(r), sin = Math.Sin(r);
        return new ArtMatrix(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>The transform that applies <paramref name="first"/> and then this one is <c>this.Then(first)</c>... written in the order the SVG writes them: <c>outer * inner</c>.</summary>
    public static ArtMatrix operator *(ArtMatrix m, ArtMatrix n) => new(
        m.A * n.A + m.C * n.B, m.B * n.A + m.D * n.B,
        m.A * n.C + m.C * n.D, m.B * n.C + m.D * n.D,
        m.A * n.E + m.C * n.F + m.E, m.B * n.E + m.D * n.F + m.F);

    public (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);
}

public readonly record struct ArtRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public static ArtRect Union(ArtRect a, ArtRect b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new ArtRect(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }
}

/// <summary>One colour of a gradient. <see cref="Color"/> is "#RRGGBB" or "#AARRGGBB".</summary>
public readonly record struct ArtStop(double Offset, string Color, double Opacity);

/// <summary>What a shape is filled or stroked with.</summary>
public abstract record ArtPaint;

public sealed record ArtSolid(string Color) : ArtPaint;

/// <summary>Coordinates are fractions of the shape's bounding box unless <see cref="UserSpace"/> is set (then drawing units).</summary>
public sealed record ArtLinearGradient(double X1, double Y1, double X2, double Y2, bool UserSpace, IReadOnlyList<ArtStop> Stops) : ArtPaint;

public sealed record ArtRadialGradient(double Cx, double Cy, double Radius, double Fx, double Fy, bool UserSpace, IReadOnlyList<ArtStop> Stops) : ArtPaint;

public enum ArtLineCap { Butt, Round, Square }
public enum ArtLineJoin { Miter, Round, Bevel }

/// <summary>A node of the drawing: a group or a path.</summary>
public abstract class ArtNode
{
    public string? Id { get; init; }
    public string? Class { get; init; }
    public double Opacity { get; init; } = 1;
    public ArtMatrix Transform { get; init; } = ArtMatrix.Identity;

    /// <summary>Path data of a clip, in this node's parent coordinates, or null.</summary>
    public string? ClipPath { get; init; }

    /// <summary>Gaussian blur standard deviation applied to this node, or 0.</summary>
    public double Blur { get; init; }

    /// <summary>The custom data-* attributes, keyed without the "data-" prefix.</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();

    public bool HasClass(string name) => Class != null && Class.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name);
    public string? Get(string key) => Data.TryGetValue(key, out var v) ? v : null;
}

public sealed class ArtGroup : ArtNode
{
    public List<ArtNode> Children { get; } = new();
}

public sealed class ArtPath : ArtNode
{
    public required string D { get; init; }
    public ArtPaint? Fill { get; init; }
    public double FillOpacity { get; init; } = 1;
    public ArtPaint? Stroke { get; init; }
    public double StrokeOpacity { get; init; } = 1;
    public double StrokeWidth { get; init; } = 1;
    public ArtLineCap LineCap { get; init; }
    public ArtLineJoin LineJoin { get; init; }
    public IReadOnlyList<double>? DashArray { get; init; }

    /// <summary>The shape that decides where a click lands on its part (drawn invisibly).</summary>
    public bool IsHit => HasClass("hit");

    /// <summary>A highlight that fades in as its part is pressed (data-fx="glow").</summary>
    public bool IsGlow => Get("fx") == "glow";
}

public enum ArtTextAnchor { Start, Middle, End }

/// <summary>
/// A line of text, such as the legend printed on a key. <see cref="X"/> is where the text starts, centres or ends (see
/// <see cref="Anchor"/>); <see cref="Y"/> is the middle of the line when <see cref="Central"/> is set, else its baseline.
/// </summary>
public sealed class ArtText : ArtNode
{
    public required string Text { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double FontSize { get; init; } = 12;

    /// <summary>CSS weight: 400 normal, 600 semibold, 700 bold.</summary>
    public int FontWeight { get; init; } = 400;

    public string Fill { get; init; } = "#000000";
    public string? FontFamily { get; init; }
    public ArtTextAnchor Anchor { get; init; }
    public bool Central { get; init; }

    /// <summary>The vertical middle of the line, in drawing units.</summary>
    public double CenterY => Central ? Y : Y - FontSize * 0.35;
}

/// <summary>How a part reacts to its input.</summary>
public enum ArtMotion
{
    /// <summary>Squashes toward its pivot and sinks a little while held.</summary>
    Press,

    /// <summary>Follows an analog value (a trigger) from 0 to 1.</summary>
    Trigger,

    /// <summary>A thumbstick: the cap follows the stick position and shrinks while clicked.</summary>
    Stick
}

/// <summary>One animated, clickable piece of the drawing, driven by one controller input.</summary>
public sealed class ArtPart
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>The controller input that drives this part (None for a keyboard key).</summary>
    public required ControllerButton Button { get; init; }

    /// <summary>For a keyboard key, its profile name (for example "A", "Escape", "ShiftKey"); null for controller parts.</summary>
    public string? Key { get; init; }
    public required ArtMotion Motion { get; init; }
    public required ArtGroup Group { get; init; }

    /// <summary>Pivot of the press and trigger motions, in drawing units.</summary>
    public required (double X, double Y) Origin { get; init; }

    /// <summary>Scale reached when fully pressed (Press) or fully clicked (Stick).</summary>
    public double PressScale { get; init; } = 0.94;

    /// <summary>How far a pressed part sinks, in drawing units (Press).</summary>
    public double PressDy { get; init; } = 1.5;

    /// <summary>How far the part travels at full deflection: a trigger's pull, or a stick cap's reach.</summary>
    public double Travel { get; init; }

    /// <summary>Box around the clickable shapes, in drawing units.</summary>
    public required ArtRect HitBounds { get; init; }
}

/// <summary>A complete drawing: its static look plus the parts that move.</summary>
public sealed class ArtScene
{
    /// <summary>Top-left of the visible area in drawing units (the viewBox origin); shapes are authored in these coordinates.</summary>
    public double OriginX { get; init; }
    public double OriginY { get; init; }

    /// <summary>Size of the visible area in drawing units.</summary>
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required ArtGroup Root { get; init; }
    public required IReadOnlyList<ArtPart> Parts { get; init; }

    public ArtPart? PartFor(ControllerButton button) => Parts.FirstOrDefault(p => p.Key == null && p.Button == button);

    public ArtPart? PartForKey(string key) => Parts.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}
