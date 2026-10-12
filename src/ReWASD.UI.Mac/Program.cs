using Avalonia;

namespace ReWASD.UI.Mac;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Anything unexpected is written to crash.log (in the app's folder) before the app goes down
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) ReWASD.UI.AppPaths.WriteCrashLog(ex, "Unhandled");
        };
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
