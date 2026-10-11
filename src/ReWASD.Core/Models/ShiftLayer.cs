namespace ReWASD.Core.Models;

public class ShiftLayer
{
    public int LayerIndex { get; set; } = 0; // 0 = Main, 1 = Shift 1, etc.
    public string Name { get; set; } = "Main";
    public ControllerButton ModifierButton { get; set; } = ControllerButton.None;
    public Dictionary<ControllerButton, ButtonMapping> Mappings { get; set; } = new();

    public ShiftLayer Clone()
    {
        var layer = new ShiftLayer
        {
            LayerIndex = LayerIndex,
            Name = Name,
            ModifierButton = ModifierButton,
            Mappings = new Dictionary<ControllerButton, ButtonMapping>()
        };

        foreach (var kvp in Mappings)
        {
            layer.Mappings[kvp.Key] = kvp.Value.Clone();
        }

        return layer;
    }
}
