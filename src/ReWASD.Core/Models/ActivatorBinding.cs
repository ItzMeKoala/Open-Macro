namespace ReWASD.Core.Models;

/// <summary>One outcome of a source, tied to one activator type (e.g. "on double press, run this macro").</summary>
public class ActivatorBinding
{
    public ActivatorType Type { get; set; } = ActivatorType.Single;
    public RemapTarget Target { get; set; } = new();
    public MappingMode Mode { get; set; } = MappingMode.Normal;
    public int TurboRateMs { get; set; } = 50;

    public ActivatorBinding Clone() => new()
    {
        Type = Type,
        Target = Target.Clone(),
        Mode = Mode,
        TurboRateMs = TurboRateMs
    };
}

/// <summary>Timing thresholds for multi-press and long-press activators. The defaults are this project's choice.</summary>
public class ActivatorTimings
{
    /// <summary>Hold time after which a Long activator fires.</summary>
    public int LongPressMs { get; set; } = 400;

    /// <summary>Maximum gap between presses for them to count as a double or triple press.</summary>
    public int MultiPressWindowMs { get; set; } = 250;

    /// <summary>Length of the pulse produced by OneTime and by activations that fire after the button was released.</summary>
    public int PulseMs { get; set; } = 40;
}
