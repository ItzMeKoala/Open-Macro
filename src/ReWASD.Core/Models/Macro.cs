namespace ReWASD.Core.Models;

public enum MacroActionType
{
    KeyDown,
    KeyUp,
    KeyTap,
    MouseDown,
    MouseUp,
    MouseClick,
    Wheel,
    Delay,

    /// <summary>Moves the mouse cursor. Value = up/down/left/right, Amount = pixels.</summary>
    MouseMove,

    /// <summary>Presses a virtual gamepad button. Value = <see cref="ControllerButton"/> name.</summary>
    PadDown,
    PadUp,
    PadTap,

    /// <summary>Sets a virtual stick or trigger. Value = axis name, Amount = percent (sticks -100..100, triggers 0..100).</summary>
    PadAxis,

    /// <summary>Vibrates the physical controller. Value = "left,right" percent (0-100), Amount = duration in ms.</summary>
    Rumble,

    /// <summary>Ends the macro immediately, including any repeat loop.</summary>
    Break
}

public enum MacroRepeatMode
{
    /// <summary>Runs to the end once per press, even if the button is released early.</summary>
    Once,

    /// <summary>Loops while the button is held and stops when it is released.</summary>
    WhileHeld,

    /// <summary>
    /// Runs the steps once, then keeps whatever they pressed held until the button is released.
    /// This is how one button can press several others at once.
    /// </summary>
    HoldUntilRelease
}

public class MacroStep
{
    public MacroActionType Action { get; set; }

    /// <summary>Key, mouse, pad or axis name, direction or rumble levels, depending on the action. Unused for Wheel, Delay and Break.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Delay in ms, wheel notches (positive = up), pixels, axis percent or rumble duration, depending on the action.</summary>
    public int Amount { get; set; }

    public MacroStep Clone() => new() { Action = Action, Value = Value, Amount = Amount };

    public override string ToString() => Action switch
    {
        MacroActionType.Delay or MacroActionType.Wheel => $"{Action}:{Amount}",
        MacroActionType.Break => "Break",
        MacroActionType.MouseMove or MacroActionType.PadAxis or MacroActionType.Rumble => $"{Action}:{Value}:{Amount}",
        _ => $"{Action}:{Value}"
    };
}

public class MacroDefinition
{
    public static readonly string[] PadAxisNames =
    {
        "LeftStickX", "LeftStickY", "RightStickX", "RightStickY", "LeftTrigger", "RightTrigger"
    };

    private static readonly string[] Directions = { "up", "down", "left", "right" };

    public MacroRepeatMode RepeatMode { get; set; } = MacroRepeatMode.Once;
    public List<MacroStep> Steps { get; set; } = new();

    public MacroDefinition Clone() => new()
    {
        RepeatMode = RepeatMode,
        Steps = Steps.Select(s => s.Clone()).ToList()
    };

    /// <summary>Whether a step undoes an earlier press (or is just waiting), i.e. it is not needed to keep inputs held.</summary>
    public static bool IsReleaseOrWait(MacroStep step) =>
        step.Action is MacroActionType.KeyUp or MacroActionType.MouseUp or MacroActionType.PadUp or MacroActionType.Delay;

    /// <summary>
    /// Drops trailing releases and waits so that, in HoldUntilRelease mode, the inputs the macro pressed stay held.
    /// </summary>
    public static void TrimTrailingReleases(List<MacroStep> steps)
    {
        while (steps.Count > 0 && IsReleaseOrWait(steps[^1])) steps.RemoveAt(steps.Count - 1);
    }

    /// <summary>Formats the steps as "KeyDown:W; Delay:50; KeyUp:W" for editing in a text box.</summary>
    public string ToScript() => string.Join("; ", Steps);

