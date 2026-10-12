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
    private WheelWindow? _wheel;
    private ViewModels.MainViewModel? _watched;

    /// <summary>The overlay window while it is open (for tests).</summary>
    internal OverlayWindow? Overlay => _overlay;

    /// <summary>The command wheel while it is on screen (for tests).</summary>
    internal WheelWindow? Wheel => _wheel;

    private TrayIconHost? _tray;
    private Views.TesterView? _tester;

    // What the window looked like before full screen, so leaving it puts everything back
    private bool _fullScreen;
    private WindowStyle _styleBefore;
    private ResizeMode _resizeBefore;
    private WindowState _stateBefore;

    /// <summary>The tester screen while the window is full screen (for tests).</summary>
    internal Views.TesterView? Tester => _tester;

    /// <summary>True while the window is full screen and showing the tester.</summary>
    internal bool IsFullScreen => _fullScreen;

    /// <summary>Full screen is the controller tester: F11 or the Tester button opens it, Esc leaves it.</summary>
    internal void SetFullScreen(bool on)
    {
        if (on == _fullScreen) return;
        var vm = DataContext as ViewModels.MainViewModel;

        if (on)
        {
            _styleBefore = WindowStyle;
            _resizeBefore = ResizeMode;
            _stateBefore = WindowState;

            // Normal first, so maximizing again covers the taskbar too
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _fullScreen = true;

            _tester = new Views.TesterView { DataContext = vm };
            _tester.ExitRequested += (_, _) => SetFullScreen(false);
            TesterHost.Content = _tester;
            TesterHost.Visibility = Visibility.Visible;
            if (vm != null) vm.IsTesterMode = true;
        }
        else
        {
            if (vm != null) vm.IsTesterMode = false;
            TesterHost.Visibility = Visibility.Collapsed;
            TesterHost.Content = null;
            _tester = null;

            _fullScreen = false;
            WindowState = WindowState.Normal;
            WindowStyle = _styleBefore;
            ResizeMode = _resizeBefore;
            WindowState = _stateBefore == WindowState.Minimized ? WindowState.Normal : _stateBefore;
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as ViewModels.MainViewModel);
        Watch(DataContext as ViewModels.MainViewModel);   // the XAML may already have created it

        // Maximising the window keeps the normal screen and makes room for the live readings under the controller; tell the view model,
        // so the controller is only measured while they are on screen
        Stage.DeckShownChanged += (_, _) =>
        {
            bool shown = Stage.IsDeckShown;
            Dispatcher.BeginInvoke(() => { if (DataContext is ViewModels.MainViewModel vm) vm.IsLiveDeckShown = shown; });
        };
        IsVisibleChanged += (_, _) => ReportShown();
        StateChanged += (_, _) => ReportShown();

        // `REWASD_START_TESTER=1` opens straight into the tester (the build checks that the tester screen loads this way)
        if (Environment.GetEnvironmentVariable("REWASD_START_TESTER") == "1") Loaded += (_, _) => SetFullScreen(true);
        // `REWASD_START_MAXIMIZED=1` opens maximised, which shows the live readings under the controller (the build checks they load)
        if (Environment.GetEnvironmentVariable("REWASD_START_MAXIMIZED") == "1") Loaded += (_, _) => WindowState = WindowState.Maximized;
        // Keys pressed while this window is in front move the keyboard drawing, even with remapping off. Alt (and F10) arrive as
        // "System" keys, with the real key alongside.
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler((_, e) =>
            (DataContext as ViewModels.MainViewModel)?.NoteWindowKey((e.Key == Key.System ? e.SystemKey : e.Key).ToString(), true)), handledEventsToo: true);
        AddHandler(PreviewKeyUpEvent, new KeyEventHandler((_, e) =>
            (DataContext as ViewModels.MainViewModel)?.NoteWindowKey((e.Key == Key.System ? e.SystemKey : e.Key).ToString(), false)), handledEventsToo: true);
        Deactivated += (_, _) => (DataContext as ViewModels.MainViewModel)?.ReleaseWindowKeys();

        PreviewKeyDown += (_, e) =>
        {
            var vm = DataContext as ViewModels.MainViewModel;
            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (e.Key == Key.F11) { SetFullScreen(!_fullScreen); e.Handled = true; }
            else if (e.Key == Key.Escape && _fullScreen) { SetFullScreen(false); e.Handled = true; }
            else if (e.Key == Key.Escape && vm is { IsPreferencesPage: true }) { vm.ToggleSettingsCommand.Execute(null); e.Handled = true; }
            else if (control && e.Key == Key.S && vm != null) { vm.SaveProfileCommand.Execute(null); e.Handled = true; }
            else if (control && e.Key == Key.OemComma && vm != null) { vm.ToggleSettingsCommand.Execute(null); e.Handled = true; }
            else if (control && e.Key == Key.Enter && vm != null) { vm.ToggleApplyCommand.Execute(null); e.Handled = true; }
        };

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
                SetFullScreen(false);       // the tester must not keep reading while hidden
                Hide();
                return;
            }

            _tray?.Dispose();
            _tray = null;
            CloseOverlay();
            CloseWheel();
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.StopLiveUpdates();
            vm?.Shutdown();
        };
    }

    private void ReportShown()
    {
        if (DataContext is ViewModels.MainViewModel vm) vm.IsWindowShown = IsVisible && WindowState != WindowState.Minimized;
    }

    private void Watch(ViewModels.MainViewModel? vm)
    {
        if (ReferenceEquals(_watched, vm)) return;
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
        }
    }

    private void OnTesterRequested(object? sender, EventArgs e) => SetFullScreen(true);

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_watched == null) return;

        if (e.PropertyName == nameof(ViewModels.MainViewModel.WheelVisible))
        {
            if (_watched.WheelVisible) OpenWheel(_watched);
            else CloseWheel();
            return;
        }

        if (e.PropertyName != nameof(ViewModels.MainViewModel.ShowOverlay)) return;

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

    private void OpenWheel(ViewModels.MainViewModel vm)
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