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
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
