namespace ReWASD.Core.Models;

public class RemapTarget
{
    public TargetType Type { get; set; } = TargetType.None;
    
    // For VirtualControllerButton (maps to ControllerButton enum values like A, B, RT)
    public ControllerButton VirtualButton { get; set; } = ControllerButton.None;

    // For KeyboardKey (e.g., "Space", "ShiftKey", "W", "A", "S", "D")
    public string KeyboardKey { get; set; } = string.Empty;

    // For MouseButton: "Left Click", "Right Click", "Middle Click", "Mouse Button 4", "Mouse Button 5",
    // "Wheel Up", "Wheel Down". Short forms ("Left", "XButton1", ...) are accepted too; see KeyMap.TryResolveMouse.
    public string MouseButton { get; set; } = string.Empty;

    // For MouseScroll: positive scrolls up, negative scrolls down
    public int ScrollDelta { get; set; } = 1;

    // For Macro
    public MacroDefinition? Macro { get; set; }

    // For JumpToLayer: the layer index to switch to while the mapping is active
    public int Layer { get; set; }

    public RemapTarget Clone() => new()
    {
        Type = Type,
        VirtualButton = VirtualButton,
        KeyboardKey = KeyboardKey,
        MouseButton = MouseButton,
        ScrollDelta = ScrollDelta,
        Macro = Macro?.Clone(),
        Layer = Layer
    };

    // Display string helper
    public string DisplayText => Type switch
    {
        TargetType.VirtualControllerButton => VirtualButton != ControllerButton.None ? VirtualButton.ToString() : "Unassigned",
        TargetType.KeyboardKey => !string.IsNullOrEmpty(KeyboardKey) ? $"Key: {KeyboardKey}" : "Unassigned",
        TargetType.MouseButton => !string.IsNullOrEmpty(MouseButton) ? $"Mouse: {MouseButton}" : "Unassigned",
        TargetType.MouseScroll => ScrollDelta >= 0 ? "Scroll Up" : "Scroll Down",
        TargetType.Macro => Macro is { Steps.Count: > 0 } ? $"Macro ({Macro.Steps.Count} steps)" : "Unassigned",
        TargetType.JumpToLayer => Layer == 0 ? "Layer: Main" : $"Layer: Shift {Layer}",
        _ => "Unassigned"
    };

    public static RemapTarget Default(ControllerButton button) => new()
    {
        Type = TargetType.VirtualControllerButton,
        VirtualButton = button
    };

    public static RemapTarget Unassigned => new() { Type = TargetType.None };
}
