using ReWASD.Core.Models;

namespace ReWASD.UI;

/// <summary>Listens to the pad while a person presses the buttons of a new combo, so they do not have to type the names.</summary>
public sealed class ComboCapture
{
    /// <summary>How long a sequence waits for the next press before it counts as finished.</summary>
    public const long SequenceIdleMs = 1500;

    private readonly List<ControllerButton> _recorded = new();
    private readonly HashSet<ControllerButton> _held = new();
    private long _lastPress;

    public bool IsCapturing { get; private set; }
    public ComboKind Kind { get; private set; }
    public IReadOnlyList<ControllerButton> Recorded => _recorded;

    public void Start(ComboKind kind, long now)
    {
        Kind = kind;
        IsCapturing = true;
        _recorded.Clear();
        _held.Clear();
        _lastPress = now;
    }

    public void Cancel() => IsCapturing = false;

    /// <summary>
    /// Feed the buttons held right now, many times a second. Returns the finished button list once the person is done
    /// (a chord: everything let go; a sequence: no new press for a moment), otherwise null.
    /// </summary>
    public IReadOnlyList<ControllerButton>? Feed(IReadOnlyCollection<ControllerButton> pressed, long now)
    {
        if (!IsCapturing) return null;

        foreach (var b in pressed)
        {
            if (!_held.Add(b)) continue;                       // already held: not a new press
            _lastPress = now;
            if (Kind == ComboKind.Sequence || !_recorded.Contains(b)) _recorded.Add(b);
        }
        _held.RemoveWhere(b => !pressed.Contains(b));

        bool done = Kind == ComboKind.Chord
            ? _recorded.Count > 0 && pressed.Count == 0
            : _recorded.Count > 0 && now - _lastPress >= SequenceIdleMs;
        if (!done) return null;

        IsCapturing = false;
        return _recorded.ToList();
    }
}
