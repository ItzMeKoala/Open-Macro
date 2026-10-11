namespace ReWASD.Engine.Input;

/// <summary>A physical controller source: polled for state, and able to say which controllers are connected.</summary>
public interface IPhysicalController : IGamepadReader
{
    /// <summary>
    /// Slot of the virtual pad to ignore so the app's own output is never read back in as input, or -1 for none.
    /// Only meaningful where the virtual pad is itself visible as an input device (XInput on Windows).
    /// </summary>
    int ExcludedUserIndex { get; set; }

    /// <summary>Slot currently being read, or -1 if no controller is connected.</summary>
    int ActiveUserIndex { get; }

    /// <summary>The slots that currently have a controller connected.</summary>
    IReadOnlyList<int> ConnectedSlots();

    /// <summary>A person-readable name for a slot returned by <see cref="ConnectedSlots"/>.</summary>
    string DescribeSlot(int slot);
}