    /// <summary>
    /// Parses the format produced by <see cref="ToScript"/>: <c>Action:Value</c>, or <c>Action:Value:Amount</c> for
    /// MouseMove, PadAxis and Rumble. Returns false and an error message on the first bad step.
    /// </summary>
    public static bool TryParseScript(string script, MacroRepeatMode repeatMode, out MacroDefinition macro, out string error)
    {
        macro = new MacroDefinition { RepeatMode = repeatMode };
        error = string.Empty;

        foreach (var raw in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':', 3, StringSplitOptions.TrimEntries);

            if (!Enum.TryParse<MacroActionType>(parts[0], ignoreCase: true, out var action) || !Enum.IsDefined(action))
            {
                error = $"Unknown action '{parts[0]}'";
                return false;
            }

            var step = new MacroStep { Action = action };

            if (action == MacroActionType.Break)
            {
                if (parts.Length != 1) { error = "Break takes no value"; return false; }
                macro.Steps.Add(step);
                continue;
            }

            if (parts.Length < 2 || parts[1].Length == 0)
            {
                error = $"'{raw}' must look like Action:Value";
                return false;
            }

            switch (action)
            {
                case MacroActionType.Delay:
                case MacroActionType.Wheel:
                    if (parts.Length != 2 || !int.TryParse(parts[1], out int amount) || (action == MacroActionType.Delay && amount < 0))
                    {
                        error = $"'{parts[1]}' is not a valid number for {action}";
                        return false;
                    }
                    step.Amount = amount;
                    break;

                case MacroActionType.MouseMove:
                    if (!RequireAmount(parts, raw, out int pixels, out error)) return false;
                    if (!Directions.Contains(parts[1], StringComparer.OrdinalIgnoreCase))
                    {
                        error = $"'{parts[1]}' is not a direction (up, down, left, right)";
                        return false;
                    }
                    step.Value = parts[1].ToLowerInvariant();
                    step.Amount = pixels;
                    break;

                case MacroActionType.PadDown:
                case MacroActionType.PadUp:
                case MacroActionType.PadTap:
                    if (parts.Length != 2) { error = $"{action} takes only a button name"; return false; }
                    if (!Enum.TryParse<ControllerButton>(parts[1], ignoreCase: true, out var pad) || pad == ControllerButton.None)
                    {
                        error = $"'{parts[1]}' is not a gamepad button";
                        return false;
                    }
                    step.Value = pad.ToString();
                    break;

                case MacroActionType.PadAxis:
                    if (!RequireAmount(parts, raw, out int percent, out error)) return false;
                    var axis = PadAxisNames.FirstOrDefault(n => n.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
                    if (axis == null)
                    {
                        error = $"'{parts[1]}' is not an axis ({string.Join(", ", PadAxisNames)})";
                        return false;
                    }
                    if (percent < -100 || percent > 100)
                    {
                        error = "Axis percent must be between -100 and 100";
                        return false;
                    }
                    step.Value = axis;
                    step.Amount = percent;
                    break;

                case MacroActionType.Rumble:
                    if (!RequireAmount(parts, raw, out int duration, out error)) return false;
                    if (!TryParseRumble(parts[1], out _, out _) || duration < 0)
                    {
                        error = $"'{parts[1]}' must be two percentages like 80,60";
                        return false;
                    }
                    step.Value = parts[1];
                    step.Amount = duration;
                    break;

                default: // key and mouse button actions
                    if (parts.Length != 2) { error = $"{action} takes only a name"; return false; }
                    step.Value = parts[1];
                    break;
            }

            macro.Steps.Add(step);
        }

        if (macro.Steps.Count == 0)
        {
            error = "Macro has no steps";
            return false;
        }

        return true;
    }

    /// <summary>Parses "80,60" into left and right motor strengths from 0 to 1.</summary>
    public static bool TryParseRumble(string value, out double left, out double right)
    {
        left = right = 0;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out int l) || !int.TryParse(parts[1], out int r)) return false;
        if (l < 0 || l > 100 || r < 0 || r > 100) return false;
        left = l / 100.0;
        right = r / 100.0;
        return true;
    }

    private static bool RequireAmount(string[] parts, string raw, out int amount, out string error)
    {
        amount = 0;
        error = string.Empty;
        if (parts.Length != 3 || !int.TryParse(parts[2], out amount))
        {
            error = $"'{raw}' needs a number as its third part (Action:Value:Amount)";
            return false;
        }
        return true;
    }
}
