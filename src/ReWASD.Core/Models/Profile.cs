namespace ReWASD.Core.Models;

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default Profile";
    public string? ProcessExecutable { get; set; }
    public ControllerType TargetControllerType { get; set; } = ControllerType.XboxOne;

    public int ActiveSlot { get; set; } = 1; // 1 to 4
    public Dictionary<int, ShiftLayer> ShiftLayers { get; set; } = new();

    public StickSettings LeftStick { get; set; } = new();
    public StickSettings RightStick { get; set; } = new();

    public Profile()
    {
        InitializeDefaultLayers();
    }

    public void InitializeDefaultLayers()
    {
        ShiftLayers[0] = new ShiftLayer { LayerIndex = 0, Name = "Main" };
        ShiftLayers[1] = new ShiftLayer { LayerIndex = 1, Name = "Shift 1" };
        ShiftLayers[2] = new ShiftLayer { LayerIndex = 2, Name = "Shift 2" };
        ShiftLayers[3] = new ShiftLayer { LayerIndex = 3, Name = "Shift 3" };
        ShiftLayers[4] = new ShiftLayer { LayerIndex = 4, Name = "Shift 4" };

        // Populate Main layer with 1:1 default pass-through mappings
        var main = ShiftLayers[0];
        foreach (ControllerButton btn in Enum.GetValues<ControllerButton>())
        {
            if (btn == ControllerButton.None) continue;
            main.Mappings[btn] = new ButtonMapping
            {
                SourceButton = btn,
                Target = RemapTarget.Default(btn),
                Mode = MappingMode.Normal
            };
        }
    }

    public static Profile CreateDefault(string name = "Default Profile")
    {
        var p = new Profile { Name = name };
        return p;
    }
}
