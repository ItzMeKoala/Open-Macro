namespace ReWASD.Core.Models;

public class ShiftLayer
{
    public const int MaxModifierButtons = 4;

    public int LayerIndex { get; set; } = 0; // 0 = Main, 1 = Shift 1, etc.
    public string Name { get; set; } = "Main";

    /// <summary>The chord that activates this layer: every listed button must be held (at most 4, like reWASD's shift sets).</summary>
    public List<ControllerButton> ModifierButtons { get; set; } = new();

    public ShiftType Type { get; set; } = ShiftType.Hold;

    public Dictionary<ControllerButton, ButtonMapping> Mappings { get; set; } = new();

    /// <summary>Keyboard keys used as sources, by key name (e.g. "W", "Space"). A mapping here suppresses the original key.</summary>
    public Dictionary<string, ButtonMapping> KeyboardMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mouse buttons and wheel directions used as sources, by name (e.g. "Left Click", "Wheel Up").</summary>
    public Dictionary<string, ButtonMapping> MouseMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>JSON deserialization replaces the dictionaries and drops their case-insensitive comparers; restore them.</summary>
    public void RestoreComparers()
    {
        KeyboardMappings = new Dictionary<string, ButtonMapping>(KeyboardMappings ?? new(), StringComparer.OrdinalIgnoreCase);
        MouseMappings = new Dictionary<string, ButtonMapping>(MouseMappings ?? new(), StringComparer.OrdinalIgnoreCase);
    }

    public ShiftLayer Clone()
    {
        var layer = new ShiftLayer
        {
            LayerIndex = LayerIndex,
            Name = Name,
            ModifierButtons = ModifierButtons.ToList(),
            Type = Type
        };

        foreach (var kvp in Mappings) layer.Mappings[kvp.Key] = kvp.Value.Clone();
        foreach (var kvp in KeyboardMappings) layer.KeyboardMappings[kvp.Key] = kvp.Value.Clone();
        foreach (var kvp in MouseMappings) layer.MouseMappings[kvp.Key] = kvp.Value.Clone();

        return layer;
    }
}
