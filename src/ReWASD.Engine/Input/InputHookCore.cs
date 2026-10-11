namespace ReWASD.Engine.Input;

/// <summary>
/// The platform-neutral half of an input hook: which keys and buttons are down, which of them are being
/// swallowed, and the panic combination. A platform hook feeds it real (non-injected) events and swallows
/// whatever it says to swallow.
///
/// Keys arrive as Windows virtual-key codes, the identifiers <see cref="KeyMap"/> uses everywhere; a platform
/// whose native codes differ translates them first. Only inputs the engine reports as mapped are swallowed.
/// Ctrl+Alt+Shift+F12 is a panic key that is never forwarded to the engine.
/// </summary>
internal sealed class InputHookCore
{
    private const int WheelHoldMs = 60;
    private const ushort VkF12 = 0x7B;

    private readonly Func<string, bool> _isKeySuppressed;
    private readonly Func<string, bool> _isMouseSuppressed;
    private readonly Action _panic;
    private readonly Action<string, bool> _keyEvent;
    private readonly Action<string, bool> _mouseEvent;
    private readonly Func<long> _now;

    private readonly object _gate = new();
    private readonly HashSet<string> _pressedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pressedMouse = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _wheelUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _swallowedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _swallowedMouse = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool[] _modifiers = new bool[3]; // ctrl, alt, shift

    public InputHookCore(Func<string, bool> isKeySuppressed, Func<string, bool> isMouseSuppressed,
        Action panic, Action<string, bool> keyEvent, Action<string, bool> mouseEvent, Func<long>? now = null)
    {
        _isKeySuppressed = isKeySuppressed;
        _isMouseSuppressed = isMouseSuppressed;
        _panic = panic;
        _keyEvent = keyEvent;
        _mouseEvent = mouseEvent;
        _now = now ?? (() => Environment.TickCount64);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _pressedKeys.Clear();
            _pressedMouse.Clear();
            _wheelUntil.Clear();
            _swallowedKeys.Clear();
            _swallowedMouse.Clear();
            Array.Clear(_modifiers);
        }
    }

    public (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot()
    {
        lock (_gate)
        {
            long now = _now();
            foreach (var expired in _wheelUntil.Where(w => w.Value <= now).Select(w => w.Key).ToList())
            {
                _wheelUntil.Remove(expired);
                _pressedMouse.Remove(expired);
            }

            return (_pressedKeys.ToArray(), _pressedMouse.ToArray());
        }
    }

    /// <summary>Returns true if the event should be swallowed.</summary>
    public bool HandleKey(ushort vk, bool down)
    {
        // Track modifiers for the panic key before anything else
        switch (vk)
        {
            case 0x10 or 0xA0 or 0xA1: lock (_gate) _modifiers[2] = down; break;
            case 0x11 or 0xA2 or 0xA3: lock (_gate) _modifiers[0] = down; break;
            case 0x12 or 0xA4 or 0xA5: lock (_gate) _modifiers[1] = down; break;
        }

        if (vk == VkF12)
        {
            bool panic;
            lock (_gate) panic = _modifiers[0] && _modifiers[1] && _modifiers[2];
            if (panic)
            {
                if (down) ThreadPool.QueueUserWorkItem(_ => _panic());
                return true; // the panic combo itself is never passed on
            }
        }

        if (!KeyMap.TryGetKeyName(vk, out var name)) return false;

        _keyEvent(name, down);

        lock (_gate)
        {
            if (down)
            {
                _pressedKeys.Add(name);
                if (_swallowedKeys.Contains(name)) return true;       // auto-repeat of a key we are already swallowing
                if (!_isKeySuppressed(name)) return false;
                _swallowedKeys.Add(name);
                return true;
            }

            _pressedKeys.Remove(name);
            return _swallowedKeys.Remove(name); // a swallowed key's release is swallowed too, so nothing is left stuck
        }
    }

    /// <summary>A mouse button (profile name) went down or up. Returns true if the event should be swallowed.</summary>
    public bool HandleMouseButton(string name, bool down)
    {
        _mouseEvent(name, down);

        lock (_gate)
        {
            if (down)
            {
                _pressedMouse.Add(name);
                if (_swallowedMouse.Contains(name)) return true;
                if (!_isMouseSuppressed(name)) return false;
                _swallowedMouse.Add(name);
                return true;
            }

            _pressedMouse.Remove(name);
            return _swallowedMouse.Remove(name);
        }
    }

    /// <summary>True while the press of this mouse button is being swallowed (so platform events that follow it can be too).</summary>
    public bool IsMouseSwallowed(string name)
    {
        lock (_gate) return _swallowedMouse.Contains(name);
    }

    /// <summary>One wheel notch ("Wheel Up" or "Wheel Down"). Returns true if it should be swallowed.</summary>
    public bool HandleWheel(string name)
    {
        _mouseEvent(name, true);
        lock (_gate)
        {
            if (!_isMouseSuppressed(name)) return false;
            _pressedMouse.Add(name);
            _wheelUntil[name] = _now() + WheelHoldMs;
        }
        return true;
    }
}
