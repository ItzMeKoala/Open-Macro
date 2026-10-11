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
    }
}

