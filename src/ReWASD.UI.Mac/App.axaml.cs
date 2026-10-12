using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Must be set before the view model is created
            UiHost.Current = new AvaloniaUi();
            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            try
            {
                TrayHost.Attach(this, window, vm);
            }
            catch (Exception)
            {
                // No menu bar or tray available: the app simply works without it
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
