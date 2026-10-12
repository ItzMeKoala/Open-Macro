namespace ReWASD.Core.Models;

/// <summary>
/// Finds what a profile could do outside the keyboard and mouse, so a profile from somebody else can be shown for approval
/// before it is used: it may open pages, start programs, run commands or contact servers when a button is pressed.
/// </summary>
public static class ProfileSafety
{
    /// <summary>One line per risky step, like "Runs a command: rm -rf ~ (on A)". Empty when the profile only presses keys and buttons.</summary>
    public static IReadOnlyList<string> FindRiskyActions(Profile profile)
    {
        var found = new List<string>();

        foreach (var layer in profile.ShiftLayers.Values.OrderBy(l => l.LayerIndex))
        {
            string where = layer.LayerIndex == 0 ? string.Empty : $", {layer.Name}";
            foreach (var (button, mapping) in layer.Mappings) Scan(mapping, button.ToString() + where, found);
            foreach (var (key, mapping) in layer.KeyboardMappings) Scan(mapping, $"key {key}{where}", found);
            foreach (var (name, mapping) in layer.MouseMappings) Scan(mapping, $"mouse {name}{where}", found);
        }

        return found;
    }

    private static void Scan(ButtonMapping mapping, string source, List<string> found)
    {
        foreach (var binding in mapping.AllBindings())
        {
            if (binding.Target.Type != TargetType.Macro || binding.Target.Macro is not { } macro) continue;

            foreach (var step in macro.Steps)
            {
                string? what = step.Action switch
                {
                    MacroActionType.Shell => "Runs a command",
                    MacroActionType.LaunchApp => "Starts a program",
                    MacroActionType.Webhook => "Sends a web request",
                    MacroActionType.Obs => "Controls OBS",
                    MacroActionType.OpenUrl => "Opens a web page",
                    _ => null
                };
                if (what != null) found.Add($"{what}: {step.Value} (on {source})");
            }
        }
    }
}
