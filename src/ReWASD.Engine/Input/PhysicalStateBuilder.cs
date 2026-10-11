using ReWASD.Core.Models;

namespace ReWASD.Engine.Input;

/// <summary>
/// Turns raw pad readings into a <see cref="PhysicalState"/>. Analog inputs also act as buttons so they can be
/// remapped: a trigger past a threshold is a press, and a stick pushed past a threshold is a direction.
/// Every platform's pad reader goes through this so the thresholds are identical everywhere.
/// </summary>
public static class PhysicalStateBuilder
{
    public const double TriggerPressThreshold = 0.35;
    public const double StickDirectionThreshold = 0.5;

    /// <param name="buttons">The digital buttons that are down; analog-derived buttons are added here.</param>
    /// <param name="lx">Sticks are -1..1 with up and right positive; triggers are 0..1.</param>
    public static PhysicalState Build(HashSet<ControllerButton> buttons,
        double lx, double ly, double rx, double ry, double lt, double rt)
    {
        if (lt > TriggerPressThreshold) buttons.Add(ControllerButton.LeftTrigger);
        if (rt > TriggerPressThreshold) buttons.Add(ControllerButton.RightTrigger);
        AddDirections(buttons, lx, ly, ControllerButton.LeftStickLeft, ControllerButton.LeftStickRight, ControllerButton.LeftStickDown, ControllerButton.LeftStickUp);
        AddDirections(buttons, rx, ry, ControllerButton.RightStickLeft, ControllerButton.RightStickRight, ControllerButton.RightStickDown, ControllerButton.RightStickUp);

        return new PhysicalState(buttons, lx, ly, rx, ry, lt, rt);
    }

    private static void AddDirections(HashSet<ControllerButton> buttons, double x, double y,
        ControllerButton left, ControllerButton right, ControllerButton down, ControllerButton up)
    {
        if (x < -StickDirectionThreshold) buttons.Add(left);
        if (x > StickDirectionThreshold) buttons.Add(right);
        if (y < -StickDirectionThreshold) buttons.Add(down);
        if (y > StickDirectionThreshold) buttons.Add(up);
    }
}
