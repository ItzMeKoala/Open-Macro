namespace ReWASD.Core.Models;

public enum ComboKind
{
    /// <summary>All the buttons pressed together (within a moment of each other) fire the action while they stay held.</summary>
    Chord,

    /// <summary>The buttons pressed one after another, in order, fire the action once.</summary>
    Sequence
}

/// <summary>A set of buttons that does something of its own, like A + B together or Up, Up, Down, Down in a row.</summary>
public class ComboMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ComboKind Kind { get; set; } = ComboKind.Chord;
    public List<ControllerButton> Buttons { get; set; } = new();

    /// <summary>Sequences: the longest wait allowed between two presses.</summary>
    public int SequenceGapMs { get; set; } = 600;

    /// <summary>What the combo does. Normal holds it while a chord is held, one-time pulses it, and so on.</summary>
    public ActivatorBinding Binding { get; set; } = new();

    /// <summary>"A + B" or "Up, Up, Down, Down".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Description => string.Join(Kind == ComboKind.Chord ? " + " : ", ", Buttons.Select(Names.Friendly));

    public ComboMapping Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        Buttons = Buttons.ToList(),
        SequenceGapMs = SequenceGapMs,
        Binding = Binding.Clone()
    };

    /// <summary>Short names for buttons and the reverse, for typing combos by hand ("A+B", "Up Up Down Down", "LB RB").</summary>
    public static class Names
    {
        private static readonly Dictionary<string, ControllerButton> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["up"] = ControllerButton.DpadUp, ["down"] = ControllerButton.DpadDown,
            ["left"] = ControllerButton.DpadLeft, ["right"] = ControllerButton.DpadRight,
            ["lb"] = ControllerButton.LeftBumper, ["rb"] = ControllerButton.RightBumper,
            ["lt"] = ControllerButton.LeftTrigger, ["rt"] = ControllerButton.RightTrigger,
            ["l3"] = ControllerButton.LeftStickClick, ["r3"] = ControllerButton.RightStickClick,
            ["back"] = ControllerButton.View, ["select"] = ControllerButton.View,
            ["start"] = ControllerButton.Menu, ["home"] = ControllerButton.Guide
        };

        private static readonly Dictionary<ControllerButton, string> Short = new()
        {
            [ControllerButton.DpadUp] = "Up", [ControllerButton.DpadDown] = "Down",
            [ControllerButton.DpadLeft] = "Left", [ControllerButton.DpadRight] = "Right",
            [ControllerButton.LeftBumper] = "LB", [ControllerButton.RightBumper] = "RB",
            [ControllerButton.LeftTrigger] = "LT", [ControllerButton.RightTrigger] = "RT",
            [ControllerButton.LeftStickClick] = "L3", [ControllerButton.RightStickClick] = "R3"
        };

        public static string Friendly(ControllerButton button) => Short.TryGetValue(button, out var s) ? s : button.ToString();

        /// <summary>Reads button names separated by plus signs, commas or spaces. Returns false and says which name it did not know.</summary>
        public static bool TryParse(string text, out List<ControllerButton> buttons, out string error)
        {
            buttons = new List<ControllerButton>();
            error = string.Empty;

            foreach (var word in (text ?? string.Empty).Split(new[] { '+', ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Aliases.TryGetValue(word, out var aliased)) buttons.Add(aliased);
                else if (Enum.TryParse<ControllerButton>(word, ignoreCase: true, out var b) && b != ControllerButton.None && Enum.IsDefined(b)) buttons.Add(b);
                else
                {
                    error = $"'{word}' is not a button name";
                    return false;
                }
            }
            return true;
        }
    }
}
