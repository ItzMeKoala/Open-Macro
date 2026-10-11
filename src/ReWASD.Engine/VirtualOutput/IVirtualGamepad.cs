using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.VirtualOutput;

/// <summary>A virtual controller that games can see. Windows backs it with the ViGEmBus driver; macOS has none.</summary>
public interface IVirtualGamepad : IDisposable
{
    /// <summary>True when the system can present a virtual controller at all (the driver is installed).</summary>
    bool IsDriverAvailable { get; }

    bool IsConnected { get; }

    /// <summary>The type currently connected, or null.</summary>
    ControllerType? ConnectedType { get; }

    void Initialize();

    /// <summary>Connects a virtual pad of the given type, replacing any that is connected. Returns false if it could not.</summary>
    bool Connect(ControllerType type);

    void DisconnectVirtual();

    void SendReport(RemappedState state);
}

/// <summary>
/// Which controller types a profile can ask for. Kept apart from the ViGEm-backed service so it can be read
/// on platforms where the ViGEm client library cannot even be loaded.
/// </summary>
public static class VirtualPadSupport
{
    /// <summary>The pad types ViGEm can emulate. Xbox One, DualSense and Switch Pro pads are not available through it.</summary>
    public static readonly IReadOnlyList<ControllerType> SupportedTypes = new[] { ControllerType.Xbox360, ControllerType.DualShock4 };

    public static bool IsSupported(ControllerType type) => SupportedTypes.Contains(type);
}

/// <summary>Stands in where no virtual controller driver exists (macOS). Never connects; reports are dropped.</summary>
public sealed class NullVirtualGamepad : IVirtualGamepad
{
    public bool IsDriverAvailable => false;
    public bool IsConnected => false;
    public ControllerType? ConnectedType => null;

    public void Initialize()
    {
    }

    public bool Connect(ControllerType type) => false;

    public void DisconnectVirtual()
    {
    }

    public void SendReport(RemappedState state)
    {
    }

    public void Dispose()
    {
    }
}
