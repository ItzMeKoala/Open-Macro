using Avalonia.Controls;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac;

public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private MainViewModel? _watched;

    /// <summary>The overlay window while it is open (for tests).</summary>
    internal OverlayWindow? Overlay => _overlay;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as MainViewModel);
        Closing += (_, _) =>
        {
            CloseOverlay();
            var vm = DataContext as MainViewModel;
            vm?.StopLiveUpdates();
            vm?.Shutdown();
        };
    }

    private void Watch(MainViewModel? vm)
    {
        if (_watched != null) _watched.PropertyChanged -= OnViewModelChanged;
        _watched = vm;
        if (vm != null) vm.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ShowOverlay) || _watched == null) return;

        if (_watched.ShowOverlay) OpenOverlay(_watched);
        else CloseOverlay();
    }

    private void OpenOverlay(MainViewModel vm)
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
