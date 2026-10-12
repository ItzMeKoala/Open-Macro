using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.Input;

/// <summary>Turns each frame's active keys/buttons into press and release events by diffing against the previous frame.</summary>
public class KeyboardMouseInjector
{
    private readonly IInputBackend _backend;
    private readonly HashSet<ushort> _downKeys = new();
    private readonly HashSet<MouseButtonKind> _downButtons = new();
    private readonly HashSet<string> _activeWheels = new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<long> _clockMs;
    private long _lastFrameMs = -1;
    private double _moveRemainderX, _moveRemainderY, _scrollRemainder;

    /// <param name="clockMs">Milliseconds clock for turning stick speeds into distances; tests pass their own.</param>
    public KeyboardMouseInjector(IInputBackend backend, Func<long>? clockMs = null)
    {
        _backend = backend;
        _clockMs = clockMs ?? (() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000 / System.Diagnostics.Stopwatch.Frequency);
    }

    public void Apply(RemappedState state)
    {
        MoveAndScroll(state);

        var wantedKeys = new HashSet<ushort>();
        foreach (var name in state.ActiveKeyboardKeys)
        {
            if (KeyMap.TryGetVirtualKey(name, out var vk)) wantedKeys.Add(vk);
        }

        var wantedButtons = new HashSet<MouseButtonKind>();
        var wantedWheels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in state.ActiveMouseButtons)
        {
            if (!KeyMap.TryResolveMouse(name, out var button, out int wheel)) continue;
            if (button is { } b) wantedButtons.Add(b);
            else if (wheel != 0) wantedWheels.Add(name);
        }

        foreach (var vk in wantedKeys)
        {
            if (_downKeys.Add(vk)) _backend.KeyDown(vk);
        }
        foreach (var vk in _downKeys.Where(k => !wantedKeys.Contains(k)).ToList())
        {
            _downKeys.Remove(vk);
            _backend.KeyUp(vk);
        }

        foreach (var b in wantedButtons)
        {
            if (_downButtons.Add(b)) _backend.MouseDown(b);
        }
        foreach (var b in _downButtons.Where(x => !wantedButtons.Contains(x)).ToList())
        {
            _downButtons.Remove(b);
            _backend.MouseUp(b);
        }

        // Wheel directions fire one notch on each press edge
        foreach (var name in wantedWheels)
        {
            if (_activeWheels.Add(name) && KeyMap.TryResolveMouse(name, out _, out int delta)) _backend.MouseWheel(delta);
        }
        _activeWheels.IntersectWith(wantedWheels);
    }

    // Sticks give speeds, so the distance moved is speed x the time since the last frame. Fractions of a pixel carry over
    // to the next frame, otherwise a gentle push would round to nothing and the cursor would never move.
    private void MoveAndScroll(RemappedState state)
    {
        long now = _clockMs();
        double dt = _lastFrameMs < 0 ? 0 : Math.Clamp((now - _lastFrameMs) / 1000.0, 0, 0.1);
        _lastFrameMs = now;

        if (state.MouseVelocityX == 0 && state.MouseVelocityY == 0)
        {
            _moveRemainderX = _moveRemainderY = 0;
        }
        else
        {
            _moveRemainderX += state.MouseVelocityX * dt;
            _moveRemainderY += state.MouseVelocityY * dt;
            int dx = (int)Math.Truncate(_moveRemainderX);
            int dy = (int)Math.Truncate(_moveRemainderY);
            _moveRemainderX -= dx;
            _moveRemainderY -= dy;
            if (dx != 0 || dy != 0) _backend.MouseMove(dx, dy);
        }

        if (state.ScrollVelocity == 0)
        {
            _scrollRemainder = 0;
        }
        else
        {
            _scrollRemainder += state.ScrollVelocity * dt;
            int notches = (int)Math.Truncate(_scrollRemainder);
            _scrollRemainder -= notches;
            if (notches != 0) _backend.MouseWheel(notches * KeyMap.WheelNotch);
        }
    }

    /// <summary>Releases everything currently held; call when remapping stops so keys never stick.</summary>
    public void ReleaseAll()
    {
        foreach (var vk in _downKeys) _backend.KeyUp(vk);
        foreach (var b in _downButtons) _backend.MouseUp(b);
        _downKeys.Clear();
        _downButtons.Clear();
        _activeWheels.Clear();
        _moveRemainderX = _moveRemainderY = _scrollRemainder = 0;
        _lastFrameMs = -1;
    }
}
