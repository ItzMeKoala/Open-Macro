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
}
