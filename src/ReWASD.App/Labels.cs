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

    public static string MacroRepeat(MacroRepeatMode mode) => mode switch
    {
        MacroRepeatMode.Once => "Run once",
        MacroRepeatMode.WhileHeld => "Repeat while held",
        MacroRepeatMode.HoldUntilRelease => "Hold until released",
        _ => mode.ToString()
    };
}
