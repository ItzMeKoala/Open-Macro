namespace ReWASD.Core.Models;

/// <summary>Ready-made configs to start from, for the things people most often want a controller to do on a computer.</summary>
public static class ProfilePresets
{
    public sealed record Preset(string Id, string Name, string Description, Func<Profile> Create);

    public static IReadOnlyList<Preset> All { get; } = new[]
    {
        new Preset("couch", "Couch mouse", "Left stick moves the cursor, right stick scrolls; A clicks, B right-clicks, D-pad is the arrow keys.", Couch),
        new Preset("wasd", "WASD game", "Left stick is W A S D, right stick looks around with the mouse; triggers click; A jumps.", Wasd),
        new Preset("presenter", "Presentation clicker", "A or D-pad right goes to the next slide, B or D-pad left goes back, X starts the show, Y ends it.", Presenter),
    };

    private static void Key(Profile p, ControllerButton button, string key) =>
        p.ShiftLayers[0].Mappings[button] = new ButtonMapping
        {
            SourceButton = button,
            Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = key }
        };

    private static void Mouse(Profile p, ControllerButton button, string mouse) =>
        p.ShiftLayers[0].Mappings[button] = new ButtonMapping
        {
            SourceButton = button,
            Target = new RemapTarget { Type = TargetType.MouseButton, MouseButton = mouse }
        };

    private static Profile Couch()
    {
        var p = Profile.CreateDefault("Couch mouse");
        p.LeftStick.Mode = StickMode.Mouse;
        p.LeftStick.MouseSpeed = 1000;
        p.LeftStick.PrecisionButton = ControllerButton.LeftTrigger;
        p.RightStick.Mode = StickMode.Scroll;
        p.RightStick.ScrollSpeed = 12;
        Mouse(p, ControllerButton.A, "Left Click");
        Mouse(p, ControllerButton.B, "Right Click");
        Mouse(p, ControllerButton.RightStickClick, "Middle Click");
        Key(p, ControllerButton.X, "Enter");
        Key(p, ControllerButton.Y, "Escape");
        Key(p, ControllerButton.DpadUp, "Up");
        Key(p, ControllerButton.DpadDown, "Down");
        Key(p, ControllerButton.DpadLeft, "Left");
        Key(p, ControllerButton.DpadRight, "Right");
        Key(p, ControllerButton.LeftBumper, "Back");
        Key(p, ControllerButton.RightBumper, "Tab");
        return p;
    }

    private static Profile Wasd()
    {
        var p = Profile.CreateDefault("WASD game");
        p.LeftStick.Mode = StickMode.Keys;
        p.LeftStick.UseKeyPreset(arrows: false);
        p.RightStick.Mode = StickMode.Mouse;
        p.RightStick.MouseSpeed = 1500;
        p.RightStick.PrecisionButton = ControllerButton.LeftBumper;
        Key(p, ControllerButton.A, "Space");
        Key(p, ControllerButton.B, "ControlKey");
        Key(p, ControllerButton.X, "E");
        Key(p, ControllerButton.Y, "Q");
        Key(p, ControllerButton.LeftStickClick, "ShiftKey");
        Key(p, ControllerButton.Menu, "Escape");
        Key(p, ControllerButton.View, "Tab");
        Key(p, ControllerButton.DpadUp, "1");
        Key(p, ControllerButton.DpadRight, "2");
        Key(p, ControllerButton.DpadDown, "3");
        Key(p, ControllerButton.DpadLeft, "4");
        Mouse(p, ControllerButton.RightTrigger, "Left Click");
        Mouse(p, ControllerButton.LeftTrigger, "Right Click");
        Key(p, ControllerButton.RightBumper, "R");
        return p;
    }

    private static Profile Presenter()
    {
        var p = Profile.CreateDefault("Presentation clicker");
        p.LeftStick.Mode = StickMode.Mouse;
        p.LeftStick.MouseSpeed = 700;
        Key(p, ControllerButton.A, "Right");
        Key(p, ControllerButton.DpadRight, "Right");
        Key(p, ControllerButton.RightBumper, "Right");
        Key(p, ControllerButton.B, "Left");
        Key(p, ControllerButton.DpadLeft, "Left");
        Key(p, ControllerButton.LeftBumper, "Left");
        Key(p, ControllerButton.X, "F5");
        Key(p, ControllerButton.Y, "Escape");
        return p;
    }
}
