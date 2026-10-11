using System.Runtime.Versioning;

namespace ReWASD.Engine.Platform.MacOS;

/// <summary>
/// The privacy permissions macOS asks for before an app may watch or inject keyboard and mouse input.
/// The grant belongs to the app that is running: ReWASD.app, or the terminal when started from one.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacPermissions
{
    /// <summary>Input Monitoring: lets the app see real keys and mouse buttons (needed to remap them).</summary>
    public static bool CanMonitorInput => Safe(MacNative.CGPreflightListenEventAccess);

    /// <summary>Accessibility: lets the app inject keys and mouse events and swallow the originals.</summary>
    public static bool CanControlInput => Safe(MacNative.CGPreflightPostEventAccess) || Safe(MacNative.AXIsProcessTrusted);

    public static bool AllGranted => CanMonitorInput && CanControlInput;

    /// <summary>Shows the system permission prompts for whatever has not been granted yet.</summary>
    public static void Request()
    {
        if (!CanMonitorInput) Safe(MacNative.CGRequestListenEventAccess);
        if (!CanControlInput) Safe(MacNative.CGRequestPostEventAccess);
    }

    /// <summary>What to tell the person when something is missing, or null if all is granted.</summary>
    public static string? DescribeMissing()
    {
        var missing = new List<string>();
        if (!CanControlInput) missing.Add("Accessibility");
        if (!CanMonitorInput) missing.Add("Input Monitoring");
        return missing.Count == 0
            ? null
            : $"Allow {string.Join(" and ", missing)} for this app in System Settings > Privacy & Security, then restart it.";
    }

    // Older systems lack these calls; assume access there rather than blocking the app
    private static bool Safe(Func<bool> check)
    {
        try
        {
            return check();
        }
        catch (EntryPointNotFoundException)
        {
            return true;
        }
        catch (DllNotFoundException)
        {
            return true;
        }
    }
}
