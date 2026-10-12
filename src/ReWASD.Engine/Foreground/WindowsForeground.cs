using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ReWASD.Engine.Foreground;

/// <summary>Finds the program that owns the window in front on Windows.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsForeground
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref uint size);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static ForegroundApp? Current()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return null;

            GetWindowThreadProcessId(window, out uint pid);
            if (pid == 0) return null;

            var process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero) return null;
            try
            {
                var path = new StringBuilder(1024);
                uint size = (uint)path.Capacity;
                if (!QueryFullProcessImageNameW(process, 0, path, ref size)) return null;

                string full = path.ToString(0, (int)size);
                return new ForegroundApp(Path.GetFileName(full), full, (int)pid);
            }
            finally
            {
                CloseHandle(process);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ArgumentException)
        {
            return null;
        }
    }
}
