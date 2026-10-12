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

        // The tester opens full screen from the Tester button or F11, and Esc closes it. Making the window full screen any other way (the
        // green button) keeps the normal screen, with the live readings under the controller.
        KeyDown += (_, e) =>
        {
            var vm = DataContext as MainViewModel;
            bool command = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (e.Key == Key.F11) { if (Tester != null) CloseTester(); else OpenTester(); e.Handled = true; }
            else if (e.Key == Key.Escape && Tester != null) { CloseTester(); e.Handled = true; }
            else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) { WindowState = WindowState.Normal; e.Handled = true; }
            else if (e.Key == Key.Escape && vm is { IsPreferencesPage: true }) { vm.ToggleSettingsCommand.Execute(null); e.Handled = true; }
            else if (command && e.Key == Key.S && vm != null) { vm.SaveProfileCommand.Execute(null); e.Handled = true; }
            else if (command && e.Key == Key.OemComma && vm != null) { vm.ToggleSettingsCommand.Execute(null); e.Handled = true; }
            else if (command && e.Key == Key.Enter && vm != null) { vm.ToggleApplyCommand.Execute(null); e.Handled = true; }
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState != WindowState.FullScreen) ShowTester(false);
            if (e.Property == WindowStateProperty || e.Property == IsVisibleProperty) ReportShown();
        };

        // Tell the view model when the live readings have room, so the controller is only measured while they are on screen
        var stage = this.FindControl<Views.StagePanel>("Stage")!;
        stage.DeckShownChanged += (_, _) =>
        {
            bool shown = stage.IsDeckShown;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (DataContext is MainViewModel vm) vm.IsLiveDeckShown = shown; });
        };
        Closing += (_, e) =>
        {
            // With "keep running in the background" the close button only hides the window; quitting from the tray, the system
            // shutting down or Cmd+Q still close it for real
            if (e.CloseReason == WindowCloseReason.WindowClosing && DataContext is MainViewModel { KeepRunningInBackground: true, IsQuitting: false })
            {
                e.Cancel = true;
                if (Tester != null) CloseTester();       // the tester must not keep reading while hidden
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

    private WindowState _stateBeforeTester = WindowState.Normal;
    private bool _testerWentFullScreen;

    /// <summary>Opens the tester, full screen. If the window was not full screen already, closing the tester puts it back.</summary>
    internal void OpenTester()
    {
        if (Tester != null) return;
        _testerWentFullScreen = WindowState != WindowState.FullScreen;
        if (_testerWentFullScreen)
        {
            _stateBeforeTester = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            WindowState = WindowState.FullScreen;
        }
        ShowTester(true);
    }

    /// <summary>Closes the tester, and leaves full screen if the tester was what entered it.</summary>
    internal void CloseTester()
    {
        ShowTester(false);
        if (_testerWentFullScreen && WindowState == WindowState.FullScreen) WindowState = _stateBeforeTester;
        _testerWentFullScreen = false;
    }

    private void ReportShown()
    {
        if (DataContext is MainViewModel vm) vm.IsWindowShown = IsVisible && WindowState != WindowState.Minimized;
    }

    private void ShowTester(bool on)
    {
        var host = this.FindControl<ContentControl>("TesterHost")!;
        var vm = DataContext as MainViewModel;

        if (on && Tester == null)
        {
            Tester = new TesterView { DataContext = vm };
            Tester.ExitRequested += (_, _) => CloseTester();
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
        if (_watched != null)
        {
            _watched.PropertyChanged -= OnViewModelChanged;
            _watched.TesterRequested -= OnTesterRequested;
        }
        _watched = vm;
        if (vm != null)
        {
            vm.PropertyChanged += OnViewModelChanged;
            vm.TesterRequested += OnTesterRequested;
            ReportShown();
        }
    }

    private void OnTesterRequested(object? sender, EventArgs e) => OpenTester();

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
