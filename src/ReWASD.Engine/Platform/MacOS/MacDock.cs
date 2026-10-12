using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReWASD.Engine.Platform.MacOS;

/// <summary>Shows or hides the app's Dock icon. A hidden app lives in the menu bar only.</summary>
[SupportedOSPlatform("macos")]
public static class MacDock
{
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendActivationPolicy(IntPtr self, IntPtr sel, nint policy);

    private const nint Regular = 0;     // NSApplicationActivationPolicyRegular: Dock icon and menu bar
    private const nint Accessory = 1;   // NSApplicationActivationPolicyAccessory: no Dock icon

    /// <summary>Returns false if the system refused (or the call is not available).</summary>
    public static bool SetHidden(bool hidden)
    {
        try
        {
            ObjC.LoadFramework("AppKit");
            var app = ObjC.Get(ObjC.Class("NSApplication"), "sharedApplication");
            return app != IntPtr.Zero && SendActivationPolicy(app, ObjC.Sel("setActivationPolicy:"), hidden ? Accessory : Regular);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
