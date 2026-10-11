namespace ReWASD.Core.Models;

public class RemapTarget
{
    public TargetType Type { get; set; } = TargetType.None;
    
    // For VirtualControllerButton (maps to ControllerButton enum values like A, B, RT)
    public ControllerButton VirtualButton { get; set; } = ControllerButton.None;

    // For KeyboardKey (e.g., "Space", "ShiftKey", "W", "A", "S", "D")
    public string KeyboardKey { get; set; } = string.Empty;

    // For MouseButton (e.g., "Left", "Right", "Middle", "XButton1", "XButton2")
    public string MouseButton { get; set; } = string.Empty;

    // Display string helper
    public string DisplayText => Type switch
    {
        TargetType.VirtualControllerButton => VirtualButton != ControllerButton.None ? VirtualButton.ToString() : "Unassigned",
        TargetType.KeyboardKey => !string.IsNullOrEmpty(KeyboardKey) ? $"Key: {KeyboardKey}" : "Unassigned",
        TargetType.MouseButton => !string.IsNullOrEmpty(MouseButton) ? $"Mouse: {MouseButton}" : "Unassigned",
        TargetType.MouseScroll => "Mouse Scroll",
        TargetType.Macro => "Macro Sequence",
        _ => "Unassigned"
    };

    public static RemapTarget Default(ControllerButton button) => new()
    {
        Type = TargetType.VirtualControllerButton,
        VirtualButton = button
    };

    public static RemapTarget Unassigned => new() { Type = TargetType.None };
}
