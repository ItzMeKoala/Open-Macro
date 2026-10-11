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
    Delay
}

public enum MacroRepeatMode
{
    /// <summary>Runs to the end once per press, even if the button is released early.</summary>
    Once,

    /// <summary>Loops while the button is held and stops when it is released.</summary>
    WhileHeld
}

public class MacroStep
{
    public MacroActionType Action { get; set; }

    /// <summary>Key or mouse button name, as used by <see cref="RemapTarget"/>. Unused for Wheel and Delay.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Delay in milliseconds, or wheel notches (positive = up). Unused for key and button actions.</summary>
    public int Amount { get; set; }

    public MacroStep Clone() => new() { Action = Action, Value = Value, Amount = Amount };

    public override string ToString() => Action switch
    {
        MacroActionType.Delay or MacroActionType.Wheel => $"{Action}:{Amount}",
        _ => $"{Action}:{Value}"
    };
}

public class MacroDefinition
{
    public MacroRepeatMode RepeatMode { get; set; } = MacroRepeatMode.Once;
    public List<MacroStep> Steps { get; set; } = new();

    public MacroDefinition Clone() => new()
    {
        RepeatMode = RepeatMode,
        Steps = Steps.Select(s => s.Clone()).ToList()
    };

    /// <summary>Formats the steps as "KeyDown:W; Delay:50; KeyUp:W" for editing in a text box.</summary>
    public string ToScript() => string.Join("; ", Steps);

    /// <summary>
    /// Parses the format produced by <see cref="ToScript"/>. Returns false and an error message on the first bad step.
    /// </summary>
    public static bool TryParseScript(string script, MacroRepeatMode repeatMode, out MacroDefinition macro, out string error)
    {
        macro = new MacroDefinition { RepeatMode = repeatMode };
        error = string.Empty;

        foreach (var raw in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[1].Length == 0)
            {
                error = $"'{raw}' must look like Action:Value";
                return false;
            }

            if (!Enum.TryParse<MacroActionType>(parts[0], ignoreCase: true, out var action))
            {
                error = $"Unknown action '{parts[0]}'";
                return false;
            }

            var step = new MacroStep { Action = action };
            if (action is MacroActionType.Delay or MacroActionType.Wheel)
            {
                if (!int.TryParse(parts[1], out int amount) || (action == MacroActionType.Delay && amount < 0))
                {
                    error = $"'{parts[1]}' is not a valid number for {action}";
                    return false;
                }
                step.Amount = amount;
            }
            else
            {
                step.Value = parts[1];
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
}
