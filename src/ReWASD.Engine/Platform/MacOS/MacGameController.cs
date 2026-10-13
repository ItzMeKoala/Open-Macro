using System.Runtime.Versioning;
using ReWASD.Core.Models;
using ReWASD.Engine.Input;

namespace ReWASD.Engine.Platform.MacOS;

/// <summary>
/// Reads physical controllers through Apple's GameController framework: Xbox, DualShock 4, DualSense, Switch Pro and
/// MFi pads that macOS recognises. The framework is reached through the Objective-C runtime, so no native helper
/// has to be built. Polling has to run under an autorelease pool because the objects it returns are autoreleased.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacGameController : IPhysicalController
{
    private readonly IntPtr _controllerClass;
    private readonly bool _available;

    public MacGameController()
    {
        try
        {
            _available = ObjC.LoadFramework("Foundation") && ObjC.LoadFramework("GameController");
            _controllerClass = _available ? ObjC.Class("GCController") : IntPtr.Zero;
            _available = _controllerClass != IntPtr.Zero;

            // Without this, macOS stops delivering controller input as soon as another app (the game) is in front
            if (_available && ObjC.ClassRespondsTo(_controllerClass, "setShouldMonitorBackgroundEvents:"))
                ObjC.SetBool(_controllerClass, "setShouldMonitorBackgroundEvents:", true);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _available = false;
        }
    }

    /// <summary>True if the GameController framework could be loaded.</summary>
    public bool IsAvailable => _available;

    // macOS shows no virtual pad to ignore, so this is accepted and unused
    public int ExcludedUserIndex { get; set; } = -1;

    public int ActiveUserIndex { get; private set; } = -1;

    private IntPtr _activeController;

    // The input loop, the live display and the tester can all ask for a reading; the Objective-C calls and the active-controller
    // bookkeeping are not meant to run at the same time on several threads
    private readonly object _sync = new();

    /// <summary>
    /// Runs <paramref name="action"/> with the controller currently in use (the one read, or the first when none has been yet),
    /// under the lock and an autorelease pool. False when no controller is connected.
    /// </summary>
    internal bool WithActiveController(Action<IntPtr> action)
    {
        if (!_available) return false;
        lock (_sync)
        {
            using var scope = new PoolScope();
            var controllers = ExtendedControllers();
            if (controllers.Count == 0) return false;
            action(controllers.Contains(_activeController) ? _activeController : controllers[0]);
            return true;
        }
    }

    public IReadOnlyList<int> ConnectedSlots()
    {
        var slots = new List<int>();
        lock (_sync)
        {
            using var scope = new PoolScope();
            int count = ExtendedControllers().Count;
            for (int i = 0; i < count; i++) slots.Add(i);
        }
        return slots;
    }

    public string DescribeSlot(int slot)
    {
        lock (_sync)
        {
            using var scope = new PoolScope();
            var controllers = ExtendedControllers();
            if (slot < 0 || slot >= controllers.Count) return $"Controller {slot + 1}";
            return ObjC.GetString(controllers[slot], "vendorName") ?? $"Controller {slot + 1}";
        }
    }

    public PhysicalState? Read()
    {
        if (!_available) return null;
        lock (_sync) return ReadLocked();
    }

    private PhysicalState? ReadLocked()
    {
        using var scope = new PoolScope();
        var controllers = ExtendedControllers();
        if (controllers.Count == 0)
        {
            ActiveUserIndex = -1;
            _activeController = IntPtr.Zero;
            return null;
        }

        // Keep the current controller while it stays connected so a second pad can't steal focus mid-game
        int index = controllers.IndexOf(_activeController);
        if (index < 0)
        {
            index = 0;
            _activeController = controllers[0];
        }
        ActiveUserIndex = index;

        var pad = ObjC.Get(_activeController, "extendedGamepad");
        return pad == IntPtr.Zero ? null : ReadPad(pad);
    }

    /// <summary>Controllers that expose the full gamepad layout (the Siri remote and similar are left out).</summary>
    private List<IntPtr> ExtendedControllers()
    {
        var result = new List<IntPtr>();
        if (!_available) return result;

        var array = ObjC.Get(_controllerClass, "controllers");
        int count = ObjC.Count(array);
        for (int i = 0; i < count; i++)
        {
            var controller = ObjC.GetAt(array, "objectAtIndex:", i);
            if (controller != IntPtr.Zero && ObjC.Get(controller, "extendedGamepad") != IntPtr.Zero) result.Add(controller);
        }
        return result;
    }

    private static PhysicalState ReadPad(IntPtr pad)
    {
        var buttons = new HashSet<ControllerButton>();

        void Button(string property, ControllerButton button)
        {
            var b = ObjC.Get(pad, property);
            if (b != IntPtr.Zero && ObjC.GetBool(b, "isPressed")) buttons.Add(button);
        }

        Button("buttonA", ControllerButton.A);
        Button("buttonB", ControllerButton.B);
        Button("buttonX", ControllerButton.X);
        Button("buttonY", ControllerButton.Y);
        Button("leftShoulder", ControllerButton.LeftBumper);
        Button("rightShoulder", ControllerButton.RightBumper);
        Button("leftThumbstickButton", ControllerButton.LeftStickClick);
        Button("rightThumbstickButton", ControllerButton.RightStickClick);
        Button("buttonMenu", ControllerButton.Menu);
        Button("buttonOptions", ControllerButton.View);
        Button("buttonHome", ControllerButton.Guide);

        var dpad = ObjC.Get(pad, "dpad");
        if (dpad != IntPtr.Zero)
        {
            void Dir(string property, ControllerButton button)
            {
                var b = ObjC.Get(dpad, property);
                if (b != IntPtr.Zero && ObjC.GetBool(b, "isPressed")) buttons.Add(button);
            }

            Dir("up", ControllerButton.DpadUp);
            Dir("down", ControllerButton.DpadDown);
            Dir("left", ControllerButton.DpadLeft);
            Dir("right", ControllerButton.DpadRight);
        }

        var (lx, ly) = Stick(ObjC.Get(pad, "leftThumbstick"));
        var (rx, ry) = Stick(ObjC.Get(pad, "rightThumbstick"));
        double lt = Trigger(ObjC.Get(pad, "leftTrigger"));
        double rt = Trigger(ObjC.Get(pad, "rightTrigger"));

        return PhysicalStateBuilder.Build(buttons, lx, ly, rx, ry, lt, rt);
    }

    private static (double X, double Y) Stick(IntPtr stick)
    {
        if (stick == IntPtr.Zero) return (0, 0);
        double x = ObjC.GetFloat(ObjC.Get(stick, "xAxis"), "value");
        double y = ObjC.GetFloat(ObjC.Get(stick, "yAxis"), "value");
        return (Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1)); // GameController already reports up as positive
    }

    private static double Trigger(IntPtr trigger) =>
        trigger == IntPtr.Zero ? 0 : Math.Clamp(ObjC.GetFloat(trigger, "value"), 0, 1);

    /// <summary>Drains the objects the Objective-C calls autoreleased on this thread.</summary>
    private readonly struct PoolScope : IDisposable
    {
        private readonly IntPtr _pool;
        public PoolScope() => _pool = ObjC.objc_autoreleasePoolPush();
        public void Dispose() => ObjC.objc_autoreleasePoolPop(_pool);
    }
}
