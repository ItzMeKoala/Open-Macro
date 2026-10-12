using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReWASD.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private ViewModels.MainViewModel? _watched;

    /// <summary>The overlay window while it is open (for tests).</summary>
    internal OverlayWindow? Overlay => _overlay;

    private TrayIconHost? _tray;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as ViewModels.MainViewModel);
        Watch(DataContext as ViewModels.MainViewModel);   // the XAML may already have created it

        try
        {
            if (DataContext is ViewModels.MainViewModel viewModel) _tray = new TrayIconHost(this, viewModel);
        }
        catch (Exception)
        {
            // No notification area available: the app simply works without it
        }

        if (Application.Current != null)
        {
            Application.Current.SessionEnding += (_, _) => (DataContext as ViewModels.MainViewModel)?.NoteSystemShutdown();
        }

        Closing += (_, e) =>
        {
            // With "keep running in the background" the close button only hides the window; Quit in the tray (or the system
            // shutting down) closes it for real
            if (DataContext is ViewModels.MainViewModel { KeepRunningInBackground: true, IsQuitting: false })
            {
                e.Cancel = true;
                Hide();
                return;
            }

            _tray?.Dispose();
            _tray = null;
            CloseOverlay();
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.StopLiveUpdates();
            vm?.Shutdown();
        };
    }

    private void Watch(ViewModels.MainViewModel? vm)
    {
        if (ReferenceEquals(_watched, vm)) return;
        if (_watched != null) _watched.PropertyChanged -= OnViewModelChanged;
        _watched = vm;
        if (vm != null) vm.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModels.MainViewModel.ShowOverlay) || _watched == null) return;

        if (_watched.ShowOverlay) OpenOverlay(_watched);
        else CloseOverlay();
    }

    private void OpenOverlay(ViewModels.MainViewModel vm)
    {
        if (_overlay != null) return;

        _overlay = new OverlayWindow(vm);
        _overlay.Closed += (_, _) =>
        {
            _overlay = null;
            if (vm.ShowOverlay) vm.ShowOverlay = false;      // closed from its own window: keep the switch honest
        };
        _overlay.Show();
    }

    private void CloseOverlay()
    {
        var overlay = _overlay;
        _overlay = null;
        overlay?.Close();
    }
}