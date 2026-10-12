using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ReWASD.Engine.Platform.MacOS;

namespace ReWASD.Engine.Foreground;

/// <summary>Finds the frontmost application on macOS (NSWorkspace), without needing any permission.</summary>
[SupportedOSPlatform("macos")]
public static class MacForeground
{
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern int SendInt(IntPtr self, IntPtr sel);

    public static ForegroundApp? Current()
    {
        IntPtr pool = IntPtr.Zero;
        try
        {
            ObjC.LoadFramework("AppKit");
            pool = ObjC.objc_autoreleasePoolPush();

            var workspace = ObjC.Get(ObjC.Class("NSWorkspace"), "sharedWorkspace");
            var app = ObjC.Get(workspace, "frontmostApplication");
            if (app == IntPtr.Zero) return null;

            string? name = ObjC.GetString(app, "localizedName");
            string? bundle = ObjC.GetString(app, "bundleIdentifier");
            int pid = SendInt(app, ObjC.Sel("processIdentifier"));
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(bundle)) return null;

            return new ForegroundApp(name ?? bundle!, bundle, pid);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            if (pool != IntPtr.Zero) ObjC.objc_autoreleasePoolPop(pool);
        }
    }
}
