using Nefarius.ViGEm.Client.Targets.DualShock4;
using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.VirtualOutput;

/// <summary>Stick and trigger values for one frame, after button-driven overrides, ready to convert for a virtual pad.</summary>
public readonly record struct PadAxes(double LeftX, double LeftY, double RightX, double RightY, double LeftTrigger, double RightTrigger);

/// <summary>One frame in DualShock 4 terms.</summary>
public sealed record Ds4Frame(
    IReadOnlySet<DualShock4Button> Buttons,
    bool Ps,
    DualShock4DPadDirection DPad,
    byte LeftX, byte LeftY, byte RightX, byte RightY,
    byte LeftTrigger, byte RightTrigger);

public static class VirtualPadMath
{
    /// <summary>Trigger value above which the DualShock's digital L2/R2 button also reads as pressed.</summary>
    public const double DigitalTriggerThreshold = 0.3;

    /// <summary>
    /// Resolves the frame's analog values: a stick-direction button remapped onto the virtual pad pushes that stick
    /// fully, and a button remapped onto a trigger counts as a full pull.
    /// </summary>
    public static PadAxes ResolveAxes(RemappedState state)
    {
        var pressed = state.ActiveVirtualButtons;
        double lt = pressed.Contains(ControllerButton.LeftTrigger) ? 1.0 : state.LeftTrigger;
        double rt = pressed.Contains(ControllerButton.RightTrigger) ? 1.0 : state.RightTrigger;

        return new PadAxes(
            DirectionOverride(pressed, ControllerButton.LeftStickLeft, ControllerButton.LeftStickRight, state.LeftStickX),
            DirectionOverride(pressed, ControllerButton.LeftStickDown, ControllerButton.LeftStickUp, state.LeftStickY),
            DirectionOverride(pressed, ControllerButton.RightStickLeft, ControllerButton.RightStickRight, state.RightStickX),
            DirectionOverride(pressed, ControllerButton.RightStickDown, ControllerButton.RightStickUp, state.RightStickY),
            lt, rt);
    }

    private static double DirectionOverride(HashSet<ControllerButton> pressed, ControllerButton negative, ControllerButton positive, double analog)
    {
        bool neg = pressed.Contains(negative), pos = pressed.Contains(positive);
        if (neg == pos) return analog; // neither, or both cancelling out
        return pos ? 1.0 : -1.0;
    }

    /// <summary>-1..1 to the signed 16-bit range an Xbox 360 pad uses.</summary>
    public static short ToXboxAxis(double value) =>
        (short)Math.Clamp((int)Math.Round(value * 32767), -32768, 32767);

    /// <summary>0..1 to the 8-bit range of a trigger.</summary>
    public static byte ToByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    /// <summary>-1..1 to 0..255 with 128 at centre. A DualShock's Y axis runs top-down, so up is 0.</summary>
    public static byte ToDs4Axis(double value, bool invert = false)
    {
        if (invert) value = -value;
        return (byte)Math.Clamp((int)Math.Round((Math.Clamp(value, -1, 1) + 1) / 2 * 255), 0, 255);
    }

    /// <summary>Converts a frame to DualShock 4 terms.</summary>
    public static Ds4Frame ToDs4(RemappedState state)
    {
        var p = state.ActiveVirtualButtons;
        var axes = ResolveAxes(state);
        var buttons = new HashSet<DualShock4Button>();

        void Map(ControllerButton source, DualShock4Button target)
        {
            if (p.Contains(source)) buttons.Add(target);
        }

        Map(ControllerButton.A, DualShock4Button.Cross);
        Map(ControllerButton.B, DualShock4Button.Circle);
        Map(ControllerButton.X, DualShock4Button.Square);
        Map(ControllerButton.Y, DualShock4Button.Triangle);
        Map(ControllerButton.LeftBumper, DualShock4Button.ShoulderLeft);
        Map(ControllerButton.RightBumper, DualShock4Button.ShoulderRight);
        Map(ControllerButton.LeftStickClick, DualShock4Button.ThumbLeft);
        Map(ControllerButton.RightStickClick, DualShock4Button.ThumbRight);
        Map(ControllerButton.View, DualShock4Button.Share);
        Map(ControllerButton.Menu, DualShock4Button.Options);

        if (axes.LeftTrigger >= DigitalTriggerThreshold) buttons.Add(DualShock4Button.TriggerLeft);
        if (axes.RightTrigger >= DigitalTriggerThreshold) buttons.Add(DualShock4Button.TriggerRight);

        return new Ds4Frame(
            buttons,
            p.Contains(ControllerButton.Guide),
            DPad(p.Contains(ControllerButton.DpadUp), p.Contains(ControllerButton.DpadRight), p.Contains(ControllerButton.DpadDown), p.Contains(ControllerButton.DpadLeft)),
            ToDs4Axis(axes.LeftX), ToDs4Axis(axes.LeftY, invert: true),
            ToDs4Axis(axes.RightX), ToDs4Axis(axes.RightY, invert: true),
            ToByte(axes.LeftTrigger), ToByte(axes.RightTrigger));
    }

    /// <summary>A DualShock reports the d-pad as one of eight directions, so opposite buttons cancel out.</summary>
    public static DualShock4DPadDirection DPad(bool up, bool right, bool down, bool left)
    {
        if (up && down) up = down = false;
        if (left && right) left = right = false;

        return (up, right, down, left) switch
        {
            (true, true, _, _) => DualShock4DPadDirection.Northeast,
            (true, _, _, true) => DualShock4DPadDirection.Northwest,
            (_, true, true, _) => DualShock4DPadDirection.Southeast,
            (_, _, true, true) => DualShock4DPadDirection.Southwest,
            (true, _, _, _) => DualShock4DPadDirection.North,
            (_, true, _, _) => DualShock4DPadDirection.East,
            (_, _, true, _) => DualShock4DPadDirection.South,
            (_, _, _, true) => DualShock4DPadDirection.West,
            _ => DualShock4DPadDirection.None
        };
    }
}
