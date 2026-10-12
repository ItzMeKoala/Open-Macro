namespace ReWASD.Core.Models;

/// <summary>One slice of the command wheel: a label and what choosing it does.</summary>
public class WheelItem
{
    public string Label { get; set; } = string.Empty;
    public MacroDefinition Macro { get; set; } = new();

    public WheelItem Clone() => new() { Label = Label, Macro = Macro.Clone() };
}

/// <summary>
/// A radial menu: hold a button, point a stick at a slice, let go of the button to run it. The button and the stick are used up
/// while the wheel is open.
/// </summary>
public class CommandWheel
{
    public const int MaxItems = 8;

    /// <summary>The button that opens the wheel while held. None switches the wheel off.</summary>
    public ControllerButton Button { get; set; } = ControllerButton.None;

    /// <summary>The stick that points at slices (the right one unless this is turned off).</summary>
    public bool UseRightStick { get; set; } = true;

    /// <summary>How far the stick has to be pushed before a slice counts as chosen, 0.2 to 0.9.</summary>
    public double Threshold { get; set; } = 0.5;

    public List<WheelItem> Items { get; set; } = new();

    public bool IsOn => Button != ControllerButton.None && Items.Count >= 2;

    public CommandWheel Clone() => new()
    {
        Button = Button,
        UseRightStick = UseRightStick,
        Threshold = Threshold,
        Items = Items.Select(i => i.Clone()).ToList()
    };

    /// <summary>
    /// Which slice a stick position points at, counting from the top and going clockwise, or -1 when it is not pushed far enough.
    /// X is right-positive and Y is up-positive, as everywhere else.
    /// </summary>
    public static int SliceAt(double x, double y, int count, double threshold)
    {
        if (count < 2) return -1;
        double push = Math.Sqrt(x * x + y * y);
        if (double.IsNaN(push) || push < Math.Clamp(threshold, 0.2, 0.9)) return -1;

        double degrees = Math.Atan2(x, y) * 180.0 / Math.PI;     // 0 = up, 90 = right
        if (degrees < 0) degrees += 360;
        double width = 360.0 / count;
        return (int)Math.Floor((degrees + width / 2) / width) % count;
    }

    /// <summary>The compass angles (degrees, 0 = up, clockwise) between which a slice is drawn.</summary>
    public static (double Start, double End) SliceAngles(int index, int count)
    {
        double width = 360.0 / count;
        double centre = index * width;
        return (centre - width / 2, centre + width / 2);
    }
}
