using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using ReWASD.Engine.Foreground;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform.MacOS;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.Engine.Platform;

/// <summary>What the current operating system can and cannot do, so the app can say so instead of failing quietly.</summary>
public sealed record PlatformInfo(
    string Name,
    string ControllerApi,
    string NoControllerText,
    string PanicKeyText,
    bool SupportsVirtualController,
    bool SupportsDeviceHiding,
    bool SupportsRumble,
    bool NeedsInputPermissions);

/// <summary>The operating-system pieces the engine runs on, created together for one platform.</summary>
public sealed class PlatformBackends : IDisposable
{
    public required PlatformInfo Info { get; init; }
    public required IPhysicalController Reader { get; init; }
    public required IInputBackend InputBackend { get; init; }
    public required IRumbleOutput Rumble { get; init; }
    public required IVirtualGamepad VirtualPad { get; init; }

    /// <summary>Builds the keyboard/mouse hook; the callbacks say which keys and mouse buttons are currently mapped.</summary>
    public required Func<Func<string, bool>, Func<string, bool>, IInputHook> CreateHook { get; init; }

    /// <summary>Words for the permissions that still need granting, or null when none are missing.</summary>
    public Func<string?> MissingPermissions { get; init; } = () => null;

    /// <summary>Which application is in front right now; null when this platform cannot tell. Cheap enough to call twice a second.</summary>
    public Func<ForegroundApp?>? Foreground { get; init; }

    /// <summary>Asks the system to show its permission prompts.</summary>
    public Action RequestPermissions { get; init; } = () => { };

    public void Dispose()
    {
        VirtualPad.Dispose();
        (InputBackend as IDisposable)?.Dispose();
    }
}

public static class PlatformServices
{
    public static readonly PlatformInfo WindowsInfo = new("Windows", "XInput", "No XInput controller detected", "Ctrl + Alt + Shift + F12",
        SupportsVirtualController: true, SupportsDeviceHiding: true, SupportsRumble: true, NeedsInputPermissions: false);

    public static readonly PlatformInfo MacOSInfo = new("macOS", "GameController", "No game controller detected", "Control + Option + Shift + F12",
        SupportsVirtualController: false, SupportsDeviceHiding: false, SupportsRumble: false, NeedsInputPermissions: true);

    /// <summary>Describes the platform this process runs on.</summary>
    public static PlatformInfo Current =>
        OperatingSystem.IsWindows() ? WindowsInfo
        : OperatingSystem.IsMacOS() ? MacOSInfo
        : throw new PlatformNotSupportedException("ReWASD runs on Windows and macOS.");

    public static PlatformBackends Create() =>
        OperatingSystem.IsWindows() ? CreateWindows()
        : OperatingSystem.IsMacOS() ? CreateMacOS()
        : throw new PlatformNotSupportedException("ReWASD runs on Windows and macOS.");

    // Kept in separate, non-inlined methods so a platform's types (the ViGEm client on Windows) are only
    // loaded when that platform is actually running.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PlatformBackends CreateWindows()
    {
        var reader = new XInputReader();
        return new PlatformBackends
        {
            Info = WindowsInfo,
            Reader = reader,
            InputBackend = new Win32InputBackend(),
            Rumble = new XInputRumbleOutput(reader),
            VirtualPad = new VirtualGamepadService(),
            CreateHook = (key, mouse) => new LowLevelInputHook(key, mouse),
            Foreground = WindowsForeground.Current
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SupportedOSPlatform("macos")]
    private static PlatformBackends CreateMacOS()
    {
        MacInputHook? hook = null;
        return new PlatformBackends
        {
            Info = MacOSInfo,
            Reader = new MacGameController(),
            InputBackend = new MacInputBackend(() => hook?.SwallowedModifierFlags ?? 0),
            Rumble = new NullRumbleOutput(),
            VirtualPad = new NullVirtualGamepad(),
            CreateHook = (key, mouse) => hook = new MacInputHook(key, mouse),
            Foreground = MacForeground.Current,
            MissingPermissions = MacPermissions.DescribeMissing,
            RequestPermissions = MacPermissions.Request
        };
    }
}

/// <summary>Used where the physical controller cannot be vibrated; rumble requests are ignored.</summary>
public sealed class NullRumbleOutput : IRumbleOutput
{
    public void Rumble(double left, double right)
    {
    }
}
