namespace ReWASD.Core.Models;

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default Profile";
    public string? ProcessExecutable { get; set; }
    public ControllerType TargetControllerType { get; set; } = ControllerType.Xbox360;

    public int ActiveSlot { get; set; } = 1; // 1 to 4
    public Dictionary<int, ShiftLayer> ShiftLayers { get; set; } = new();

    public ActivatorTimings Timings { get; set; } = new();

    /// <summary>
    /// Buttons that lock and unlock the controller when all are held together for <see cref="LockHoldMs"/>.
    /// While locked, no button, stick or trigger does anything. Empty turns the chord off.
    /// </summary>
    public List<ControllerButton> LockChord { get; set; } = new();

    public int LockHoldMs { get; set; } = 1000;

    /// <summary>A radial menu opened by holding a button. Off unless a button is chosen and it has at least two slices.</summary>
    public CommandWheel Wheel { get; set; } = new();

    /// <summary>Button combinations (chords and sequences) that do something of their own.</summary>
    public List<ComboMapping> Combos { get; set; } = new();

    /// <summary>Chords: how close together in time the buttons must go down to count as one chord. Buttons that are part of a chord wait this long before acting alone.</summary>
    public int ChordWindowMs { get; set; } = 120;

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
