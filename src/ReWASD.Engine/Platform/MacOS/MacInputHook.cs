using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ReWASD.Engine.Input;

namespace ReWASD.Engine.Platform.MacOS;

/// <summary>
/// Watches the real keyboard and mouse with a Quartz event tap and swallows the inputs that have a mapping.
/// Needs the Input Monitoring and Accessibility permissions; without them <see cref="Start"/> returns false and
/// <see cref="FailureReason"/> says what to grant. Events injected by <see cref="MacInputBackend"/> are ignored.
///
/// Safety: only inputs the engine reports as mapped are swallowed, and Ctrl+Option+Shift+F12 is a panic key
/// (see <see cref="InputHookCore"/>). A modifier that is swallowed is also removed from the flags of the events
/// that follow, because macOS keeps modifier state apart from the key events themselves.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacInputHook : IInputHook
{
    private const ulong EventMask =
        (1UL << (int)MacNative.LeftMouseDown) | (1UL << (int)MacNative.LeftMouseUp)
        | (1UL << (int)MacNative.RightMouseDown) | (1UL << (int)MacNative.RightMouseUp)
        | (1UL << (int)MacNative.LeftMouseDragged) | (1UL << (int)MacNative.RightMouseDragged)
        | (1UL << (int)MacNative.KeyDown) | (1UL << (int)MacNative.KeyUp) | (1UL << (int)MacNative.FlagsChanged)
        | (1UL << (int)MacNative.ScrollWheel)
        | (1UL << (int)MacNative.OtherMouseDown) | (1UL << (int)MacNative.OtherMouseUp) | (1UL << (int)MacNative.OtherMouseDragged);

    private readonly InputHookCore _core;
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly object _modifierGate = new();
    private readonly HashSet<ushort> _downModifiers = new();    // modifier key codes currently held
    private readonly HashSet<ushort> _swallowedModifiers = new();
    private ulong _swallowedFlags;

    internal ulong SwallowedModifierFlags
    {
        get
        {
            lock (_modifierGate)
            {
                ulong flags = _swallowedFlags;
                if ((flags & MacNative.FlagShift) != 0) flags |= MacNative.DeviceLShift | MacNative.DeviceRShift;
                if ((flags & MacNative.FlagControl) != 0) flags |= MacNative.DeviceLControl | MacNative.DeviceRControl;
                if ((flags & MacNative.FlagAlternate) != 0) flags |= MacNative.DeviceLAlt | MacNative.DeviceRAlt;
                if ((flags & MacNative.FlagCommand) != 0) flags |= MacNative.DeviceLCommand | MacNative.DeviceRCommand;
                return flags;
            }
        }
    }

    // The delegate is a field so the GC cannot collect it while the system still holds the pointer
    private MacNative.EventTapCallback? _callback;
    private IntPtr _tap;
    private Thread? _thread;
    private volatile bool _running;

    public MacInputHook(Func<string, bool> isKeySuppressed, Func<string, bool> isMouseSuppressed)
    {
        _core = new InputHookCore(isKeySuppressed, isMouseSuppressed,
            () => PanicRequested?.Invoke(),
            (name, down) => KeyEvent?.Invoke(name, down),
            (name, down) => MouseEvent?.Invoke(name, down));
    }

    public bool IncludeInjected { get; set; }
    public bool IsInstalled { get; private set; }

    /// <summary>Why the last <see cref="Start"/> failed, in words for the person using the app.</summary>
    public string? FailureReason { get; private set; }

    public event Action? PanicRequested;
    public event Action<string, bool>? KeyEvent;
    public event Action<string, bool>? MouseEvent;

    public bool Start()
    {
        if (_running) return IsInstalled;

        FailureReason = null;
        using var ready = new ManualResetEventSlim(false);
        _running = true;
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "ReWASD input tap" };
        _thread.Start();

        ready.Wait(TimeSpan.FromSeconds(3));
        if (!IsInstalled) Stop();
        return IsInstalled;
    }

    public void Stop()
    {
        if (!_running && _thread == null) return;
        _running = false;
        _thread?.Join(2000);
        _thread = null;
        IsInstalled = false;

        _core.Reset();
        lock (_modifierGate)
        {
            _downModifiers.Clear();
            _swallowedModifiers.Clear();
            _swallowedFlags = 0;
        }
    }

    public (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot() => _core.Snapshot();

    private void Run(ManualResetEventSlim ready)
    {
        IntPtr tap = IntPtr.Zero, source = IntPtr.Zero, runLoop = IntPtr.Zero;
        try
        {
            _callback = OnEvent;
            tap = _tap = MacNative.CGEventTapCreate(MacNative.SessionEventTap, MacNative.HeadInsertEventTap, MacNative.EventTapOptionDefault,
                EventMask, _callback, IntPtr.Zero);
            if (tap == IntPtr.Zero)
            {
                FailureReason = MacPermissions.DescribeMissing()
                                ?? "macOS would not let this app watch the keyboard and mouse.";
                ready.Set();
                return;
            }

            source = MacNative.CFMachPortCreateRunLoopSource(IntPtr.Zero, tap, 0);
            runLoop = MacNative.CFRunLoopGetCurrent();
            MacNative.CFRunLoopAddSource(runLoop, source, MacNative.DefaultRunLoopMode);
            MacNative.CGEventTapEnable(tap, true);

            IsInstalled = true;
            ready.Set();

            // Run in short slices so Stop() never has to interrupt the loop
            while (_running) MacNative.CFRunLoopRunInMode(MacNative.DefaultRunLoopMode, 0.2, false);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            FailureReason = "The macOS event APIs are unavailable.";
        }
        finally
        {
            IsInstalled = false;
            _tap = IntPtr.Zero;
            if (tap != IntPtr.Zero) MacNative.CGEventTapEnable(tap, false);
            if (source != IntPtr.Zero)
            {
                MacNative.CFRunLoopRemoveSource(runLoop, source, MacNative.DefaultRunLoopMode);
                MacNative.CFRelease(source);
            }
            if (tap != IntPtr.Zero)
            {
                MacNative.CFMachPortInvalidate(tap);
                MacNative.CFRelease(tap);
            }
            ready.Set();
        }
    }

    private IntPtr OnEvent(IntPtr proxy, uint type, IntPtr evt, IntPtr userInfo)
    {
        try
        {
            if (type is MacNative.TapDisabledByTimeout or MacNative.TapDisabledByUserInput)
            {
                // The system turns a slow or interrupted tap off; switch it back on so mapped inputs keep working
                if (_running && _tap != IntPtr.Zero) MacNative.CGEventTapEnable(_tap, true);
                return evt;
            }

            if (!IncludeInjected && IsOwnEvent(evt)) return evt;

            if (Handle(type, evt)) return IntPtr.Zero; // swallowed
            StripSwallowedModifiers(evt);
        }
        catch (Exception)
        {
            // Never let a bug in our handling block the user's keyboard or mouse
        }
        return evt;
    }

    private bool IsOwnEvent(IntPtr evt) =>
        MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldSourceUserData) == MacInputBackend.InjectedMarker
        || MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldSourceUnixProcessId) == _ownProcessId;

    /// <summary>Returns true if the event should be swallowed.</summary>
    private bool Handle(uint type, IntPtr evt)
    {
        switch (type)
        {
            case MacNative.KeyDown or MacNative.KeyUp:
            {
                ushort code = (ushort)MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldKeyboardKeycode);
                if (!MacKeyCodes.TryGetVirtualKey(code, out ushort vk)) return false;
                return _core.HandleKey(vk, type == MacNative.KeyDown);
            }

            case MacNative.FlagsChanged:
                return HandleModifier((ushort)MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldKeyboardKeycode),
                    MacNative.CGEventGetFlags(evt));

            case MacNative.LeftMouseDown or MacNative.LeftMouseUp:
                return _core.HandleMouseButton("Left Click", type == MacNative.LeftMouseDown);
            case MacNative.RightMouseDown or MacNative.RightMouseUp:
                return _core.HandleMouseButton("Right Click", type == MacNative.RightMouseDown);

            case MacNative.OtherMouseDown or MacNative.OtherMouseUp:
            {
                string? name = OtherButtonName(evt);
                return name != null && _core.HandleMouseButton(name, type == MacNative.OtherMouseDown);
            }

            // Movement while a swallowed button is held would otherwise reach apps as a drag that never began
            case MacNative.LeftMouseDragged: return _core.IsMouseSwallowed("Left Click");
            case MacNative.RightMouseDragged: return _core.IsMouseSwallowed("Right Click");
            case MacNative.OtherMouseDragged:
            {
                string? name = OtherButtonName(evt);
                return name != null && _core.IsMouseSwallowed(name);
            }

            case MacNative.ScrollWheel:
            {
                long delta = MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldScrollDeltaAxis1);
                if (delta == 0) delta = MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldScrollPointDeltaAxis1);
                if (delta == 0) return false; // horizontal scrolling is not a mappable source
                return _core.HandleWheel(delta > 0 ? "Wheel Up" : "Wheel Down");
            }

            default:
                return false;
        }
    }

    private static string? OtherButtonName(IntPtr evt) =>
        MacNative.CGEventGetIntegerValueField(evt, MacNative.FieldMouseButtonNumber) switch
        {
            2 => "Middle Click",
            3 => "Mouse Button 4",
            4 => "Mouse Button 5",
            _ => null
        };

    /// <summary>Modifier presses arrive as flag changes: the key code says which key, the flags say whether it went down.</summary>
    private bool HandleModifier(ushort code, ulong flags)
    {
        ulong deviceBit, familyBit;
        switch (code)
        {
            case MacKeyCodes.LeftShift: (deviceBit, familyBit) = (MacNative.DeviceLShift, MacNative.FlagShift); break;
            case MacKeyCodes.RightShift: (deviceBit, familyBit) = (MacNative.DeviceRShift, MacNative.FlagShift); break;
            case MacKeyCodes.LeftControl: (deviceBit, familyBit) = (MacNative.DeviceLControl, MacNative.FlagControl); break;
            case MacKeyCodes.RightControl: (deviceBit, familyBit) = (MacNative.DeviceRControl, MacNative.FlagControl); break;
            case MacKeyCodes.LeftOption: (deviceBit, familyBit) = (MacNative.DeviceLAlt, MacNative.FlagAlternate); break;
            case MacKeyCodes.RightOption: (deviceBit, familyBit) = (MacNative.DeviceRAlt, MacNative.FlagAlternate); break;
            case MacKeyCodes.LeftCommand: (deviceBit, familyBit) = (MacNative.DeviceLCommand, MacNative.FlagCommand); break;
            case MacKeyCodes.RightCommand: (deviceBit, familyBit) = (MacNative.DeviceRCommand, MacNative.FlagCommand); break;
            default: return false; // Caps Lock and Fn are not sources
        }

        if (!MacKeyCodes.TryGetVirtualKey(code, out ushort vk)) return false;

        bool down;
        lock (_modifierGate)
        {
            // The side-specific bit is the truth; if the system doesn't report it, fall back to toggling
            bool hasDeviceBits = (flags & (MacNative.DeviceLShift | MacNative.DeviceRShift | MacNative.DeviceLControl
                                           | MacNative.DeviceRControl | MacNative.DeviceLAlt | MacNative.DeviceRAlt
                                           | MacNative.DeviceLCommand | MacNative.DeviceRCommand)) != 0;
            down = hasDeviceBits ? (flags & deviceBit) != 0 : (flags & familyBit) != 0 && !_downModifiers.Contains(code);

            if (down) _downModifiers.Add(code);
            else _downModifiers.Remove(code);
        }

        bool swallow = _core.HandleKey(vk, down);

        lock (_modifierGate)
        {
            if (down)
            {
                if (swallow) _swallowedModifiers.Add(code);
            }
            else
            {
                _swallowedModifiers.Remove(code); // the core already swallows the release of a swallowed key
            }

            _swallowedFlags = 0;
            foreach (var swallowed in _swallowedModifiers)
            {
                _swallowedFlags |= swallowed switch
                {
                    MacKeyCodes.LeftShift or MacKeyCodes.RightShift => MacNative.FlagShift,
                    MacKeyCodes.LeftControl or MacKeyCodes.RightControl => MacNative.FlagControl,
                    MacKeyCodes.LeftCommand or MacKeyCodes.RightCommand => MacNative.FlagCommand,
                    _ => MacNative.FlagAlternate
                };
            }
        }

        return swallow;
    }

    private void StripSwallowedModifiers(IntPtr evt)
    {
        ulong strip;
        lock (_modifierGate) strip = _swallowedFlags;
        if (strip == 0) return;

        ulong flags = MacNative.CGEventGetFlags(evt);
        if ((flags & strip) != 0) MacNative.CGEventSetFlags(evt, flags & ~strip);
    }

    public void Dispose() => Stop();
}
