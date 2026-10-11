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
    Macro
}

public enum MappingMode
{
    Normal,
    Turbo,
    Toggle
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
