namespace ReWASD.Core.Models;

/// <summary>
/// What a source (pad button, key or mouse button) does. The top-level Target/Mode/TurboRateMs are the
/// Single activator; <see cref="Activators"/> holds the others (Long, Double, Triple, Start, Release).
/// </summary>
public class ButtonMapping
{
    public ControllerButton SourceButton { get; set; }
    public RemapTarget Target { get; set; } = new();
    public MappingMode Mode { get; set; } = MappingMode.Normal;
    public int TurboRateMs { get; set; } = 50;
    public bool IsMuted { get; set; } = false; // Mutes original hardware input so game only sees remap

    /// <summary>Bindings for every activator other than Single.</summary>
    public List<ActivatorBinding> Activators { get; set; } = new();

    /// <summary>All bindings, with the top-level fields presented as the Single activator.</summary>
    public IEnumerable<ActivatorBinding> AllBindings()
    {
        yield return new ActivatorBinding { Type = ActivatorType.Single, Target = Target, Mode = Mode, TurboRateMs = TurboRateMs };
        foreach (var a in Activators)
        {
            if (a.Type != ActivatorType.Single) yield return a;
        }
    }

    /// <summary>The binding for the given activator, or null if that activator has no outcome.</summary>
    public ActivatorBinding? GetBinding(ActivatorType type)
    {
        if (type == ActivatorType.Single) return AllBindings().First();
        return Activators.FirstOrDefault(a => a.Type == type);
    }

    /// <summary>Sets (or, with a null binding, removes) the outcome for an activator.</summary>
    public void SetBinding(ActivatorType type, ActivatorBinding? binding)
    {
        if (type == ActivatorType.Single)
        {
            if (binding == null) return; // Single is the mapping itself
            Target = binding.Target;
            Mode = binding.Mode;
            TurboRateMs = binding.TurboRateMs;
            return;
        }

        Activators.RemoveAll(a => a.Type == type);
        if (binding != null)
        {
            binding.Type = type;
            Activators.Add(binding);
        }
    }

    public ButtonMapping Clone() => new()
    {
        SourceButton = SourceButton,
        Target = Target.Clone(),
        Mode = Mode,
        TurboRateMs = TurboRateMs,
        IsMuted = IsMuted,
        Activators = Activators.Select(a => a.Clone()).ToList()
    };
}
