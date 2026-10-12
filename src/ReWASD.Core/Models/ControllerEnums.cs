namespace ReWASD.Core.Models;

public enum ControllerButton
{
    None = 0,
    
    // Face Buttons
    A,
    B,
    X,
    Y,

    // D-Pad
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,

    // Bumpers & Triggers
    LeftBumper,
    RightBumper,
    LeftTrigger,
    RightTrigger,

    // Sticks
    LeftStickClick,
    RightStickClick,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,

    // Menu / Navigation
    View,
    Menu,
    Guide,

    // Paddles (Elite / Scuf)
    Paddle1,
    Paddle2,
    Paddle3,
    Paddle4
}

public enum TargetType
{
    None,
    VirtualControllerButton,
    KeyboardKey,
    MouseButton,
    MouseScroll,
    Macro,
    JumpToLayer
}

/// <summary>What the outcome does once an activator fires. Mirrors reWASD's hold_until_release / repeat / toggle / onetime.</summary>
public enum MappingMode
{
    /// <summary>Held for as long as the activation lasts (reWASD's hold_until_release).</summary>
    Normal,

    /// <summary>Repeats while held (reWASD's repeat / turbo).</summary>
    Turbo,

    /// <summary>Latches on and off with each activation.</summary>
    Toggle,

    /// <summary>A single short pulse per activation.</summary>
    OneTime
}

/// <summary>When a mapping fires, relative to presses of its source.</summary>
public enum ActivatorType
{
    /// <summary>One press. If other multi-press activators exist, waits to see whether more presses follow.</summary>
    Single,

    /// <summary>Held past the long-press threshold.</summary>
    Long,

    /// <summary>Two presses in quick succession.</summary>
    Double,

    /// <summary>Three presses in quick succession.</summary>
    Triple,

    /// <summary>Fires immediately when the source goes down, regardless of what follows.</summary>
    Start,

    /// <summary>Fires when the source is released.</summary>
    Release
}

public enum ShiftType
{
    /// <summary>Layer is active while its modifier chord is held.</summary>
    Hold,

    /// <summary>Pressing the modifier chord switches the layer on or off.</summary>
    Toggle
}

/// <summary>The kind of physical device a source belongs to.</summary>
public enum SourceKind
{
    Pad,
    Key,
    Mouse,

    /// <summary>A chord or sequence of pad buttons (see <see cref="ComboMapping"/>).</summary>
    Combo
}

/// <summary>What a stick drives while remapping is on.</summary>
public enum StickMode
{
    /// <summary>The stick stays a stick: it moves the virtual controller's stick (Windows) and nothing else.</summary>
    Gamepad,

    /// <summary>Moves the mouse cursor.</summary>
    Mouse,

    /// <summary>Scrolls the wheel up and down.</summary>
    Scroll,

    /// <summary>Holds a key per direction (WASD, the arrow keys, or any keys), with diagonals.</summary>
    Keys
}

public enum ResponseCurveType
{
    Linear,
    Smooth,
    Aggressive,
    Instant,
    Custom
}

public enum ControllerType
{
    XboxOne,
    Xbox360,
    DualShock4,
    DualSense,
    SwitchPro,
    KeyboardMouseComposite
}
