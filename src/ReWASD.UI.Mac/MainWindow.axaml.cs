using Avalonia.Controls;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) =>
        {
            var vm = DataContext as MainViewModel;
            vm?.StopLiveUpdates();
            vm?.Shutdown();
        };
    }
}
