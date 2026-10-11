namespace ReWASD.Core.Skins.Art;

/// <summary>A damped spring that chases a target, so presses sink and release with a little life instead of snapping.</summary>
public struct ArtSpring
{
    public double Value;
    public double Velocity;
    public double Target;

    /// <summary>Natural frequency in radians per second: higher is snappier.</summary>
    public double Omega;

    /// <summary>1 settles without overshoot; below 1 overshoots and rings a little.</summary>
    public double Damping;

    public ArtSpring(double omega, double damping)
    {
        Value = 0;
        Velocity = 0;
        Target = 0;
        Omega = omega;
        Damping = damping;
    }

    public readonly bool IsSettled => Math.Abs(Target - Value) < 0.0015 && Math.Abs(Velocity) < 0.01;

    public void Step(double seconds)
    {
        // Small fixed sub-steps keep the integration stable however long a frame took
        const double Sub = 1.0 / 240;
        int n = Math.Max(1, (int)Math.Ceiling(Math.Min(seconds, 0.1) / Sub));
        double h = Math.Min(seconds, 0.1) / n;
        for (int i = 0; i < n; i++)
        {
            double accel = Omega * Omega * (Target - Value) - 2 * Damping * Omega * Velocity;
            Velocity += accel * h;
            Value += Velocity * h;
        }

        if (IsSettled)
        {
            Value = Target;
            Velocity = 0;
        }
    }
}

/// <summary>The motion of one part for one frame: how to transform it, and how lit its highlight is.</summary>
public readonly record struct ArtPose(ArtMatrix Matrix, double Glow);

/// <summary>
/// Turns what a controller input is doing (pressed, how far a trigger is pulled, where a stick is) into a smooth pose for
/// its part of the drawing. Pure maths, so every front end animates the same way.
/// </summary>
public sealed class ArtPartAnimator
{
    // A tap shorter than a display frame still shows as a press for this long
    private const double MinimumPressSeconds = 0.075;

    private ArtSpring _press;
    private ArtSpring _analog;
    private ArtSpring _x;
    private ArtSpring _y;
    private ArtSpring _selected;
    private ArtSpring _hover;
    private bool _pressed;
    private double _holdLeft;

    public ArtPartAnimator(ArtPart part)
    {
        Part = part;
        _press = new ArtSpring(46, 0.72);
        _analog = new ArtSpring(40, 0.92);
        _x = new ArtSpring(34, 0.55);
        _y = new ArtSpring(34, 0.55);
        _selected = new ArtSpring(26, 1);
        _hover = new ArtSpring(30, 1);
    }

    public ArtPart Part { get; }

    /// <summary>Strength of the selection highlight, 0 to 1.</summary>
    public double Selection => Math.Clamp(_selected.Value, 0, 1);

    /// <summary>Strength of the pointer-over highlight, 0 to 1.</summary>
    public double Hover => Math.Clamp(_hover.Value, 0, 1);

    public void SetPressed(bool pressed)
    {
        if (pressed && !_pressed) _holdLeft = MinimumPressSeconds;
        _pressed = pressed;
    }

    /// <summary>Trigger pull, 0 to 1.</summary>
    public void SetAnalog(double value) => _analog.Target = Math.Clamp(value, 0, 1);

    /// <summary>Stick position, each axis -1 to 1 with right and down positive (screen directions).</summary>
    public void SetStick(double x, double y)
    {
        double length = Math.Sqrt(x * x + y * y);
        if (length > 1) { x /= length; y /= length; } // a stick's reach is a circle, not a square
        _x.Target = x;
        _y.Target = y;
    }

    public void SetSelected(bool selected) => _selected.Target = selected ? 1 : 0;
    public void SetHover(bool hover) => _hover.Target = hover ? 1 : 0;

    public bool IsSettled =>
        _holdLeft <= 0 && _press.IsSettled && _analog.IsSettled && _x.IsSettled && _y.IsSettled && _selected.IsSettled && _hover.IsSettled;

    public void Step(double seconds)
    {
        _holdLeft = Math.Max(0, _holdLeft - seconds);
        _press.Target = _pressed || _holdLeft > 0 ? 1 : 0;

        _press.Step(seconds);
        _analog.Step(seconds);
        _x.Step(seconds);
        _y.Step(seconds);
        _selected.Step(seconds);
        _hover.Step(seconds);
    }

    /// <summary>Jumps straight to the targets (no spring), for the first frame and for tests.</summary>
    public void Snap()
    {
        _holdLeft = 0;
        _press.Target = _pressed ? 1 : 0;
        _press.Value = _press.Target; _press.Velocity = 0;
        _analog.Value = _analog.Target; _analog.Velocity = 0;
        _x.Value = _x.Target; _x.Velocity = 0;
        _y.Value = _y.Target; _y.Velocity = 0;
        _selected.Value = _selected.Target; _selected.Velocity = 0;
        _hover.Value = _hover.Target; _hover.Velocity = 0;
    }

    /// <summary>
    /// How to move the part. For a stick this is the pose of the cap (the well around it stays put); for everything else
    /// it is the pose of the whole part.
    /// </summary>
    public ArtPose Pose
    {
        get
        {
            var (ox, oy) = Part.Origin;
            ArtMatrix About(ArtMatrix m) => ArtMatrix.Translate(ox, oy) * m * ArtMatrix.Translate(-ox, -oy);

            switch (Part.Motion)
            {
                case ArtMotion.Trigger:
                {
                    double v = _analog.Value;
                    // A pulled trigger travels toward the player and squashes a little against the body
                    var m = ArtMatrix.Translate(0, Part.Travel * v) * About(ArtMatrix.Scale(1, 1 - 0.06 * Math.Clamp(v, 0, 1.2)));
                    return new ArtPose(m, Math.Clamp(v, 0, 1));
                }

                case ArtMotion.Stick:
                {
                    double p = _press.Value;
                    double s = 1 + (Part.PressScale - 1) * p;
                    var m = ArtMatrix.Translate(_x.Value * Part.Travel, _y.Value * Part.Travel) * About(ArtMatrix.Scale(s, s));
                    return new ArtPose(m, Math.Clamp(p, 0, 1));
                }

                default:
                {
                    double p = _press.Value;
                    double s = 1 + (Part.PressScale - 1) * p;
                    var m = ArtMatrix.Translate(0, Part.PressDy * p) * About(ArtMatrix.Scale(s, s));
                    return new ArtPose(m, Math.Clamp(p, 0, 1));
                }
            }
        }
    }
}
