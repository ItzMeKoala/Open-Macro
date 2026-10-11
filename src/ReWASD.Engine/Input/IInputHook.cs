namespace ReWASD.Engine.Input;

/// <summary>
/// Watches the real keyboard and mouse so they can be used as remapping sources, and swallows the keys and
/// buttons that have a mapping. Implemented per platform: low-level hooks on Windows, an event tap on macOS.
/// </summary>
public interface IInputHook : IDisposable
{
    /// <summary>True once the hook is installed and receiving events.</summary>
    bool IsInstalled { get; }

    /// <summary>When true (tests only), events injected by this or any process are processed instead of ignored.</summary>
    bool IncludeInjected { get; set; }

    /// <summary>Raised on a thread-pool thread when the panic hotkey is pressed.</summary>
    event Action? PanicRequested;

    /// <summary>Raised for every real key press/release (name, isDown), whether or not it is swallowed. Handlers must be quick.</summary>
    event Action<string, bool>? KeyEvent;

    /// <summary>Raised for every real mouse button press/release and wheel notch (name, isDown). Handlers must be quick.</summary>
    event Action<string, bool>? MouseEvent;

    bool Start();

    void Stop();

    /// <summary>Keys and mouse buttons (and recent wheel notches) currently down, by profile name.</summary>
    (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot();
}
