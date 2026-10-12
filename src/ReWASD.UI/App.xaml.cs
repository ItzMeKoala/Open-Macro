using System.Configuration;
using System.Data;
using System.Windows;

namespace ReWASD.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        // Must be set before MainWindow.xaml creates its MainViewModel
        UiHost.Current = new WpfUi();

        // Anything unexpected is written to crash.log (in the app's folder) before the app goes down
        DispatcherUnhandledException += (_, e) => AppPaths.WriteCrashLog(e.Exception, "UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) AppPaths.WriteCrashLog(ex, "Background thread");
        };
    }
}

