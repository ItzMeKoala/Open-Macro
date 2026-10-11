using System.Runtime.Versioning;
using ReWASD.Engine.Input;

namespace ReWASD.Engine.Platform.MacOS;

/// <summary>
/// Injects keyboard and mouse events with Quartz Event Services. Needs the Accessibility permission. Every event
/// carries <see cref="InjectedMarker"/> so <see cref="MacInputHook"/> never reads this app's own output back in.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacInputBackend : IInputBackend, IDisposable
{
    /// <summary>Stamped into every injected event's source user-data field ("REWA").</summary>
    public const long InjectedMarker = 0x52455741;

    private const int LinesPerWheelNotch = 3; // the same distance a Windows wheel notch scrolls by default
    private const double DoubleClickSeconds = 0.5;

    private readonly object _gate = new();
    private readonly IntPtr _source = MacNative.CGEventSourceCreate(MacNative.EventSourceStateHidSystem);
    private readonly HashSet<ushort> _heldModifiers = new();
    private readonly HashSet<MouseButtonKind> _heldButtons = new();
    private readonly Dictionary<MouseButtonKind, (long Ticks, int Count)> _lastClick = new();

    public void KeyDown(ushort virtualKey) => SendKey(virtualKey, true);
    public void KeyUp(ushort virtualKey) => SendKey(virtualKey, false);

    private void SendKey(ushort virtualKey, bool down)
    {
        if (!MacKeyCodes.TryGetMacKeyCode(virtualKey, out ushort code)) return; // a key this keyboard doesn't have

        lock (_gate)
        {
            if (MacKeyCodes.IsModifier(code))
            {
                if (down) _heldModifiers.Add(code);
                else _heldModifiers.Remove(code);
            }

            var evt = MacNative.CGEventCreateKeyboardEvent(_source, code, down);
            Post(evt, withFlags: true);
        }
    }

    public void MouseDown(MouseButtonKind button) => SendMouseButton(button, true);
    public void MouseUp(MouseButtonKind button) => SendMouseButton(button, false);

    private void SendMouseButton(MouseButtonKind button, bool down)
    {
        lock (_gate)
        {
            var position = CurrentPosition();
            int clickCount = 1;

            if (down)
            {
                _heldButtons.Add(button);
                long now = Environment.TickCount64;
                if (_lastClick.TryGetValue(button, out var last) && now - last.Ticks <= DoubleClickSeconds * 1000) clickCount = last.Count + 1;
                _lastClick[button] = (now, clickCount);
            }
            else
            {
                _heldButtons.Remove(button);
                if (_lastClick.TryGetValue(button, out var last)) clickCount = last.Count;
            }

            var (type, number) = button switch
            {
                MouseButtonKind.Left => (down ? MacNative.LeftMouseDown : MacNative.LeftMouseUp, 0u),
                MouseButtonKind.Right => (down ? MacNative.RightMouseDown : MacNative.RightMouseUp, 1u),
                MouseButtonKind.Middle => (down ? MacNative.OtherMouseDown : MacNative.OtherMouseUp, 2u),
                MouseButtonKind.X1 => (down ? MacNative.OtherMouseDown : MacNative.OtherMouseUp, 3u),
                _ => (down ? MacNative.OtherMouseDown : MacNative.OtherMouseUp, 4u)
            };

            var evt = MacNative.CGEventCreateMouseEvent(_source, type, position, number);
            if (evt == IntPtr.Zero) return;
            MacNative.CGEventSetIntegerValueField(evt, MacNative.FieldMouseClickState, clickCount);
            Post(evt, withFlags: true);
        }
    }

    public void MouseWheel(int delta)
    {
        int lines = (int)Math.Round(delta / (double)KeyMap.WheelNotch * LinesPerWheelNotch);
        if (lines == 0) lines = Math.Sign(delta);
        if (lines == 0) return;

        lock (_gate)
        {
            var evt = MacNative.CGEventCreateScrollWheelEvent2(_source, MacNative.ScrollEventUnitLine, 1, lines, 0, 0);
            Post(evt, withFlags: true);
        }
    }

    public void MouseMove(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;

        lock (_gate)
        {
            var from = CurrentPosition();
            var to = ClampToDisplays(new CGPoint(from.X + dx, from.Y + dy));

            // While a button is held the system expects drag events, otherwise apps ignore the movement
            var (type, number) =
                _heldButtons.Contains(MouseButtonKind.Left) ? (MacNative.LeftMouseDragged, 0u) :
                _heldButtons.Contains(MouseButtonKind.Right) ? (MacNative.RightMouseDragged, 1u) :
                _heldButtons.Count > 0 ? (MacNative.OtherMouseDragged, 2u) :
                (MacNative.MouseMoved, 0u);

            var evt = MacNative.CGEventCreateMouseEvent(_source, type, to, number);
            if (evt == IntPtr.Zero) return;
            MacNative.CGEventSetIntegerValueField(evt, MacNative.FieldMouseDeltaX, dx);
            MacNative.CGEventSetIntegerValueField(evt, MacNative.FieldMouseDeltaY, dy);
            Post(evt, withFlags: true);
        }
    }

    private static CGPoint CurrentPosition()
    {
        var probe = MacNative.CGEventCreate(IntPtr.Zero);
        if (probe == IntPtr.Zero) return default;
        try
        {
            return MacNative.CGEventGetLocation(probe);
        }
        finally
        {
            MacNative.CFRelease(probe);
        }
    }

    /// <summary>Keeps the pointer inside the desktop (the union of all displays) so it can't be pushed off screen.</summary>
    private static CGPoint ClampToDisplays(CGPoint p)
    {
        var ids = new uint[16];
        if (MacNative.CGGetActiveDisplayList((uint)ids.Length, ids, out uint count) != 0 || count == 0) return p;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (int i = 0; i < count; i++)
        {
            var b = MacNative.CGDisplayBounds(ids[i]);
            minX = Math.Min(minX, b.Origin.X);
            minY = Math.Min(minY, b.Origin.Y);
            maxX = Math.Max(maxX, b.Origin.X + b.Size.Width);
            maxY = Math.Max(maxY, b.Origin.Y + b.Size.Height);
        }

        return new CGPoint(Math.Clamp(p.X, minX, maxX - 1), Math.Clamp(p.Y, minY, maxY - 1));
    }

    private ulong HeldFlags()
    {
        ulong flags = 0;
        foreach (var code in _heldModifiers)
        {
            flags |= code switch
            {
                MacKeyCodes.LeftShift or MacKeyCodes.RightShift => MacNative.FlagShift,
                MacKeyCodes.LeftControl or MacKeyCodes.RightControl => MacNative.FlagControl,
                MacKeyCodes.LeftOption or MacKeyCodes.RightOption => MacNative.FlagAlternate,
                MacKeyCodes.LeftCommand or MacKeyCodes.RightCommand => MacNative.FlagCommand,
                _ => 0UL
            };
        }
        return flags;
    }

    private void Post(IntPtr evt, bool withFlags)
    {
        if (evt == IntPtr.Zero) return;
        try
        {
            if (withFlags) MacNative.CGEventSetFlags(evt, HeldFlags());
            MacNative.CGEventSetIntegerValueField(evt, MacNative.FieldSourceUserData, InjectedMarker);
            MacNative.CGEventPost(MacNative.HidEventTap, evt);
        }
        finally
        {
            MacNative.CFRelease(evt);
        }
    }

    public void Dispose()
    {
        if (_source != IntPtr.Zero) MacNative.CFRelease(_source);
    }
}
