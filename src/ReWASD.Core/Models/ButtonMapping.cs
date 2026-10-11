namespace ReWASD.Core.Models;

public class ButtonMapping
{
    public ControllerButton SourceButton { get; set; }
    public RemapTarget Target { get; set; } = new();
    public MappingMode Mode { get; set; } = MappingMode.Normal;
    public int TurboRateMs { get; set; } = 50;
    public bool IsMuted { get; set; } = false; // Mutes original hardware input so game only sees remap

    public ButtonMapping Clone() => new()
    {
        SourceButton = SourceButton,
        Target = new RemapTarget
        {
            Type = Target.Type,
            VirtualButton = Target.VirtualButton,
            KeyboardKey = Target.KeyboardKey,
            MouseButton = Target.MouseButton
        },
        Mode = Mode,
        TurboRateMs = TurboRateMs,
        IsMuted = IsMuted
    };
}
