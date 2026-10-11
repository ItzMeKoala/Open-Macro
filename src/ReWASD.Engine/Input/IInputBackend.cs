namespace ReWASD.Engine.Input;

public enum MouseButtonKind
{
    Left,
    Right,
    Middle,
    X1,
    X2
}

/// <summary>Low-level sink for synthetic keyboard/mouse events.</summary>
public interface IInputBackend
{
    void KeyDown(ushort virtualKey);
    void KeyUp(ushort virtualKey);
    void MouseDown(MouseButtonKind button);
    void MouseUp(MouseButtonKind button);
    void MouseWheel(int delta);

    /// <summary>Moves the cursor by a relative number of pixels (positive x = right, positive y = down).</summary>
    void MouseMove(int dx, int dy);
}
