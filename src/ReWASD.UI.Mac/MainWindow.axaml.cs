using Avalonia.Controls;
using Avalonia.Input;
using ReWASD.UI.Mac.Tester;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac;

public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private WheelWindow? _wheel;
    private MainViewModel? _watched;

    /// <summary>The overlay window while it is open (for tests).</summary>
    internal OverlayWindow? Overlay => _overlay;

    /// <summary>The command wheel while it is on screen (for tests).</summary>
    internal WheelWindow? Wheel => _wheel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as MainViewModel);

        // Full screen is the controller tester: F11 or the button toggles it, Esc leaves it, and the green button of a Mac window counts too
        this.FindControl<Button>("TesterButton")!.Click += (_, _) => ToggleFullScreen();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; }
            else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) { WindowState = WindowState.Normal; e.Handled = true; }
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) ShowTester(WindowState == WindowState.FullScreen);
        };
        Closing += (_, e) =>
        {
            // With "keep running in the background" the close button only hides the window; quitting from the tray, the system
            // shutting down or Cmd+Q still close it for real
            if (e.CloseReason == WindowCloseReason.WindowClosing && DataContext is MainViewModel { KeepRunningInBackground: true, IsQuitting: false })
            {
                e.Cancel = true;
                if (WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;    // the tester must not keep reading while hidden
                Hide();
                return;
            }

            CloseOverlay();
            CloseWheel();
            var vm = DataContext as MainViewModel;
            vm?.StopLiveUpdates();
            vm?.Shutdown();
        };
    }

    /// <summary>The tester screen while the window is full screen (for tests).</summary>
    internal TesterView? Tester { get; private set; }

    private WindowState _stateBeforeFullScreen = WindowState.Normal;

    private void ToggleFullScreen()
    {
        if (WindowState == WindowState.FullScreen) WindowState = _stateBeforeFullScreen == WindowState.FullScreen ? WindowState.Normal : _stateBeforeFullScreen;
        else
        {
            _stateBeforeFullScreen = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    private void ShowTester(bool on)
    {
        var host = this.FindControl<ContentControl>("TesterHost")!;
        var vm = DataContext as MainViewModel;

        if (on && Tester == null)
        {
            Tester = new TesterView { DataContext = vm };
            Tester.ExitRequested += (_, _) => WindowState = WindowState.Normal;
            host.Content = Tester;
            host.IsVisible = true;
            if (vm != null) vm.IsTesterMode = true;
        }
        else if (!on && Tester != null)
        {
            if (vm != null) vm.IsTesterMode = false;
            host.IsVisible = false;
            host.Content = null;
            Tester = null;
        }
    }

    private void Watch(MainViewModel? vm)
    {
        if (_watched != null) _watched.PropertyChanged -= OnViewModelChanged;
        _watched = vm;
        if (vm != null) vm.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_watched == null) return;

        if (e.PropertyName == nameof(MainViewModel.WheelVisible))
        {
            if (_watched.WheelVisible) OpenWheel(_watched);
            else CloseWheel();
            return;
        }

        if (e.PropertyName != nameof(MainViewModel.ShowOverlay)) return;

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

    private void OpenWheel(MainViewModel vm)
    {
        if (_wheel != null) return;
        _wheel = new WheelWindow(vm);
        _wheel.Show();
    }

    private void CloseWheel()
    {
        var wheel = _wheel;
        _wheel = null;
        wheel?.Close();
    }

    private void CloseOverlay()
    {
        var overlay = _overlay;
        _overlay = null;
        overlay?.Close();
    }
}
