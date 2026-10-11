using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.Input;

/// <summary>Where macros send virtual gamepad presses and axis values.</summary>
public interface IPadOutput
{
    void SetButton(ControllerButton button, bool down);

    /// <summary>Sets a stick (-1..1) or trigger (0..1) by axis name; null releases the override.</summary>
    void SetAxis(string axis, double? value);
}

/// <summary>Where macros send vibration for the physical controller.</summary>
public interface IRumbleOutput
{
    /// <summary>Motor strengths from 0 to 1. Zero for both stops the vibration.</summary>
    void Rumble(double left, double right);
}

/// <summary>
/// Pad state driven by macros. The pipeline merges it into each frame's output, so macro presses reach the
/// virtual controller alongside whatever the physical controller is doing.
/// </summary>
public sealed class MacroPadState : IPadOutput
{
    private readonly object _gate = new();
    private readonly Dictionary<ControllerButton, int> _buttonHolds = new();
    private readonly Dictionary<string, double> _axes = new(StringComparer.OrdinalIgnoreCase);

    public void SetButton(ControllerButton button, bool down)
    {
        lock (_gate)
        {
            _buttonHolds.TryGetValue(button, out int count);
            count = down ? count + 1 : Math.Max(0, count - 1); // two macros may hold the same button
            if (count == 0) _buttonHolds.Remove(button);
            else _buttonHolds[button] = count;
        }
    }

    public void SetAxis(string axis, double? value)
    {
        lock (_gate)
        {
            if (value is double v) _axes[axis] = Math.Clamp(v, -1.0, 1.0);
            else _axes.Remove(axis);
        }
    }

    /// <summary>Adds the macros' buttons to the frame and lets their axis values override the physical ones.</summary>
    public void ApplyTo(RemappedState state)
    {
        lock (_gate)
        {
            foreach (var b in _buttonHolds.Keys) state.ActiveVirtualButtons.Add(b);

            foreach (var (axis, value) in _axes)
            {
                switch (axis.ToLowerInvariant())
                {
                    case "leftstickx": state.LeftStickX = value; break;
                    case "leftsticky": state.LeftStickY = value; break;
                    case "rightstickx": state.RightStickX = value; break;
                    case "rightsticky": state.RightStickY = value; break;
                    case "lefttrigger": state.LeftTrigger = Math.Max(0, value); break;
                    case "righttrigger": state.RightTrigger = Math.Max(0, value); break;
                }
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _buttonHolds.Clear();
            _axes.Clear();
        }
    }
}
