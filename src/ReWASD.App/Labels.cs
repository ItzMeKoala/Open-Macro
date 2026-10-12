using ReWASD.Core.Models;

namespace ReWASD.UI;

/// <summary>Plain-language names shown in the interface, shared by every toolkit.</summary>
public static class Labels
{
    public static string Mode(MappingMode mode) => mode switch
    {
        MappingMode.Normal => "Hold while pressed",
        MappingMode.Turbo => "Repeat (turbo)",
        MappingMode.Toggle => "Toggle on / off",
        MappingMode.OneTime => "One short press",
        _ => mode.ToString()
    };

    /// <summary>One plain sentence on what a behaviour does, shown under the choice.</summary>
    public static string ModeHelp(MappingMode mode) => mode switch
    {
        MappingMode.Normal => "Stays pressed for as long as you hold the button.",
        MappingMode.Turbo => "Presses again and again while you hold the button.",
        MappingMode.Toggle => "Each press switches it on or off, like a latch.",
        MappingMode.OneTime => "A single short tap, however long you hold.",
        _ => string.Empty
    };

    /// <summary>The short names of the six ways a button can trigger something.</summary>
    public static string Trigger(ActivatorType type) => type switch
    {
        ActivatorType.Single => "Press",
        ActivatorType.Long => "Hold",
        ActivatorType.Double => "Double",
        ActivatorType.Triple => "Triple",
        ActivatorType.Start => "On down",
        ActivatorType.Release => "On release",
        _ => type.ToString()
    };

    public static string TriggerHelp(ActivatorType type) => type switch
    {
        ActivatorType.Single => "One press",
        ActivatorType.Long => "Held past the long-press time (Settings → Combos and timing)",
        ActivatorType.Double => "Two quick presses",
        ActivatorType.Triple => "Three quick presses",
        ActivatorType.Start => "The moment the button goes down, whatever follows",
        ActivatorType.Release => "When the button is let go",
        _ => string.Empty
    };

    public static string MacroRepeat(MacroRepeatMode mode) => mode switch
    {
        MacroRepeatMode.Once => "Run once",
        MacroRepeatMode.WhileHeld => "Repeat while held",
        MacroRepeatMode.HoldUntilRelease => "Hold until released",
        _ => mode.ToString()
    };
}
