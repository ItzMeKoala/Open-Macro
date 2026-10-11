using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.Input;

/// <summary>Turns each frame's active keys/buttons into press and release events by diffing against the previous frame.</summary>
public class KeyboardMouseInjector
{
    private readonly IInputBackend _backend;
    private readonly HashSet<ushort> _downKeys = new();
    private readonly HashSet<MouseButtonKind> _downButtons = new();
    private readonly HashSet<string> _activeWheels = new(StringComparer.OrdinalIgnoreCase);

    public KeyboardMouseInjector(IInputBackend backend)
    {
        _backend = backend;
    }

    public void Apply(RemappedState state)
    {
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

    /// <summary>Releases everything currently held; call when remapping stops so keys never stick.</summary>
    public void ReleaseAll()
    {
        foreach (var vk in _downKeys) _backend.KeyUp(vk);
        foreach (var b in _downButtons) _backend.MouseUp(b);
        _downKeys.Clear();
        _downButtons.Clear();
        _activeWheels.Clear();
    }
}
