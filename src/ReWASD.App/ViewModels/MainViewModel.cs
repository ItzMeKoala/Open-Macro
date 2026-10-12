using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine;
using ReWASD.Engine.Actions;
using ReWASD.UI.Profiles;
using ReWASD.UI.Tester;
using ReWASD.Engine.Foreground;
using ReWASD.Engine.Scripting;
using ReWASD.Engine.HidHide;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.Updates;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;
using ReWASD.Core.Skins;
using ReWASD.Core.Skins.Art;

namespace ReWASD.UI.ViewModels;

public enum AppPage
{
    Remap,
    Preferences
}

/// <summary>Whether the selected input is remapped to something else, or runs a macro.</summary>
public enum TargetMode
{
    Remap,
    Macro,
    Script
}

/// <summary>Which kind of input is being mapped: the controller, the keyboard or the mouse.</summary>
public enum InputTab
{
    Gamepad,
    Keyboard,
    Mouse
}

public partial class MainViewModel : ObservableObject
{
    private const string ScrollUpLabel = "Scroll Up";
    private const string ScrollDownLabel = "Scroll Down";

    private readonly PlatformBackends _platform;
    private readonly ProfileHistory _history;
    private readonly Func<ForegroundApp?>? _foreground;
    private System.Threading.Timer? _foregroundTimer;
    private Profile? _manualProfile;
    private bool _autoSwitching;
    private readonly IUpdateService _updates;
    private readonly UpdateInstaller _installer;
    private UpdateInfo? _pendingUpdate;
    private readonly IPlatformUi _ui;
    private readonly string _profilesDirectory;
    private readonly MappingEngine _mappingEngine;
    private readonly IVirtualGamepad _virtualService;
    private readonly IPhysicalController _reader;
    private readonly IInputBackend _inputBackend;
    private readonly KeyboardMouseInjector _injector;
    private readonly HidHideService _hidHide = new();
    private readonly MacroRunner _macros;
    private readonly MacroPadState _macroPad;
    private readonly IInputHook _hook;
    private readonly InputPipeline _pipeline;
    private readonly MacroRecorder _recorder = new();
    private readonly IDisposable _liveTimer;
    private bool _hadController;
    private readonly Action<string, bool> _recordKey;
    private readonly Action<string, bool> _recordMouse;
    private bool _hookStartedForRecording;
    private IReadOnlyList<string> _physicalDevices = Array.Empty<string>();
    private bool _loadingLayerOptions;

    [ObservableProperty]
    private ObservableCollection<Profile> _profiles = new();

    [ObservableProperty]
    private Profile _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreferencesPage))]
    private AppPage _currentPage = AppPage.Remap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGamepadTab))]
    [NotifyPropertyChangedFor(nameof(ShowSkinArt))]
    private InputTab _selectedInputTab = InputTab.Gamepad;

    [ObservableProperty]
    private ObservableCollection<ButtonViewModel> _currentSources = new();

    // Target process for the selected profile (empty = applies to everything)
    [ObservableProperty]
    private string _selectedProcessExecutable = string.Empty;

    [ObservableProperty]
    private bool _isOutputMenuOpen;

    [ObservableProperty]
    private bool _isConfigMenuOpen;

    [ObservableProperty]
    private bool _isLayerMenuOpen;

    // Hides the physical pad through HidHide; hiding is per device, not per button
    [ObservableProperty]
    private bool _hideInputDevice;

    /// <summary>The kind of virtual controller games will see (stored per config).</summary>
    [ObservableProperty]
    private ControllerType _selectedVirtualController = ControllerType.Xbox360;

    public IReadOnlyList<VirtualControllerOption> VirtualControllerOptions { get; }

    private static IReadOnlyList<VirtualControllerOption> BuildVirtualControllerOptions(PlatformInfo platform)
    {
        // macOS has no driver that can present a virtual controller, so no type is available there
        const string NoMac = "Not available on macOS: it has no virtual game controller driver";
        bool can = platform.SupportsVirtualController;
        return new[]
        {
            new VirtualControllerOption(ControllerType.Xbox360, "Xbox 360", can, can ? "Works with every game that supports an Xbox controller" : NoMac),
            new VirtualControllerOption(ControllerType.DualShock4, "DualShock 4", can, can ? "For games and emulators that expect a PlayStation 4 controller" : NoMac),
            new VirtualControllerOption(ControllerType.XboxOne, "Xbox One", false, "Not available: the ViGEm driver cannot emulate an Xbox One controller"),
            new VirtualControllerOption(ControllerType.DualSense, "DualSense (PS5)", false, "Not available: the ViGEm driver cannot emulate a DualSense controller"),
            new VirtualControllerOption(ControllerType.SwitchPro, "Switch Pro", false, "Not available: the ViGEm driver cannot emulate a Switch Pro controller")
        };
    }

    // What hiding the physical controller actually did, so a failure is never silent
    [ObservableProperty]
    private string _hideStatusText = "Physical controller is not hidden";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingHint = string.Empty;

    partial void OnHideInputDeviceChanged(bool value) => ApplyHidingNow();

    partial void OnSelectedVirtualControllerChanged(ControllerType value)
    {
        SelectedProfile.TargetControllerType = value;
        IsOutputMenuOpen = false;

        // The drawing follows the emulated controller (it can still be changed by hand afterwards)
        SelectedSkin = value == ControllerType.DualShock4 ? SkinKind.PlayStation4 : SkinKind.Xbox;

        var name = VirtualControllerOptions.FirstOrDefault(o => o.Type == value)?.Name ?? value.ToString();
        StatusMessage = $"Virtual controller: {name}";

        if (IsRemappingActive) ReconnectVirtualController();
    }

    /// <summary>Swaps the virtual pad for a different type while remapping is running.</summary>
    private void ReconnectVirtualController()
    {
        // The old pad stays excluded from input until its replacement is known, so it can't be read back in
        _virtualService.DisconnectVirtual();
        Thread.Sleep(300); // the driver removes the old pad asynchronously

        var before = _reader.ConnectedSlots();
        bool ok = _virtualService.Connect(SelectedVirtualController);
        _reader.ExcludedUserIndex = ok && SelectedVirtualController == ControllerType.Xbox360 ? FindNewSlot(before) : -1;

        StatusMessage = ok ? $"🟢 REMAPPING ACTIVE — Virtual {SelectedVirtualController} connected" : "Could not connect the virtual controller";
        PopulateConnectedDevices();
    }

    public IReadOnlyList<int> SlotNumbers { get; } = new[] { 1, 2, 3, 4 };

    partial void OnActiveSlotChanged(int value)
    {
        SelectedProfile.ActiveSlot = value;
    }

    public IReadOnlyList<string> LayerNames { get; } = new[] { "Main Layer", "Shift 1", "Shift 2", "Shift 3", "Shift 4" };

    public bool IsPreferencesPage => CurrentPage == AppPage.Preferences;
    public bool IsGamepadTab => SelectedInputTab == InputTab.Gamepad;

    public bool IsDriverAvailable => _virtualService.IsDriverAvailable;
    public bool IsHidHideAvailable => _hidHide.IsAvailable;

    /// <summary>What the operating system can and cannot do (virtual controller, hiding the pad, permissions).</summary>
    public PlatformInfo Platform => _platform.Info;
    public bool SupportsVirtualController => Platform.SupportsVirtualController;
    public bool SupportsDeviceHiding => Platform.SupportsDeviceHiding;
    public bool IsMacOS => Platform.NeedsInputPermissions;
    public string PanicKeyText => Platform.PanicKeyText;

    /// <summary>The version and git commit of this build, shown in the bottom bar.</summary>
    public string BuildText => BuildInfo.Text;

    public string DriverStatusText =>
        !SupportsVirtualController ? $"Virtual controller: not available on {Platform.Name}"
        : IsDriverAvailable ? "ViGEmBus: Ready" : "ViGEmBus: Not installed";

    public string HidHideStatusText =>
        !SupportsDeviceHiding ? $"Hide controller: not available on {Platform.Name}"
        : IsHidHideAvailable ? "HidHide: Ready" : "HidHide: Not installed";

    /// <summary>macOS asks permission before an app may watch or inject keys and the mouse; empty on systems that don't.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPermissionIssue))]
    [NotifyPropertyChangedFor(nameof(PermissionStatusText))]
    private string _missingPermissions = string.Empty;

    public bool HasPermissionIssue => MissingPermissions.Length > 0;
    public string PermissionStatusText => !Platform.NeedsInputPermissions ? string.Empty : HasPermissionIssue ? "Permissions: needed" : "Permissions: granted";

    private void RefreshPermissions() => MissingPermissions = _platform.MissingPermissions() ?? string.Empty;

    /// <summary>Shows the system's permission prompts (macOS) and re-checks.</summary>
    [RelayCommand]
    private void RequestPermissions()
    {
        _platform.RequestPermissions();
        RefreshPermissions();
        StatusMessage = HasPermissionIssue ? MissingPermissions : "Permissions are granted";
    }

    [ObservableProperty]
    private int _activeSlot = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLayerEditable))]
    private int _activeShiftLayer = 0;

    [ObservableProperty]
    private bool _isRemappingActive = false;

    [ObservableProperty]
    private string _statusMessage = "Ready — Emulation Idle";

    [ObservableProperty]
    private ButtonViewModel? _selectedButton;

    // Mapping Inspector Properties
    [ObservableProperty]
    private ActivatorType _selectedActivator = ActivatorType.Single;

    [ObservableProperty]
    private TargetType _selectedTargetType = TargetType.VirtualControllerButton;

    [ObservableProperty]
    private ControllerButton _selectedVirtualButton = ControllerButton.A;

    [ObservableProperty]
    private string _selectedKeyboardKey = "Space";

    [ObservableProperty]
    private string _selectedMouseButton = "Left Click";

    [ObservableProperty]
    private string _selectedScrollDirection = ScrollUpLabel;

    [ObservableProperty]
    private int _selectedJumpLayer = 1;

    [ObservableProperty]
    private string _macroScript = string.Empty;

    [ObservableProperty]
    private MacroRepeatMode _selectedMacroRepeat = MacroRepeatMode.Once;

    [ObservableProperty]
    private string _macroError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTurboMode))]
    private MappingMode _selectedMode = MappingMode.Normal;

    /// <summary>The repeat-speed slider only matters in Turbo mode.</summary>
    public bool IsTurboMode => SelectedMode == MappingMode.Turbo;

    [ObservableProperty]
    private int _turboRateMs = 50;

    [ObservableProperty]
    private bool _isMuted = false;

    // Activator timing (stored on the profile)
    [ObservableProperty]
    private int _longPressMs = new ActivatorTimings().LongPressMs;

    [ObservableProperty]
    private int _multiPressWindowMs = new ActivatorTimings().MultiPressWindowMs;

    [ObservableProperty]
    private int _pulseMs = new ActivatorTimings().PulseMs;

    // Shift layer activation (stored on the active layer)
    [ObservableProperty]
    private ShiftType _selectedLayerType = ShiftType.Hold;

    public bool IsLayerEditable => ActiveShiftLayer > 0;
    public IReadOnlyList<ShiftType> AvailableLayerTypes { get; } = Enum.GetValues<ShiftType>();
    public ObservableCollection<ModifierOption> LayerModifierOptions { get; } = new();

    // Collections for Inspector choices
    public IReadOnlyList<MappingMode> AvailableModes { get; } = Enum.GetValues<MappingMode>();
    public IReadOnlyList<MacroRepeatMode> AvailableMacroRepeatModes { get; } = Enum.GetValues<MacroRepeatMode>();
    public ObservableCollection<ButtonViewModel> ControllerButtons { get; } = new();
    public ObservableCollection<ButtonViewModel> KeyboardSources { get; } = new();
    public ObservableCollection<ButtonViewModel> MouseSources { get; } = new();
    public ObservableCollection<string> ConnectedDevices { get; } = new();

    /// <summary>Creates the view model for the operating system it runs on, hosted by the toolkit that registered itself in <see cref="UiHost"/>.</summary>
    public MainViewModel()
        : this(PlatformServices.Create(), UiHost.Current ?? new SynchronizationContextUi(), AppPaths.ProfilesDirectory, new UpdateService(), new UpdateInstaller(), RealActions)
    {
    }

    /// <param name="updates">Where to look for newer builds; the default never finds any, so tests and tools make no network calls.</param>
    // The one set of system actions the real app uses; it reads OBS's address from whatever the window currently holds
    private static readonly ObsSettings SharedObs = new();
    private static ISystemActions RealActions => new SystemActions(() => SharedObs);

    /// <param name="actions">What macro steps that open things, run commands or call the network do; the default does nothing, so tests never touch the real system.</param>
    public MainViewModel(PlatformBackends platform, IPlatformUi ui, string profilesDirectory, IUpdateService? updates = null, UpdateInstaller? installer = null, ISystemActions? actions = null)
    {
        _platform = platform;
        _ui = ui;
        _profilesDirectory = profilesDirectory;
        _history = new ProfileHistory(profilesDirectory);
        _updates = updates ?? new NoUpdates();
        _installer = installer ?? new UpdateInstaller();
        _reader = platform.Reader;
        _inputBackend = platform.InputBackend;
        VirtualControllerOptions = BuildVirtualControllerOptions(platform.Info);

        _selectedProfile = Profile.CreateDefault("Default Controller Profile");
        Profiles.Add(_selectedProfile);
        LoadSavedProfiles();

        _mappingEngine = new MappingEngine(_selectedProfile);
        _virtualService = platform.VirtualPad;
        _virtualService.Initialize();
        _injector = new KeyboardMouseInjector(_inputBackend);
        _macroPad = new MacroPadState();
        _macros = new MacroRunner(_inputBackend, _macroPad, platform.Rumble, actions ?? new NullSystemActions(), new JintScriptRunner(), platform.Foreground);
        _macros.ScriptMessage += message => _ui.Post(() => StatusMessage = message);
        _macros.ActionFailed += message => _ui.Post(() => StatusMessage = message);
        _hook = platform.CreateHook(_mappingEngine.IsKeySuppressed, _mappingEngine.IsMouseSuppressed);
        _hook.PanicRequested += OnPanicRequested;
        LeftStickSettings.CalibrationRequested = k => StartCalibration(LeftStickSettings, _leftCalibrator, k, left: true);
        RightStickSettings.CalibrationRequested = k => StartCalibration(RightStickSettings, _rightCalibrator, k, left: false);
        _macros.MacroStarted += () => Interlocked.Increment(ref _macroStarts);
        _foreground = platform.Foreground;
        _mappingEngine.LockChanged += locked => _ui.Post(() => OnEngineLockChanged(locked));
        _pipeline = new InputPipeline(_mappingEngine, _reader, _virtualService, _injector, _macros, _macroPad, _hook);
        _pipeline.FrameProcessed += OnWheelFrame;
        _recordKey = _recorder.OnKey;
        _recordMouse = _recorder.OnMouse;

        // Drives the live button, trigger and stick display (and pad recording) from the UI thread
        _liveTimer = _ui.StartTimer(TimeSpan.FromMilliseconds(16), OnLiveTick);

        RefreshPermissions();
        PopulateInspectorOptions();
        PopulateConnectedDevices();
        InitializeControllerButtons();
        InitializeSourcePickers();
        BuildLayerModifierOptions();
        LoadProfileSettings(_selectedProfile);

        // Start with the A button selected
        var start = ControllerButtons.FirstOrDefault(b => b.Button == ControllerButton.A) ?? ControllerButtons.FirstOrDefault();
        if (start != null) SelectButton(start);

        _constructed = true;
        LoadSkinChoice();
        AutoSelectSkin();
        LoadUpdateSetting();
        LoadObsSettings();
        LoadAutoSwitchSetting();
        LoadBackgroundSettings();
        if (HideDockIcon && CanHideDockIcon) _ui.SetDockIconHidden(true);
        _manualProfile = _selectedProfile;
        RefreshHistory();
        LoadStats();
        if (AutoCheckUpdates && _updates is not NoUpdates) _ = CheckAfterStartupAsync();
    }

    /// <summary>Brings back configs saved with the save button. A saved config replaces the starter one of the same name.</summary>
    private void LoadSavedProfiles()
    {
        try
        {
            if (!System.IO.Directory.Exists(_profilesDirectory)) return;

            foreach (var file in System.IO.Directory.EnumerateFiles(_profilesDirectory, "*.json").OrderBy(f => f))
            {
                try
                {
                    var saved = ProfileSerializer.LoadFromFile(file);
                    if (string.IsNullOrWhiteSpace(saved.Name)) continue;

                    var existing = Profiles.FirstOrDefault(p => p.Name == saved.Name);
                    if (existing == null)
                    {
                        Profiles.Add(saved);
                        continue;
                    }

                    Profiles[Profiles.IndexOf(existing)] = saved;

                    // Assigned through the field on purpose: this runs in the constructor, before the engine exists
#pragma warning disable MVVMTK0034
                    if (ReferenceEquals(existing, _selectedProfile)) _selectedProfile = saved;
#pragma warning restore MVVMTK0034
                }
                catch (Exception)
                {
                    // A damaged or old-format file must not stop the app starting; it is simply skipped
                }
            }
        }
        catch (Exception)
        {
            // No profiles folder, or no access to it: start with just the starter config
        }
    }

    partial void OnSelectedProfileChanged(Profile value)
    {
        // A profile picked by hand becomes the one to come back to; one picked because of the app in front does not
        if (!_autoSwitching) _manualProfile = value;

        // Keep the engine on the profile being edited
        _mappingEngine.CurrentProfile = value;
        if (!value.ShiftLayers.ContainsKey(ActiveShiftLayer)) ActiveShiftLayer = 0;
        _mappingEngine.SetShiftLayer(ActiveShiftLayer);
        LoadProfileSettings(value);
        RefreshLayerOptions();
        UpdateAllButtonDisplays();
        if (SelectedButton != null) SelectButton(SelectedButton);
    }

    public StickViewModel LeftStickSettings { get; } = new("Left stick");
    public StickViewModel RightStickSettings { get; } = new("Right stick");

    /// <summary>"Same as the profile", then each stick mode: what a layer can set a stick to while it is active.</summary>
    public IReadOnlyList<string> LayerStickChoices { get; } = new[] { "Same as profile" }.Concat(StickViewModel.ModeNames).ToList();

    [ObservableProperty]
    private int _layerLeftStickIndex;

    [ObservableProperty]
    private int _layerRightStickIndex;

    private void LoadProfileSettings(Profile profile)
    {
        LoadLockChord(profile);
        LoadWheel(profile);
        RefreshCombos();
        _loadingProfile = true;
        try { ChordWindowMs = profile.ChordWindowMs; }
        finally { _loadingProfile = false; }
        profile.LeftStick ??= new StickSettings();
        profile.RightStick ??= new StickSettings();
        LeftStickSettings.Load(profile.LeftStick);
        RightStickSettings.Load(profile.RightStick);

        SelectedProcessExecutable = profile.ProcessExecutable ?? string.Empty;

        // A config saved with a type this build cannot emulate falls back to Xbox 360
        SelectedVirtualController = VirtualPadSupport.IsSupported(profile.TargetControllerType) ? profile.TargetControllerType : ControllerType.Xbox360;
        profile.TargetControllerType = SelectedVirtualController; // write the fallback back so it is saved correctly
        profile.Timings ??= new ActivatorTimings();
        LongPressMs = profile.Timings.LongPressMs;
        MultiPressWindowMs = profile.Timings.MultiPressWindowMs;
        PulseMs = profile.Timings.PulseMs;
    }

    partial void OnSelectedProcessExecutableChanged(string value)
    {
        SelectedProfile.ProcessExecutable = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (_constructed) UpdateForegroundWatching();
    }

    partial void OnLongPressMsChanged(int value) => SelectedProfile.Timings.LongPressMs = value;
    partial void OnMultiPressWindowMsChanged(int value) => SelectedProfile.Timings.MultiPressWindowMs = value;
    partial void OnPulseMsChanged(int value) => SelectedProfile.Timings.PulseMs = value;

    [RelayCommand]
    private void ToggleSettings()
    {
        CurrentPage = IsPreferencesPage ? AppPage.Remap : AppPage.Preferences;
    }

    [RelayCommand]
    private void ShowInfo()
    {
        StatusMessage = $"ReWASD Open Source {BuildInfo.Text} — clean-room gamepad remapper";
    }

    [RelayCommand]
    private void SaveProfile()
    {
        try
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var fileName = string.Concat(SelectedProfile.Name.Select(c => invalid.Contains(c) ? '_' : c)) + ".json";
            var path = System.IO.Path.Combine(_profilesDirectory, fileName);

            lock (_mappingEngine.SyncRoot)
            {
                KeepCopyOfSavedFile(path);
                ProfileSerializer.SaveToFile(SelectedProfile, path);
            }
            RefreshHistory();
            StatusMessage = $"Saved config to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save config: {ex.Message}";
        }
    }

    // ---- Stick tuning: live readout and calibration ---------------------------------------------------------------------

    private readonly StickCalibrator _leftCalibrator = new();
    private readonly StickCalibrator _rightCalibrator = new();
    private int _tuningTicks;

    private void StartCalibration(StickViewModel stick, StickCalibrator calibrator, CalibrationKind kind, bool left)
    {
        var phys = _pipeline.IsRunning ? _pipeline.LatestPhysical : _reader.Read();
        if (phys == null)
        {
            stick.CalibrationText = "Connect a controller first";
            return;
        }

        calibrator.Start(kind, NowMs());
        stick.IsCalibrating = true;
        stick.CalibrationProgress = 0;
        stick.CalibrationText = kind == CalibrationKind.Rest
            ? "Let go of the stick and leave it alone…"
            : "Roll the stick slowly around its edge, all the way…";
    }

    private void FeedStickTuning(PhysicalState? phys)
    {
        FeedCalibrator(LeftStickSettings, _leftCalibrator, phys?.LeftStickX, phys?.LeftStickY);
        FeedCalibrator(RightStickSettings, _rightCalibrator, phys?.RightStickX, phys?.RightStickY);

        // The readout is only worked out while Preferences is open, a few times a second
        if (!IsPreferencesPage || ++_tuningTicks % 4 != 0) return;
        LeftStickSettings.LiveText = Describe(phys?.LeftStickX, phys?.LeftStickY);
        RightStickSettings.LiveText = Describe(phys?.RightStickX, phys?.RightStickY);
    }

    private static string Describe(double? x, double? y) =>
        x is not { } px || y is not { } py ? "no controller" : $"x {px:+0.00;-0.00;0.00}   y {py:+0.00;-0.00;0.00}   push {Math.Sqrt(px * px + py * py):0.00}";

    private void FeedCalibrator(StickViewModel stick, StickCalibrator calibrator, double? x, double? y)
    {
        if (!calibrator.IsRunning) return;

        var kind = calibrator.Kind;
        double? result = calibrator.Feed(x, y, NowMs());
        stick.CalibrationProgress = calibrator.Progress;
        if (result is not { } value) return;

        stick.IsCalibrating = false;
        if (kind == CalibrationKind.Rest)
        {
            stick.DeadzoneMin = value;
            stick.CalibrationText = $"Inner deadzone set to {value:P0}";
        }
        else
        {
            stick.DeadzoneMax = value;
            stick.CalibrationText = $"Full push now counts from {value:P0}";
        }
    }

    // ---- Controller tester (the full-screen view) -------------------------------------------------------------------------

    public TesterViewModel Tester { get; } = new();
    private TesterSampler? _sampler;
    private int _testerTicks;
    private string _testerControllerName = "No controller";

    /// <summary>The tester screen is showing (the window is full screen). While it is, the controller is read as fast as it reports.</summary>
    [ObservableProperty]
    private bool _isTesterMode;

    partial void OnIsTesterModeChanged(bool value)
    {
        if (!_constructed) return;
        _sampler ??= new TesterSampler(ReadPhysical, Tester.Model);

        if (value)
        {
            _sampler.Start();
            RefreshTester(force: true);
        }
        else
        {
            _sampler.Stop();
            StopVibration();
        }
    }

    // While remapping runs the pipeline owns the pad reader; otherwise read it here
    private PhysicalState? ReadPhysical() => _pipeline.IsRunning ? _pipeline.LatestPhysical : _reader.Read();

    private void RefreshTester(bool force = false)
    {
        // The name of the controller changes rarely, and asking for it is not free: about once a second
        if (force || _testerTicks % 60 == 0)
        {
            try
            {
                int slot = _reader.ActiveUserIndex;
                _testerControllerName = slot < 0 ? "No controller" : _reader.DescribeSlot(slot);
            }
            catch (Exception)
            {
                _testerControllerName = "Controller";
            }
        }

        Tester.Refresh(Tester.Model.Snapshot(), _testerControllerName, SelectedProfile.LeftStick, SelectedProfile.RightStick);
    }

    [RelayCommand]
    private void ResetTester() => Tester.Model.Reset();

    [RelayCommand]
    private void StartReactionTest() => Tester.Model.StartReaction(TesterSampler.NowMs());

    [RelayCommand]
    private void ResetTesterLeftStick() => Tester.Model.ResetStickMeasurements(left: true);

    [RelayCommand]
    private void ResetTesterRightStick() => Tester.Model.ResetStickMeasurements(left: false);

    /// <summary>The platform can shake the controller (Windows XInput); elsewhere the vibration buttons are hidden.</summary>
    public bool CanVibrate => Platform.SupportsRumble;

    private int _vibrationToken;

    private void Vibrate(double left, double right)
    {
        if (!CanVibrate) return;
        int token = Interlocked.Increment(ref _vibrationToken);
        try { _platform.Rumble.Rumble(left, right); }
        catch (Exception) { return; }

        // A test shake lasts a moment, never until somebody remembers to stop it
        _ = Task.Delay(700).ContinueWith(_ =>
        {
            if (token == Volatile.Read(ref _vibrationToken)) StopVibration();
        });
    }

    [RelayCommand] private void VibrateLeft() => Vibrate(1.0, 0);
    [RelayCommand] private void VibrateRight() => Vibrate(0, 1.0);
    [RelayCommand] private void VibrateBoth() => Vibrate(1.0, 1.0);

    [RelayCommand]
    private void StopVibration()
    {
        Interlocked.Increment(ref _vibrationToken);
        if (!CanVibrate) return;
        try { _platform.Rumble.Rumble(0, 0); }
        catch (Exception) { /* nothing to stop */ }
    }

    // ---- Command wheel -------------------------------------------------------------------------------------------------

    public static IReadOnlyList<ControllerButton> WheelButtons { get; } = new[]
    {
        ControllerButton.None, ControllerButton.LeftBumper, ControllerButton.RightBumper, ControllerButton.LeftTrigger, ControllerButton.RightTrigger,
        ControllerButton.LeftStickClick, ControllerButton.RightStickClick, ControllerButton.View, ControllerButton.Menu
    };

    public static IReadOnlyList<string> WheelButtonNames { get; } =
        WheelButtons.Select(b => b == ControllerButton.None ? "Off" : ComboMapping.Names.Friendly(b)).ToList();

    public static IReadOnlyList<string> WheelStickNames { get; } = new[] { "Right stick", "Left stick" };

    public ObservableCollection<WheelItem> WheelItems { get; } = new();

    [ObservableProperty]
    private int _wheelButtonIndex;

    [ObservableProperty]
    private int _wheelStickIndex;

    [ObservableProperty]
    private double _wheelThreshold = 0.5;

    [ObservableProperty]
    private string _newWheelLabel = string.Empty;

    [ObservableProperty]
    private string _newWheelAction = string.Empty;

    [ObservableProperty]
    private string _wheelMessage = string.Empty;

    /// <summary>The wheel is open on screen (its button is held).</summary>
    [ObservableProperty]
    private bool _wheelVisible;

    /// <summary>The slice the stick points at, or -1.</summary>
    [ObservableProperty]
    private int _wheelSelectedIndex = -1;

    private volatile bool _wheelSeenOpen;
    private int _wheelSeenIndex = -1;

    private void LoadWheel(Profile profile)
    {
        profile.Wheel ??= new CommandWheel();
        _loadingProfile = true;
        try
        {
            WheelButtonIndex = Math.Max(0, WheelButtons.ToList().IndexOf(profile.Wheel.Button));
            WheelStickIndex = profile.Wheel.UseRightStick ? 0 : 1;
            WheelThreshold = profile.Wheel.Threshold;
        }
        finally
        {
            _loadingProfile = false;
        }
        WheelItems.Clear();
        foreach (var item in profile.Wheel.Items) WheelItems.Add(item);
    }

    partial void OnWheelButtonIndexChanged(int value)
    {
        if (_loadingProfile || value < 0 || value >= WheelButtons.Count) return;
        lock (_mappingEngine.SyncRoot) SelectedProfile.Wheel.Button = WheelButtons[value];
    }

    partial void OnWheelStickIndexChanged(int value)
    {
        if (_loadingProfile) return;
        lock (_mappingEngine.SyncRoot) SelectedProfile.Wheel.UseRightStick = value == 0;
    }

    partial void OnWheelThresholdChanged(double value)
    {
        if (_loadingProfile) return;
        lock (_mappingEngine.SyncRoot) SelectedProfile.Wheel.Threshold = value;
    }

    [RelayCommand]
    private void AddWheelItem()
    {
        WheelMessage = string.Empty;
        if (WheelItems.Count >= CommandWheel.MaxItems)
        {
            WheelMessage = $"A wheel has at most {CommandWheel.MaxItems} slices";
            return;
        }
        if (string.IsNullOrWhiteSpace(NewWheelLabel))
        {
            WheelMessage = "Give the slice a name";
            return;
        }
        if (!MacroDefinition.TryParseScript(NewWheelAction, MacroRepeatMode.Once, out var macro, out var error))
        {
            WheelMessage = error;
            return;
        }

        var item = new WheelItem { Label = NewWheelLabel.Trim(), Macro = macro };
        lock (_mappingEngine.SyncRoot) SelectedProfile.Wheel.Items.Add(item);
        WheelItems.Add(item);
        NewWheelLabel = string.Empty;
        NewWheelAction = string.Empty;
        WheelMessage = SelectedProfile.Wheel.IsOn
            ? $"Added {item.Label}"
            : WheelButtonIndex == 0 ? $"Added {item.Label}. Choose the button that opens the wheel to switch it on" : $"Added {item.Label}. A wheel needs at least two slices";
    }

    [RelayCommand]
    private void RemoveWheelItem(WheelItem? item)
    {
        if (item == null) return;
        lock (_mappingEngine.SyncRoot) SelectedProfile.Wheel.Items.Remove(item);
        WheelItems.Remove(item);
        WheelMessage = $"Removed {item.Label}";
    }

    // Called on the input thread after every frame: only tell the window when something changed
    internal void OnWheelFrame(RemappedState state)
    {
        if (state.WheelOpen == _wheelSeenOpen && state.WheelIndex == _wheelSeenIndex) return;
        bool open = state.WheelOpen;
        int index = state.WheelIndex;
        _wheelSeenOpen = open;
        _wheelSeenIndex = index;
        _ui.Post(() =>
        {
            WheelSelectedIndex = index;
            WheelVisible = open;
        });
    }

    // ---- Running in the background (tray / menu bar) ---------------------------------------------------------------------

    /// <summary>Closing the window hides it and ReWASD keeps running from the tray or menu bar; Quit there ends it.</summary>
    [ObservableProperty]
    private bool _keepRunningInBackground;

    /// <summary>macOS: no Dock icon, the menu bar icon only.</summary>
    [ObservableProperty]
    private bool _hideDockIcon;

    public bool CanHideDockIcon => Platform.Name == "macOS";

    /// <summary>Set when the person chose Quit (or the system is shutting down): the window really closes then.</summary>
    public bool IsQuitting { get; private set; }

    private string BackgroundSettingPath => System.IO.Path.Combine(_profilesDirectory, "background.cfg");

    partial void OnKeepRunningInBackgroundChanged(bool value) => SaveBackgroundSettings();

    partial void OnHideDockIconChanged(bool value)
    {
        if (_constructed && CanHideDockIcon && !_ui.SetDockIconHidden(value)) StatusMessage = "Could not change the Dock icon";
        SaveBackgroundSettings();
    }

    private void SaveBackgroundSettings()
    {
        if (!_constructed) return;
        try
        {
            System.IO.Directory.CreateDirectory(_profilesDirectory);
            System.IO.File.WriteAllText(BackgroundSettingPath, $"background={(KeepRunningInBackground ? "on" : "off")}\ndock={(HideDockIcon ? "hidden" : "shown")}\n");
        }
        catch (Exception)
        {
            // Not being able to remember a setting must not get in the way
        }
    }

    private void LoadBackgroundSettings()
    {
        try
        {
            if (!System.IO.File.Exists(BackgroundSettingPath)) return;
            var lines = System.IO.File.ReadAllLines(BackgroundSettingPath);
            KeepRunningInBackground = lines.Contains("background=on");
            if (CanHideDockIcon) HideDockIcon = lines.Contains("dock=hidden");
        }
        catch (Exception)
        {
            // An unreadable file just means the defaults
        }
    }

    /// <summary>Quits for real (the tray's Quit): the window will close instead of hiding.</summary>
    public void RequestQuit()
    {
        IsQuitting = true;
        _ui.RequestExit();
    }

    /// <summary>The system is shutting down or logging off: do not hold the window back.</summary>
    public void NoteSystemShutdown() => IsQuitting = true;

    // ---- Input overlay (a small always-on-top window that shows the controller, for streaming or just looking) -----------

    /// <summary>The overlay window is open. The front end opens and closes the window when this changes.</summary>
    [ObservableProperty]
    private bool _showOverlay;

    /// <summary>The overlay can draw the controller picture; otherwise it lists the pressed buttons as text.</summary>
    public bool OverlayHasArt => SkinArt != null;

    /// <summary>The buttons held right now, for the overlay's text form. Empty when nothing is held.</summary>
    [ObservableProperty]
    private string _pressedText = string.Empty;

    private void UpdatePressedText(PhysicalState? phys)
    {
        if (!ShowOverlay) return;
        string text = phys == null ? string.Empty : string.Join("  ", ControllerButtons.Where(b => phys.Buttons.Contains(b.Button)).Select(b => ComboMapping.Names.Friendly(b.Button)));
        if (text != PressedText) PressedText = text;
    }

    // ---- Presets -------------------------------------------------------------------------------------------------------

    public IReadOnlyList<ProfilePresets.Preset> Presets { get; } = ProfilePresets.All;

    [RelayCommand]
    private void NewFromPreset(ProfilePresets.Preset? preset)
    {
        if (preset == null) return;

        var profile = preset.Create();
        string baseName = profile.Name;
        for (int n = 2; Profiles.Any(p => p.Name == profile.Name); n++) profile.Name = $"{baseName} ({n})";

        Profiles.Add(profile);
        SelectedProfile = profile;
        UpdateForegroundWatching();
        UpdateAllButtonDisplays();
        StatusMessage = $"Created {profile.Name}: {preset.Description}";
    }

    // ---- Usage counts (this computer only) ------------------------------------------------------------------------------

    private UsageStats _stats = new();
    private long _macroStarts;
    private long _statsLastTick;
    private int _statsTicks;

    private string StatsPath => System.IO.Path.Combine(_profilesDirectory, "usage.cfg");

    [ObservableProperty]
    private string _usageSummary = string.Empty;

    public ObservableCollection<string> TopButtonLines { get; } = new();

    partial void OnIsRemappingActiveChanged(bool value)
    {
        if (value || !_constructed) return;
        _stats.Save(StatsPath);
        RefreshStatsDisplay();
    }

    private void LoadStats()
    {
        _stats = UsageStats.Load(StatsPath);
        RefreshStatsDisplay();
    }

    private void RefreshStatsDisplay()
    {
        UsageSummary = _stats.Summary;
        TopButtonLines.Clear();
        foreach (var (button, count) in _stats.Top(5)) TopButtonLines.Add($"{ComboMapping.Names.Friendly(button)}  —  {count:N0}");
    }

    private void CountUsage(PhysicalState? phys)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double seconds = _statsLastTick == 0 ? 0 : Math.Min(0.25, (now - _statsLastTick) / (double)System.Diagnostics.Stopwatch.Frequency);
        _statsLastTick = now;

        if (!_pipeline.IsRunning) return;

        _stats.SecondsRemapping += seconds;
        if (phys != null) _stats.Observe(phys.Buttons);
        _stats.MacrosRun += Interlocked.Exchange(ref _macroStarts, 0);

        // Look at the numbers about twice a second and write them out every half minute: no more work than that while gaming
        if (++_statsTicks % 32 == 0) RefreshStatsDisplay();
        if (_statsTicks % 1900 == 0) _stats.Save(StatsPath);
    }

    [RelayCommand]
    private void ResetUsage()
    {
        _stats.Reset();
        _stats.Save(StatsPath);
        RefreshStatsDisplay();
    }

    // ---- Combos: chords and sequences ----------------------------------------------------------------------------------

    public static IReadOnlyList<string> ComboKindNames { get; } = new[] { "Chord (together)", "Sequence (in order)" };

    public ObservableCollection<ComboMapping> Combos { get; } = new();

    [ObservableProperty]
    private int _newComboKindIndex;

    /// <summary>Button names for the new combo, like "A + B" or "Up Up Down Down" (or press the buttons and use Capture).</summary>
    [ObservableProperty]
    private string _newComboButtons = string.Empty;

    /// <summary>What the new combo does, written like a macro: KeyTap:Space, or KeyDown:W with the Hold repeat setting.</summary>
    [ObservableProperty]
    private string _newComboAction = string.Empty;

    [ObservableProperty]
    private int _newComboRepeatIndex;

    public static IReadOnlyList<string> ComboRepeatNames { get; } = new[] { "Run once", "Repeat while held", "Hold until released" };

    /// <summary>How close together (ms) chord buttons must go down. Buttons in a chord wait this long before acting alone.</summary>
    [ObservableProperty]
    private int _chordWindowMs = new Profile().ChordWindowMs;

    partial void OnChordWindowMsChanged(int value)
    {
        if (_constructed && !_loadingProfile) lock (_mappingEngine.SyncRoot) SelectedProfile.ChordWindowMs = value;
    }

    [ObservableProperty]
    private string _comboMessage = string.Empty;

    [ObservableProperty]
    private bool _isCapturingCombo;

    private readonly ComboCapture _capture = new();

    private static long NowMs() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000 / System.Diagnostics.Stopwatch.Frequency;

    private void RefreshCombos()
    {
        Combos.Clear();
        foreach (var combo in SelectedProfile.Combos ??= new List<ComboMapping>()) Combos.Add(combo);
    }

    [RelayCommand]
    private void CaptureComboButtons()
    {
        if (_capture.IsCapturing)
        {
            _capture.Cancel();
            IsCapturingCombo = false;
            ComboMessage = "Capture cancelled";
            return;
        }

        var kind = NewComboKindIndex == 1 ? ComboKind.Sequence : ComboKind.Chord;
        _capture.Start(kind, NowMs());
        IsCapturingCombo = true;
        ComboMessage = kind == ComboKind.Chord
            ? "Press and hold the buttons together on your controller, then let go"
            : "Press the buttons one after another on your controller";
    }

    private void FeedComboCapture(PhysicalState? phys)
    {
        if (!_capture.IsCapturing) return;

        var done = _capture.Feed(phys?.Buttons ?? new HashSet<ControllerButton>(), NowMs());
        if (done == null) return;

        IsCapturingCombo = false;
        NewComboButtons = string.Join(_capture.Kind == ComboKind.Chord ? " + " : " ", done.Select(ComboMapping.Names.Friendly));
        ComboMessage = $"Captured {NewComboButtons}. Now say what it does and press Add.";
    }

    [RelayCommand]
    private void AddCombo()
    {
        ComboMessage = string.Empty;

        if (!ComboMapping.Names.TryParse(NewComboButtons, out var buttons, out var error))
        {
            ComboMessage = error;
            return;
        }
        var kind = NewComboKindIndex == 1 ? ComboKind.Sequence : ComboKind.Chord;
        if (buttons.Count < 2)
        {
            ComboMessage = kind == ComboKind.Chord ? "A chord needs at least two buttons" : "A sequence needs at least two presses";
            return;
        }
        if (kind == ComboKind.Chord && buttons.Distinct().Count() != buttons.Count)
        {
            ComboMessage = "A chord cannot use the same button twice";
            return;
        }

        var repeat = AvailableMacroRepeatModes[Math.Clamp(NewComboRepeatIndex, 0, AvailableMacroRepeatModes.Count - 1)];
        if (!MacroDefinition.TryParseScript(NewComboAction, repeat, out var macro, out var macroError))
        {
            ComboMessage = macroError;
            return;
        }

        var combo = new ComboMapping
        {
            Kind = kind,
            Buttons = buttons,
            Binding = new ActivatorBinding { Target = new RemapTarget { Type = TargetType.Macro, Macro = macro } }
        };
        lock (_mappingEngine.SyncRoot) SelectedProfile.Combos.Add(combo);
        Combos.Add(combo);
        NewComboButtons = string.Empty;
        NewComboAction = string.Empty;
        ComboMessage = $"Added {combo.Description}";
    }

    [RelayCommand]
    private void RemoveCombo(ComboMapping? combo)
    {
        if (combo == null) return;
        lock (_mappingEngine.SyncRoot) SelectedProfile.Combos.RemoveAll(c => c.Id == combo.Id);
        Combos.Remove(combo);
        ComboMessage = $"Removed {combo.Description}";
    }

    // ---- Config files: delete, import, export, and the history that undoes them -------------------------------------

    public ObservableCollection<HistoryEntry> HistoryEntries { get; } = new();

    [ObservableProperty]
    private string _historyHint = "Copies of configs are kept here before they are deleted or replaced, so you can bring them back.";

    private void RefreshHistory()
    {
        HistoryEntries.Clear();
        foreach (var entry in _history.List()) HistoryEntries.Add(entry);
    }

    private string SavedPathFor(Profile profile)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        string fileName = string.Concat(profile.Name.Select(c => invalid.Contains(c) ? '_' : c)) + ".json";
        return System.IO.Path.Combine(_profilesDirectory, fileName);
    }

    // The file about to be overwritten is kept first, unless the save changes nothing
    private void KeepCopyOfSavedFile(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return;
            var before = ProfileSerializer.LoadFromFile(path);
            if (ProfileSerializer.ToJson(before) == ProfileSerializer.ToJson(SelectedProfile)) return;
            _history.Snapshot(before, "replaced by a save");
        }
        catch (Exception)
        {
            // An unreadable old file just means there is nothing to keep
        }
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        var doomed = SelectedProfile;
        _history.Snapshot(doomed, "deleted");

        // Move the selection off the doomed config first: removing the selected item would leave the list with nothing selected
        int index = Profiles.IndexOf(doomed);
        if (Profiles.Count == 1) Profiles.Add(Profile.CreateDefault("Default Controller Profile"));
        var next = Profiles.Where(p => !ReferenceEquals(p, doomed)).ElementAt(Math.Min(index, Profiles.Count - 2));
        _manualProfile = next;
        SelectedProfile = next;
        Profiles.Remove(doomed);
        try
        {
            string path = SavedPathFor(doomed);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch (Exception)
        {
            // If the file cannot be removed the config comes back next start; the copy in History is still there
        }
        UpdateForegroundWatching();
        RefreshHistory();
        StatusMessage = $"Deleted {doomed.Name} — bring it back from History in Preferences";
    }

    [RelayCommand]
    private async Task ExportProfile()
    {
        string? path = await _ui.PickSaveFileAsync("Export this config", System.IO.Path.GetFileName(SavedPathFor(SelectedProfile)));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            lock (_mappingEngine.SyncRoot) ProfileSerializer.SaveToFile(SelectedProfile, path);
            StatusMessage = $"Exported {SelectedProfile.Name} to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not export: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportProfile()
    {
        string? path = await _ui.PickOpenFileAsync("Import a config");
        if (string.IsNullOrEmpty(path)) return;

        Profile imported;
        try
        {
            imported = ProfileSerializer.FromJson(System.IO.File.ReadAllText(path));
            if (!imported.ShiftLayers.ContainsKey(0)) throw new FormatException("it has no main layer");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or FormatException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not import {System.IO.Path.GetFileName(path)}: {ex.Message}";
            return;
        }

        // A config from somebody else can run commands and contact servers: show what, and ask first
        var risky = ProfileSafety.FindRiskyActions(imported);
        if (risky.Count > 0)
        {
            string list = string.Join("\n", risky.Take(12)) + (risky.Count > 12 ? $"\n…and {risky.Count - 12} more" : "");
            bool allowed = await _ui.ConfirmAsync("This config can run things on your computer",
                $"\"{imported.Name}\" does more than press keys. When you press the buttons it is set up for, it will:\n\n{list}\n\nOnly continue if you trust where this config came from.");
            if (!allowed)
            {
                StatusMessage = "Import cancelled";
                return;
            }
        }

        imported.Id = Guid.NewGuid();
        string baseName = string.IsNullOrWhiteSpace(imported.Name) ? "Imported config" : imported.Name;
        string name = baseName;
        for (int n = 2; Profiles.Any(p => p.Name == name); n++) name = $"{baseName} ({n})";
        imported.Name = name;

        Profiles.Add(imported);
        SelectedProfile = imported;
        UpdateForegroundWatching();
        StatusMessage = $"Imported {name}";
    }

    [RelayCommand]
    private void RestoreHistoryEntry(HistoryEntry? entry)
    {
        if (entry == null) return;
        var restored = _history.Load(entry);
        if (restored == null)
        {
            StatusMessage = "That copy could not be read";
            RefreshHistory();
            return;
        }

        var existing = Profiles.FirstOrDefault(p => p.Id == restored.Id);
        if (existing != null)
        {
            _history.Snapshot(existing, "replaced by a restore");   // restoring is itself undoable
            if (ReferenceEquals(_manualProfile, existing)) _manualProfile = restored;
            Profiles.Insert(Profiles.IndexOf(existing), restored);
            SelectedProfile = restored;                 // off the old one before it goes, so the list is never left with nothing selected
            Profiles.Remove(existing);
        }
        else
        {
            if (Profiles.Any(p => p.Name == restored.Name)) restored.Name += " (restored)";
            Profiles.Add(restored);
        }

        SelectedProfile = restored;
        UpdateForegroundWatching();
        RefreshHistory();
        StatusMessage = $"Restored {restored.Name}";
    }

    private void PopulateInspectorOptions() => BuildRemapOptions();

    /// <summary>Re-reads the connected controllers, as happens when one is plugged in or out.</summary>
    internal void RefreshDevicesForTests() => PopulateConnectedDevices();

    private void PopulateConnectedDevices()
    {
        ConnectedDevices.Clear();
        var slots = _reader.ConnectedSlots();
        foreach (var slot in slots)
        {
            string tag = slot == _reader.ExcludedUserIndex ? " (virtual output)" : "";
            ConnectedDevices.Add($"🎮 {_reader.DescribeSlot(slot)}{tag}");
        }
        if (slots.Count == 0) ConnectedDevices.Add(Platform.NoControllerText);

        AutoSelectSkin();
    }

    // ---- Macro step helpers -----------------------------------------------------------------------------------------

    /// <summary>A ready-made step the macro editor can add, with a placeholder to overwrite.</summary>
    public sealed record MacroStepTemplate(string Label, string Script);

    public IReadOnlyList<MacroStepTemplate> MacroStepTemplates { get; } = new MacroStepTemplate[]
    {
        new("Open web page", "OpenUrl:https://example.com"),
        new("Launch app", OperatingSystem.IsWindows() ? "LaunchApp:notepad.exe" : "LaunchApp:Safari"),
        new("Run command", OperatingSystem.IsWindows() ? "Shell:echo hello > %TEMP%\\hello.txt" : "Shell:say hello"),
        new("Webhook", "Webhook:POST https://example.com/hook {\"pressed\":true}"),
        new("OBS scene", "Obs:Scene Gaming"),
        new("OBS record", "Obs:record"),
        new("Type text", "TypeText:Hello!"),
        new("Wait", "Delay:500"),
    };

    /// <summary>What the macro editor accepts, shown under the box.</summary>
    public string MacroHelpText { get; } =
        "Steps are separated by semicolons. Keys and mouse: KeyDown / KeyUp / KeyTap:W, MouseClick:Left Click, Wheel:1, MouseMove:up:20, Delay:100. " +
        "Outside the keyboard: OpenUrl:https://..., LaunchApp:Safari, Shell:command, TypeText:hello, " +
        "Webhook:POST https://... {json body}, Obs:Scene Name (or Obs:record, Obs:stream, Obs:mute Mic). Write \\; for a semicolon inside text.";

    [RelayCommand]
    private void UseScriptExample(ScriptExample? example)
    {
        if (example != null) ScriptCode = example.Code;
    }

    [RelayCommand]
    private void AddMacroStep(MacroStepTemplate? template)
    {
        if (template == null) return;
        string current = (MacroScript ?? string.Empty).Trim().TrimEnd(';').TrimEnd();
        MacroScript = current.Length == 0 ? template.Script : current + "; " + template.Script;
    }

    // ---- Profiles that follow the app in front ------------------------------------------------------------------------

    /// <summary>Switch to the profile linked to the application in front (see "Target process"), and back when it is left.</summary>
    [ObservableProperty]
    private bool _autoSwitchProfiles = true;

    /// <summary>True on a platform that can tell which application is in front.</summary>
    public bool CanAutoSwitch => _foreground != null;

    private string AutoSwitchSettingPath => System.IO.Path.Combine(_profilesDirectory, "autoswitch.txt");

    partial void OnAutoSwitchProfilesChanged(bool value)
    {
        if (!_constructed) return;
        try
        {
            System.IO.Directory.CreateDirectory(_profilesDirectory);
            System.IO.File.WriteAllText(AutoSwitchSettingPath, value ? "on" : "off");
        }
        catch (Exception)
        {
            // Not being able to remember the setting must not get in the way
        }
        UpdateForegroundWatching();
        if (!value) ReturnToManualProfile();
    }

    private void LoadAutoSwitchSetting()
    {
        try
        {
            if (System.IO.File.Exists(AutoSwitchSettingPath)) AutoSwitchProfiles = !System.IO.File.ReadAllText(AutoSwitchSettingPath).Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // An unreadable file just means the default
        }
        UpdateForegroundWatching();
    }

    // Look twice a second, and only while at least one profile is linked to an app: a plain setup never asks the system anything
    private void UpdateForegroundWatching()
    {
        bool wanted = _foreground != null && AutoSwitchProfiles && Profiles.Any(p => !string.IsNullOrWhiteSpace(p.ProcessExecutable));
        if (wanted && _foregroundTimer == null)
        {
            _foregroundTimer = new System.Threading.Timer(_ => _ui.Post(CheckForeground), null, 500, 500);
        }
        else if (!wanted && _foregroundTimer != null)
        {
            _foregroundTimer.Dispose();
            _foregroundTimer = null;
        }
    }

    /// <summary>Looks at the application in front and switches to the profile linked to it. Called by the timer; public so it can be tested.</summary>
    public void CheckForeground()
    {
        if (_foreground == null || !AutoSwitchProfiles) return;

        ForegroundApp? app;
        try
        {
            app = _foreground();
        }
        catch (Exception)
        {
            return;
        }

        var manual = _manualProfile ?? SelectedProfile;
        var chosen = AppMatcher.Choose(Profiles, app, Environment.ProcessId, manual, SelectedProfile);
        if (chosen == null || ReferenceEquals(chosen, SelectedProfile)) return;

        _autoSwitching = true;
        try
        {
            SelectedProfile = chosen;
        }
        finally
        {
            _autoSwitching = false;
        }
        StatusMessage = ReferenceEquals(chosen, manual) ? $"Back to {chosen.Name}" : $"Switched to {chosen.Name} for {app?.Name}";
    }

    private void ReturnToManualProfile()
    {
        if (_manualProfile == null || ReferenceEquals(_manualProfile, SelectedProfile) || !Profiles.Contains(_manualProfile)) return;
        _autoSwitching = true;
        try
        {
            SelectedProfile = _manualProfile;
        }
        finally
        {
            _autoSwitching = false;
        }
    }

    // ---- Controller lock --------------------------------------------------------------------------------------------

    public static IReadOnlyList<string> LockChordNames { get; } = new[]
    {
        "Off", "View + Menu", "Guide", "Both stick clicks", "Both bumpers + View"
    };

    private static readonly ControllerButton[][] LockChords =
    {
        Array.Empty<ControllerButton>(),
        new[] { ControllerButton.View, ControllerButton.Menu },
        new[] { ControllerButton.Guide },
        new[] { ControllerButton.LeftStickClick, ControllerButton.RightStickClick },
        new[] { ControllerButton.LeftBumper, ControllerButton.RightBumper, ControllerButton.View }
    };

    [ObservableProperty]
    private int _lockChordIndex;

    /// <summary>The controller is locked: nothing it does has any effect until it is unlocked.</summary>
    [ObservableProperty]
    private bool _isControllerLocked;

    partial void OnLockChordIndexChanged(int value)
    {
        if (_loadingProfile || value < 0 || value >= LockChords.Length) return;
        lock (_mappingEngine.SyncRoot) SelectedProfile.LockChord = LockChords[value].ToList();
    }

    [RelayCommand]
    private void ToggleLock() => _mappingEngine.SetLocked(!_mappingEngine.IsLocked);

    private void OnEngineLockChanged(bool locked)
    {
        IsControllerLocked = locked;
        StatusMessage = locked ? "Controller locked — hold the lock buttons again to unlock" : "Controller unlocked";
    }

    private bool _loadingProfile;

    private void LoadLockChord(Profile profile)
    {
        _loadingProfile = true;
        try
        {
            int index = Array.FindIndex(LockChords, c => c.SequenceEqual(profile.LockChord ?? new List<ControllerButton>()));
            LockChordIndex = Math.Max(0, index);
        }
        finally
        {
            _loadingProfile = false;
        }
    }

    // ---- OBS connection ---------------------------------------------------------------------------------------------

    /// <summary>Where the Obs macro step connects. Kept in this computer's settings, never in a shareable profile.</summary>
    [ObservableProperty]
    private string _obsHost = "localhost";

    [ObservableProperty]
    private int _obsPort = 4455;

    [ObservableProperty]
    private string _obsPassword = string.Empty;

    private string ObsSettingsPath => System.IO.Path.Combine(_profilesDirectory, "obs-settings.cfg");

    partial void OnObsHostChanged(string value) => SaveObsSettings();
    partial void OnObsPortChanged(int value) => SaveObsSettings();
    partial void OnObsPasswordChanged(string value) => SaveObsSettings();

    private void SaveObsSettings()
    {
        if (!_constructed) return;
        var settings = new ObsSettings { Host = string.IsNullOrWhiteSpace(ObsHost) ? "localhost" : ObsHost.Trim(), Port = ObsPort is > 0 and < 65536 ? ObsPort : 4455, Password = ObsPassword ?? string.Empty };
        SharedObs.Host = settings.Host;
        SharedObs.Port = settings.Port;
        SharedObs.Password = settings.Password;
        try
        {
            System.IO.Directory.CreateDirectory(_profilesDirectory);
            System.IO.File.WriteAllText(ObsSettingsPath, System.Text.Json.JsonSerializer.Serialize(settings));
        }
        catch (Exception)
        {
            // Not being able to remember the address must not get in the way
        }
    }

    private void LoadObsSettings()
    {
        try
        {
            if (System.IO.File.Exists(ObsSettingsPath) &&
                System.Text.Json.JsonSerializer.Deserialize<ObsSettings>(System.IO.File.ReadAllText(ObsSettingsPath)) is { } saved)
            {
                ObsHost = saved.Host;
                ObsPort = saved.Port;
                ObsPassword = saved.Password;
            }
        }
        catch (Exception)
        {
            // An unreadable file just means the defaults
        }
        SharedObs.Host = ObsHost;
        SharedObs.Port = ObsPort;
        SharedObs.Password = ObsPassword;
    }

    // ---- Updates --------------------------------------------------------------------------------------------------

    /// <summary>What the last look for a newer build found, in words for the person using the app.</summary>
    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    /// <summary>A newer build is waiting.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateButton))]
    private bool _isUpdateAvailable;

    /// <summary>The update is downloading or being swapped in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateButton))]
    private bool _isUpdating;

    /// <summary>Download progress, 0 to 1.</summary>
    [ObservableProperty]
    private double _updateProgress;

    /// <summary>Whether the app looks for a newer build by itself when it starts.</summary>
    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    public bool ShowUpdateButton => IsUpdateAvailable && !IsUpdating;

    private string UpdateSettingPath => System.IO.Path.Combine(_profilesDirectory, "updates.txt");

    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        if (!_constructed) return;
        try
        {
            System.IO.Directory.CreateDirectory(_profilesDirectory);
            System.IO.File.WriteAllText(UpdateSettingPath, value ? "on" : "off");
        }
        catch (Exception)
        {
            // Not being able to remember the setting must never get in the way of using the app
        }
    }

    private void LoadUpdateSetting()
    {
        try
        {
            if (System.IO.File.Exists(UpdateSettingPath)) AutoCheckUpdates = !System.IO.File.ReadAllText(UpdateSettingPath).Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // An unreadable file just means the default
        }
    }

    private async Task CheckAfterStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(4)); // let the window appear first
        await RunUpdateCheckAsync(manual: false);
    }

    [RelayCommand]
    private Task CheckForUpdates() => RunUpdateCheckAsync(manual: true);

    private async Task RunUpdateCheckAsync(bool manual)
    {
        if (IsUpdating) return;
        if (manual) UpdateStatusText = "Looking for updates…";

        UpdateCheck result;
        try
        {
            result = await _updates.CheckAsync(BuildInfo.Revision);
        }
        catch (Exception ex)
        {
            result = new UpdateCheck.Unknown(ex.Message);
        }

        _ui.Post(() =>
        {
            switch (result)
            {
                case UpdateCheck.Available a:
                    _pendingUpdate = a.Update;
                    IsUpdateAvailable = true;
                    UpdateStatusText = $"A newer build is ready ({a.Update.Revision}). You have {BuildInfo.Text}.";
                    if (!manual) StatusMessage = $"Update available: build {a.Update.Revision}";
                    break;
                case UpdateCheck.UpToDate:
                    _pendingUpdate = null;
                    IsUpdateAvailable = false;
                    UpdateStatusText = "You have the latest build.";
                    break;
                case UpdateCheck.Unknown u:
                    if (manual) UpdateStatusText = "Could not check for updates: " + u.Reason;
                    break;
            }
        });
    }

    /// <summary>Downloads the waiting update, swaps it in for this app, and restarts.</summary>
    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (_pendingUpdate is not { } update || IsUpdating) return;

        if (!_installer.CanSelfUpdate)
        {
            UpdateStatusText = "This copy was not made by the build script, so it cannot replace itself. Pull the latest and run the build script (build.sh or Build.cmd) instead.";
            return;
        }

        IsUpdating = true;
        UpdateProgress = 0;
        UpdateStatusText = "Downloading the update…";
        try
        {
            var progress = new Progress<double>(p => _ui.Post(() => UpdateProgress = p));
            string file = await _installer.DownloadAsync(update, progress);

            UpdateStatusText = "Installing… ReWASD will restart.";
            Shutdown(); // stop remapping and give every key back before the app closes
            _installer.Install(file);
            _ui.RequestExit();
        }
        catch (Exception ex)
        {
            IsUpdating = false;
            UpdateStatusText = "The update could not be installed: " + ex.Message;
        }
    }

    // ---- Surface: controller picture, keyboard and mouse ---------------------------------------------------------

    [ObservableProperty]
    private SkinKind _selectedSkin = SkinKind.Xbox;

    [ObservableProperty]
    private ObservableCollection<DecorShape> _surfaceDecor = new();

    /// <summary>How much larger than the shared drawing area the current picture is drawn. The mouse is a small shape, so it is enlarged.</summary>
    [ObservableProperty]
    private double _surfaceScale = 1.0;

    public IReadOnlyList<SkinOption> SkinOptions => ControllerSkins.Options;

    /// <summary>The detailed animated drawing of the chosen controller, if it has one (front ends that cannot show it use the outline).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkinArt))]
    [NotifyPropertyChangedFor(nameof(OverlayHasArt))]
    private ArtScene? _skinArt;

    /// <summary>True when the detailed drawing replaces the outline: the controller tab is open and the skin has one.</summary>
    public bool ShowSkinArt => SkinArt != null && SelectedInputTab == InputTab.Gamepad;

    partial void OnSelectedSkinChanged(SkinKind value)
    {
        if (!_autoSkin)
        {
            _skinPinned = true; // the person chose this picture, so a different controller plugged in later won't change it
            SaveSkinChoice(value);
        }
        ApplySkin(value);
    }

    // The picture is remembered between runs in a small file next to the saved configs (which only read *.json, so it is ignored by them)
    private string SkinChoicePath => System.IO.Path.Combine(_profilesDirectory, "skin.txt");

    private void SaveSkinChoice(SkinKind kind)
    {
        if (!_constructed) return;
        try
        {
            System.IO.Directory.CreateDirectory(_profilesDirectory);
            System.IO.File.WriteAllText(SkinChoicePath, kind.ToString());
        }
        catch (Exception)
        {
            // Not being able to remember the choice must never get in the way of using the app
        }
    }

    private void LoadSkinChoice()
    {
        try
        {
            if (System.IO.File.Exists(SkinChoicePath) && Enum.TryParse<SkinKind>(System.IO.File.ReadAllText(SkinChoicePath).Trim(), out var kind) && Enum.IsDefined(kind))
            {
                _skinPinned = true; // also pin when the saved choice equals the default and the setter does not run
                SelectedSkin = kind;
            }
        }
        catch (Exception)
        {
            // An unreadable file just means the default picture
        }
    }

    private bool _skinPinned;
    private bool _autoSkin;
    private bool _constructed;

    /// <summary>Shows the picture that matches the connected controller (a DualSense gets the PS5 drawing), unless one was chosen by hand.</summary>
    private void AutoSelectSkin()
    {
        if (!_constructed || _skinPinned) return;

        var slots = _reader.ConnectedSlots();
        if (slots.Count == 0) return;

        int slot = slots.Contains(_reader.ActiveUserIndex) ? _reader.ActiveUserIndex : slots[0];
        if (ControllerSkins.GuessFromName(_reader.DescribeSlot(slot)) is not { } kind || kind == SelectedSkin) return;

        _autoSkin = true;
        try
        {
            SelectedSkin = kind;
        }
        finally
        {
            _autoSkin = false;
        }
    }

    private static string FriendlyPadName(ControllerButton b) =>
        System.Text.RegularExpressions.Regex.Replace(b.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

    private void InitializeControllerButtons()
    {
        ControllerButtons.Clear();

        foreach (var button in ControllerSkins.Get(SkinKind.Xbox).Layout.Keys)
        {
            ControllerButtons.Add(new ButtonViewModel
            {
                Kind = SourceKind.Pad,
                Button = button,
                SourceName = button.ToString(),
                FriendlyName = FriendlyPadName(button),
                HasStick = button is ControllerButton.LeftStickClick or ControllerButton.RightStickClick,
                OnSelected = SelectButton
            });
        }

        CurrentSources = ControllerButtons;
        ApplySkin(SelectedSkin);
    }

    /// <summary>Re-lays out the controller drawing, relabels its buttons and the dropdown icons for the chosen picture.</summary>
    private void ApplySkin(SkinKind kind)
    {
        var skin = ControllerSkins.Get(kind);
        SkinArt = skin.Art;

        foreach (var vm in ControllerButtons)
        {
            var p = skin.Layout[vm.Button];
            var face = ControllerSkins.Face(kind, vm.Button);

            vm.Width = p.W;
            vm.Height = p.H;
            vm.X = p.Cx - p.W / 2;
            vm.Y = p.Cy - p.H / 2;
            vm.DisplayName = face.Legend;
            vm.LegendColor = face.Color;
            vm.CornerRadius = face.Shape == IconShape.Circle ? Math.Min(p.W, p.H) / 2 : p.H / 2;
            vm.LegendFontSize = face.Legend.Length == 1 ? 15 : face.Legend.Length > 3 ? 10 : 12;
        }

        foreach (var option in RemapOptions.Where(o => o.Kind == RemapKind.Pad))
        {
            var face = ControllerSkins.Face(kind, option.Pad);
            option.Legend = face.Legend;
            option.LegendColor = face.Color;
            option.Shape = face.Shape;
        }

        if (SelectedInputTab == InputTab.Gamepad) SurfaceDecor = new ObservableCollection<DecorShape>(skin.Decor);
        UpdateAllButtonDisplays();
    }

    private ButtonViewModel MakeSource(SourceKind kind, SourcePlacement s)
    {
        var vm = new ButtonViewModel
        {
            Kind = kind,
            SourceName = s.Name,
            FriendlyName = s.Name,
            DisplayName = s.Legend,
            CornerRadius = s.Radius,
            LegendFontSize = s.FontSize,
            OnSelected = SelectButton
        };

        if (s.ShapeData != null)
        {
            // A shaped source (part of the mouse body, say) covers the whole drawing and is hit-tested by its outline
            vm.ShapeData = s.ShapeData;
            vm.ClipData = s.ClipData;
            vm.X = 0;
            vm.Y = 0;
            vm.Width = ControllerSkins.CanvasWidth;
            vm.Height = ControllerSkins.CanvasHeight;
            vm.LegendLeft = s.Place.Cx - 40; // the legend box is 80 wide and centred on the legend position
            vm.LegendTop = s.Place.Cy - s.FontSize * 0.7;
        }
        else
        {
            vm.X = s.Place.Cx - s.Place.W / 2;
            vm.Y = s.Place.Cy - s.Place.H / 2;
            vm.Width = s.Place.W;
            vm.Height = s.Place.H;
        }

        return vm;
    }

    private void InitializeSourcePickers()
    {
        foreach (var key in InputLayouts.Keyboard) KeyboardSources.Add(MakeSource(SourceKind.Key, key));
        foreach (var button in InputLayouts.Mouse) MouseSources.Add(MakeSource(SourceKind.Mouse, button));
        UpdateAllButtonDisplays();
    }

    partial void OnSelectedInputTabChanged(InputTab value)
    {
        CurrentSources = value switch
        {
            InputTab.Keyboard => KeyboardSources,
            InputTab.Mouse => MouseSources,
            _ => ControllerButtons
        };

        IEnumerable<DecorShape> decor = value switch
        {
            InputTab.Keyboard => InputLayouts.KeyboardDecor,
            InputTab.Mouse => InputLayouts.MouseDecor,
            _ => ControllerSkins.Get(SelectedSkin).Decor
        };
        SurfaceDecor = new ObservableCollection<DecorShape>(decor);
        SurfaceScale = value == InputTab.Mouse ? 1.2 : 1.0;

        var first = value == InputTab.Gamepad
            ? CurrentSources.FirstOrDefault(b => b.Button == ControllerButton.A)
            : CurrentSources.FirstOrDefault();
        if (first != null) SelectButton(first);
    }

    // ---- The "Remap to" dropdown ---------------------------------------------------------------------------------

    private bool _syncingOption;

    public ObservableCollection<RemapOption> RemapOptions { get; } = new();

    /// <summary>The options grouped for toolkits that can group a list (WPF); null elsewhere, which uses <see cref="RemapGroups"/>.</summary>
    public System.Collections.IEnumerable? RemapOptionsView { get; private set; }

    /// <summary>The same options, already split into their groups (Controller, Keyboard, Mouse, Layers).</summary>
    public IReadOnlyList<RemapGroup> RemapGroups { get; private set; } = Array.Empty<RemapGroup>();

    [ObservableProperty]
    private RemapOption? _selectedRemapOption;

    /// <summary>Whether the grouped "Remap to" menu is open (toolkits without a grouped drop-down draw their own).</summary>
    [ObservableProperty]
    private bool _isRemapMenuOpen;

    [RelayCommand]
    private void ChooseRemapOption(RemapOption? option)
    {
        if (option != null) SelectedRemapOption = option;
        IsRemapMenuOpen = false;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMacroMode))]
    [NotifyPropertyChangedFor(nameof(IsRemapMode))]
    [NotifyPropertyChangedFor(nameof(IsScriptMode))]
    private TargetMode _targetMode = TargetMode.Remap;

    public bool IsMacroMode => TargetMode == TargetMode.Macro;
    public bool IsScriptMode => TargetMode == TargetMode.Script;

    /// <summary>The JavaScript of the Script editor.</summary>
    [ObservableProperty]
    private string _scriptCode = string.Empty;

    [ObservableProperty]
    private string _scriptError = string.Empty;

    public sealed record ScriptExample(string Label, string Code);

    public IReadOnlyList<ScriptExample> ScriptExamples { get; } = new ScriptExample[]
    {
        new("Rapid click", "// Use with Repeat while held: clicks as fast as you like while the button is down\nclick();\ndelay(40);"),
        new("Type a line", "type('Hello from my controller!');\npress('Enter');"),
        new("Count presses", "// state keeps its values between presses\nstate.count = (state.count || 0) + 1;\nnotify('Pressed ' + state.count + ' times');"),
        new("Only in one app", "if (app.is('chrome') || app.is('Safari')) {\n  press('F5');       // refresh the page\n} else {\n  notify('Not in a browser (' + app.name + ')');\n}"),
        new("Open and wait", "openURL('https://example.com');\ndelay(1500);\npress('F11');"),
        new("Ping a server", "webhook('POST', 'https://example.com/hook', { pressed: true, app: app.name });"),
    };
    public bool IsRemapMode => TargetMode == TargetMode.Remap;

    private void BuildRemapOptions()
    {
        const string Glyph = "";

        RemapOptions.Add(new RemapOption(RemapKind.Nothing, "Nothing", "Nothing (disable this input)") { Legend = "", Shape = IconShape.Glyph });

        // Without a virtual controller (macOS) a mapping to a pad button has nowhere to go, so the group says so
        string padGroup = SupportsVirtualController ? "Controller" : "Controller (Windows only)";
        foreach (var b in ControllerSkins.Get(SkinKind.Xbox).Layout.Keys)
        {
            RemapOptions.Add(new RemapOption(RemapKind.Pad, padGroup, FriendlyPadName(b)) { Pad = b });
        }

        foreach (var key in InputLayouts.Keyboard)
        {
            RemapOptions.Add(new RemapOption(RemapKind.Key, "Keyboard", key.Name) { Name = key.Name, Legend = key.Legend, Shape = IconShape.KeyCap });
        }

        foreach (var name in new[] { "Left Click", "Right Click", "Middle Click", "Mouse Button 4", "Mouse Button 5" })
        {
            RemapOptions.Add(new RemapOption(RemapKind.Mouse, "Mouse", name) { Name = name, Legend = Glyph, Shape = IconShape.Glyph });
        }
        RemapOptions.Add(new RemapOption(RemapKind.Scroll, "Mouse", ScrollUpLabel) { Number = 1, Legend = "", Shape = IconShape.Glyph });
        RemapOptions.Add(new RemapOption(RemapKind.Scroll, "Mouse", ScrollDownLabel) { Number = -1, Legend = "", Shape = IconShape.Glyph });

        for (int i = 0; i < LayerNames.Count; i++)
        {
            RemapOptions.Add(new RemapOption(RemapKind.Layer, "Layers", "Switch to " + LayerNames[i]) { Number = i, Legend = "", Shape = IconShape.Glyph });
        }

        RemapOptionsView = _ui.CreateGroupedView(RemapOptions, nameof(RemapOption.Group));
        RemapGroups = RemapOptions.GroupBy(o => o.Group).Select(g => new RemapGroup(g.Key, g.ToList())).ToList();
        SelectedRemapOption = RemapOptions[0];
    }

    private RemapOption? FindOption(RemapTarget t) => t.Type switch
    {
        TargetType.None => RemapOptions.FirstOrDefault(o => o.Kind == RemapKind.Nothing),
        TargetType.VirtualControllerButton => RemapOptions.FirstOrDefault(o => o.Kind == RemapKind.Pad && o.Pad == t.VirtualButton),
        TargetType.KeyboardKey => RemapOptions.FirstOrDefault(o => o.Kind == RemapKind.Key && string.Equals(o.Name, t.KeyboardKey, StringComparison.OrdinalIgnoreCase)),
        TargetType.MouseButton => RemapOptions.FirstOrDefault(o => o.Kind == RemapKind.Mouse && string.Equals(o.Name, t.MouseButton, StringComparison.OrdinalIgnoreCase)),
        TargetType.MouseScroll => RemapOptions.FirstOrDefault(o => o.Kind == RemapKind.Scroll && o.Number == (t.ScrollDelta < 0 ? -1 : 1)),
        TargetType.JumpToLayer => RemapOptions.FirstOrDefault(o => o.Kind == RemapKind.Layer && o.Number == t.Layer),
        _ => null
    };

    /// <summary>Copies a picked dropdown choice into the fields the save command reads.</summary>
    private void ApplyOption(RemapOption option)
    {
        switch (option.Kind)
        {
            case RemapKind.Nothing:
                SelectedTargetType = TargetType.None;
                break;
            case RemapKind.Pad:
                SelectedTargetType = TargetType.VirtualControllerButton;
                SelectedVirtualButton = option.Pad;
                break;
            case RemapKind.Key:
                SelectedTargetType = TargetType.KeyboardKey;
                SelectedKeyboardKey = option.Name;
                break;
            case RemapKind.Mouse:
                SelectedTargetType = TargetType.MouseButton;
                SelectedMouseButton = option.Name;
                break;
            case RemapKind.Scroll:
                SelectedTargetType = TargetType.MouseScroll;
                SelectedScrollDirection = option.Number < 0 ? ScrollDownLabel : ScrollUpLabel;
                break;
            case RemapKind.Layer:
                SelectedTargetType = TargetType.JumpToLayer;
                SelectedJumpLayer = option.Number;
                break;
        }
    }

    partial void OnSelectedRemapOptionChanged(RemapOption? value)
    {
        if (_syncingOption || value == null || IsMacroMode || IsScriptMode) return;
        ApplyOption(value);
    }

    partial void OnTargetModeChanged(TargetMode value)
    {
        if (_syncingOption) return;

        if (value == TargetMode.Macro || value == TargetMode.Script) SelectedTargetType = TargetType.Macro;
        else if (SelectedRemapOption != null) ApplyOption(SelectedRemapOption);
    }

    // ---- Mapping access ------------------------------------------------------------------------------------------
    // A source is a pad button, a key or a mouse button. Shift layers inherit from Main unless they override.

    private ShiftLayer CurrentLayer => SelectedProfile.ShiftLayers.TryGetValue(ActiveShiftLayer, out var l) ? l : SelectedProfile.ShiftLayers[0];

    private static ButtonMapping? Lookup(ShiftLayer layer, ButtonViewModel src) => src.Kind switch
    {
        SourceKind.Pad => layer.Mappings.GetValueOrDefault(src.Button),
        SourceKind.Key => layer.KeyboardMappings.GetValueOrDefault(src.SourceName),
        _ => layer.MouseMappings.GetValueOrDefault(src.SourceName)
    };

    private ButtonMapping? FindMapping(ShiftLayer layer, ButtonViewModel src, bool inherit)
    {
        var m = Lookup(layer, src);
        if (m == null && inherit && layer.LayerIndex != 0 && SelectedProfile.ShiftLayers.TryGetValue(0, out var main)) m = Lookup(main, src);
        return m;
    }

    private static void Store(ShiftLayer layer, ButtonViewModel src, ButtonMapping mapping)
    {
        switch (src.Kind)
        {
            case SourceKind.Pad: layer.Mappings[src.Button] = mapping; break;
            case SourceKind.Key: layer.KeyboardMappings[src.SourceName] = mapping; break;
            default: layer.MouseMappings[src.SourceName] = mapping; break;
        }
    }

    private static void Remove(ShiftLayer layer, ButtonViewModel src)
    {
        switch (src.Kind)
        {
            case SourceKind.Pad: layer.Mappings.Remove(src.Button); break;
            case SourceKind.Key: layer.KeyboardMappings.Remove(src.SourceName); break;
            default: layer.MouseMappings.Remove(src.SourceName); break;
        }
    }

    /// <summary>What a source does when nothing is configured: pass itself through.</summary>
    private static RemapTarget DefaultTarget(ButtonViewModel src) => src.Kind switch
    {
        SourceKind.Pad => RemapTarget.Default(src.Button),
        SourceKind.Key => new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = src.SourceName },
        _ => src.SourceName switch
        {
            "Wheel Up" => new RemapTarget { Type = TargetType.MouseScroll, ScrollDelta = 1 },
            "Wheel Down" => new RemapTarget { Type = TargetType.MouseScroll, ScrollDelta = -1 },
            _ => new RemapTarget { Type = TargetType.MouseButton, MouseButton = src.SourceName }
        }
    };

    private static bool IsDefaultMapping(ButtonMapping m, ButtonViewModel src)
    {
        if (m.Activators.Count > 0 || m.Mode != MappingMode.Normal) return false;
        var d = DefaultTarget(src);
        var t = m.Target;
        return t.Type == d.Type
            && t.VirtualButton == d.VirtualButton
            && string.Equals(t.KeyboardKey, d.KeyboardKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.MouseButton, d.MouseButton, StringComparison.OrdinalIgnoreCase)
            && (t.Type != TargetType.MouseScroll || t.ScrollDelta == d.ScrollDelta);
    }

    private IEnumerable<ButtonViewModel> AllSources => ControllerButtons.Concat(KeyboardSources).Concat(MouseSources);

    public void SelectButton(ButtonViewModel button)
    {
        foreach (var b in AllSources) b.IsSelected = false;
        button.IsSelected = true;
        SelectedButton = button;

        var mapping = FindMapping(CurrentLayer, button, inherit: true);
        var binding = mapping?.GetBinding(SelectedActivator);

        // Unconfigured Single shows the pass-through; an unconfigured other activator starts out empty
        if (binding == null && SelectedActivator == ActivatorType.Single)
        {
            binding = new ActivatorBinding { Target = DefaultTarget(button) };
        }

        var target = binding?.Target ?? new RemapTarget { Type = TargetType.None };
        SelectedTargetType = target.Type;
        SelectedVirtualButton = target.VirtualButton != ControllerButton.None ? target.VirtualButton : (button.Kind == SourceKind.Pad ? button.Button : ControllerButton.A);
        SelectedKeyboardKey = !string.IsNullOrEmpty(target.KeyboardKey) ? target.KeyboardKey : "Space";
        SelectedMouseButton = !string.IsNullOrEmpty(target.MouseButton) ? target.MouseButton : "Left Click";
        SelectedScrollDirection = target.ScrollDelta < 0 ? ScrollDownLabel : ScrollUpLabel;
        SelectedJumpLayer = target.Layer;
        bool isScript = target.Macro?.SingleScript != null;
        MacroScript = isScript ? string.Empty : target.Macro?.ToScript() ?? string.Empty;
        ScriptCode = target.Macro?.SingleScript ?? string.Empty;
        ScriptError = string.Empty;
        SelectedMacroRepeat = target.Macro?.RepeatMode ?? MacroRepeatMode.Once;
        MacroError = string.Empty;
        SelectedMode = binding?.Mode ?? MappingMode.Normal;
        TurboRateMs = binding?.TurboRateMs ?? 50;
        IsMuted = mapping?.IsMuted ?? false;

        // Show the same thing in the Remap dropdown and the Remap/Macro switch (without re-applying it)
        _syncingOption = true;
        try
        {
            SelectedRemapOption = FindOption(target.Type == TargetType.Macro ? DefaultTarget(button) : target) ?? RemapOptions[0];
            TargetMode = target.Type == TargetType.Macro ? (isScript ? TargetMode.Script : TargetMode.Macro) : TargetMode.Remap;
        }
        finally
        {
            _syncingOption = false;
        }
    }

    partial void OnSelectedActivatorChanged(ActivatorType value)
    {
        if (SelectedButton != null) SelectButton(SelectedButton);
    }

    public void UpdateAllButtonDisplays()
    {
        var layer = CurrentLayer;

        foreach (var src in AllSources)
        {
            var mapping = FindMapping(layer, src, inherit: true);
            if (mapping == null)
            {
                src.IsRemapped = false;
                src.TargetText = src.DisplayName;
                continue;
            }

            src.IsRemapped = !IsDefaultMapping(mapping, src);
            string extra = mapping.Activators.Count > 0 ? $" +{mapping.Activators.Count}" : "";
            src.TargetText = mapping.Target.DisplayText + extra;
        }
    }

    [RelayCommand]
    private void SaveCurrentMapping()
    {
        if (SelectedButton == null) return;
        var source = SelectedButton;

        MacroDefinition? macro = null;
        ScriptError = string.Empty;
        if (SelectedTargetType == TargetType.Macro && IsScriptMode)
        {
            if (!JintScriptRunner.TryValidate(ScriptCode, out var scriptProblem))
            {
                ScriptError = scriptProblem;
                StatusMessage = $"Script not saved: {scriptProblem}";
                return;
            }
            macro = MacroDefinition.FromScript(ScriptCode.Trim(), SelectedMacroRepeat);
        }
        else if (SelectedTargetType == TargetType.Macro)
        {
            if (!MacroDefinition.TryParseScript(MacroScript, SelectedMacroRepeat, out var parsed, out var error))
            {
                MacroError = error;
                StatusMessage = $"Macro not saved: {error}";
                return;
            }
            macro = parsed;
        }
        MacroError = string.Empty;

        var target = new RemapTarget
        {
            Type = SelectedTargetType,
            VirtualButton = SelectedTargetType == TargetType.VirtualControllerButton ? SelectedVirtualButton : ControllerButton.None,
            KeyboardKey = SelectedTargetType == TargetType.KeyboardKey ? SelectedKeyboardKey : string.Empty,
            MouseButton = SelectedTargetType == TargetType.MouseButton ? SelectedMouseButton : string.Empty,
            ScrollDelta = SelectedScrollDirection == ScrollDownLabel ? -1 : 1,
            Layer = SelectedTargetType == TargetType.JumpToLayer ? SelectedJumpLayer : 0,
            Macro = macro
        };

        var binding = new ActivatorBinding { Type = SelectedActivator, Target = target, Mode = SelectedMode, TurboRateMs = TurboRateMs };

        // The pipeline thread reads these mappings, so edit under the engine's lock
        lock (_mappingEngine.SyncRoot)
        {
            var layer = CurrentLayer;
            var mapping = Lookup(layer, source);
            if (mapping == null)
            {
                // First edit in this layer: start from what it inherits so other activators are not lost
                mapping = FindMapping(layer, source, inherit: true)?.Clone()
                          ?? new ButtonMapping { SourceButton = source.Button, Target = DefaultTarget(source) };
                Store(layer, source, mapping);
            }

            mapping.IsMuted = IsMuted;

            if (SelectedTargetType == TargetType.None && SelectedActivator != ActivatorType.Single)
                mapping.SetBinding(SelectedActivator, null); // an empty extra activator just means "no outcome"
            else
                mapping.SetBinding(SelectedActivator, binding);
        }

        UpdateAllButtonDisplays();
        StatusMessage = SelectedTargetType == TargetType.None && SelectedActivator != ActivatorType.Single
            ? $"Cleared {SelectedActivator} on {source.FriendlyName}"
            : $"Mapped {source.FriendlyName} ({SelectedActivator}) -> {target.DisplayText}";
    }

    [RelayCommand]
    private void ResetCurrentMapping()
    {
        if (SelectedButton == null) return;
        var source = SelectedButton;

        lock (_mappingEngine.SyncRoot)
        {
            var layer = CurrentLayer;
            if (layer.LayerIndex == 0 && source.Kind == SourceKind.Pad)
            {
                // The Main layer always carries an explicit pass-through for pad buttons
                layer.Mappings[source.Button] = new ButtonMapping
                {
                    SourceButton = source.Button,
                    Target = RemapTarget.Default(source.Button),
                    Mode = MappingMode.Normal
                };
            }
            else
            {
                Remove(layer, source); // keys and mouse buttons fall back to the real input; shift layers fall back to Main
            }
        }

        SelectButton(source);
        UpdateAllButtonDisplays();
        StatusMessage = $"Reset {source.FriendlyName} to default";
    }

    // ---- Layers --------------------------------------------------------------------------------------------------

    partial void OnActiveShiftLayerChanged(int value)
    {
        // The layer dropdown binds here; ignore indexes the profile doesn't have (e.g. -1 while the list refreshes)
        if (!SelectedProfile.ShiftLayers.TryGetValue(value, out var layer)) return;

        _mappingEngine.SetShiftLayer(value);
        RefreshLayerOptions();
        UpdateAllButtonDisplays();
        if (SelectedButton != null) SelectButton(SelectedButton);
        StatusMessage = $"Switched to {layer.Name}";
    }

    private void BuildLayerModifierOptions()
    {
        LayerModifierOptions.Clear();
        foreach (var b in ControllerButtons)
        {
            var option = new ModifierOption(b.Button, b.Button.ToString()) { Changed = OnModifierOptionChanged };
            LayerModifierOptions.Add(option);
        }
    }

    private void RefreshLayerOptions()
    {
        _loadingLayerOptions = true;
        try
        {
            var layer = CurrentLayer;
            foreach (var option in LayerModifierOptions) option.IsChecked = layer.ModifierButtons.Contains(option.Button);
            SelectedLayerType = layer.Type;
            LayerLeftStickIndex = layer.LeftStickMode is { } l ? (int)l + 1 : 0;
            LayerRightStickIndex = layer.RightStickMode is { } r ? (int)r + 1 : 0;
        }
        finally
        {
            _loadingLayerOptions = false;
        }
    }

    private void OnModifierOptionChanged(ModifierOption changed)
    {
        if (_loadingLayerOptions || ActiveShiftLayer == 0) return;

        var chosen = LayerModifierOptions.Where(o => o.IsChecked).Select(o => o.Button).ToList();
        if (chosen.Count > ShiftLayer.MaxModifierButtons)
        {
            _loadingLayerOptions = true;
            changed.IsChecked = false;
            _loadingLayerOptions = false;
            StatusMessage = $"A layer can use at most {ShiftLayer.MaxModifierButtons} buttons";
            return;
        }

        lock (_mappingEngine.SyncRoot)
        {
            CurrentLayer.ModifierButtons = chosen;
        }
        StatusMessage = chosen.Count == 0
            ? $"{CurrentLayer.Name} has no activation buttons, so it can only be reached by a jump or the dropdown"
            : $"{CurrentLayer.Name} activates with {string.Join(" + ", chosen)}";
    }

    partial void OnLayerLeftStickIndexChanged(int value)
    {
        if (_loadingLayerOptions) return;
        lock (_mappingEngine.SyncRoot) CurrentLayer.LeftStickMode = value <= 0 ? null : (StickMode)(value - 1);
    }

    partial void OnLayerRightStickIndexChanged(int value)
    {
        if (_loadingLayerOptions) return;
        lock (_mappingEngine.SyncRoot) CurrentLayer.RightStickMode = value <= 0 ? null : (StickMode)(value - 1);
    }

    partial void OnSelectedLayerTypeChanged(ShiftType value)
    {
        if (_loadingLayerOptions || ActiveShiftLayer == 0) return;
        lock (_mappingEngine.SyncRoot)
        {
            CurrentLayer.Type = value;
        }
    }

    // ---- Apply / stop --------------------------------------------------------------------------------------------

    private bool AnyMappingMuted() =>
        SelectedProfile.ShiftLayers.Values.Any(l =>
            l.Mappings.Values.Any(m => m.IsMuted)
            || l.KeyboardMappings.Values.Any(m => m.IsMuted)
            || l.MouseMappings.Values.Any(m => m.IsMuted));

    [RelayCommand]
    private async Task ToggleApply()
    {
        IsRemappingActive = !IsRemappingActive;
        _mappingEngine.IsActive = IsRemappingActive;

        if (IsRemappingActive)
        {
            // macOS only lets an app watch or inject input once the person has allowed it; ask now, not on the first key press
            if (Platform.NeedsInputPermissions && _platform.MissingPermissions() != null) _platform.RequestPermissions();
            RefreshPermissions();

            // Snapshot XInput slots first: the virtual pad is the slot that appears after connecting
            var before = _reader.ConnectedSlots();

            // Find the physical controller's devices before the virtual pad exists, so the virtual pad is never hidden
            var currentDevices = _hidHide.IsAvailable ? _hidHide.GetPhysicalControllerDevices() : Array.Empty<string>();
            if (currentDevices.Count > 0) _physicalDevices = currentDevices;

            bool ok = _virtualService.Connect(SelectedVirtualController);

            // Only an Xbox 360 pad shows up as an XInput slot that has to be ignored; a DualShock 4 does not
            _reader.ExcludedUserIndex = ok && SelectedVirtualController == ControllerType.Xbox360 ? FindNewSlot(before) : -1;

            bool wantMute = HideInputDevice || AnyMappingMuted();
            HideResult? hide = null;
            if (wantMute)
            {
                hide = !SupportsDeviceHiding ? HideResult.Fail($"Hiding a controller from other apps is not possible on {Platform.Name}.")
                    : _hidHide.Hide(_physicalDevices);
                HideStatusText = hide.Message;
            }
            else if (_hidHide.IsHiding)
            {
                _hidHide.Restore();
                HideStatusText = "Physical controller is not hidden";
            }

            _pipeline.Start();
            PopulateConnectedDevices();
            StatusMessage = ok
                ? $"🟢 REMAPPING ACTIVE — Virtual {VirtualControllerOptions.First(o => o.Type == SelectedVirtualController).Name} controller connected"
                : !SupportsVirtualController
                    ? $"🟢 REMAPPING ACTIVE — controller to keyboard, mouse and macros ({Platform.Name} has no virtual controller)"
                    : "🟡 REMAPPING ACTIVE — Virtual Output (ViGEm Driver not detected, running in simulation mode)";
            if (!_pipeline.KeyboardMouseHooked)
            {
                StatusMessage += " — keyboard/mouse hook unavailable";
                if (HasPermissionIssue) StatusMessage += $". {MissingPermissions}";
            }
            else if (HasPermissionIssue)
            {
                StatusMessage += $" — {MissingPermissions}";
            }
            if (hide != null)
            {
                StatusMessage += hide.Success ? " — physical controller hidden" : $" — NOT hidden: {hide.Message}";
            }
        }
        else
        {
            _pipeline.Stop();
            _virtualService.DisconnectVirtual();
            _reader.ExcludedUserIndex = -1;

            if (!HideInputDevice)
            {
                _hidHide.Restore();
                HideStatusText = "Physical controller is not hidden";
            }
            StatusMessage = "⚪ REMAPPING PAUSED — Emulation Disconnected";

            // The driver removes the virtual pad asynchronously; refreshing immediately would still list it
            await Task.Delay(500);
            PopulateConnectedDevices();
        }
    }

    // Switch hiding on or off straight away when the toggle changes
    private void ApplyHidingNow()
    {
        if (!SupportsDeviceHiding)
        {
            HideStatusText = $"Hiding a controller from other apps is not possible on {Platform.Name}.";
            return;
        }

        if (!_hidHide.IsAvailable)
        {
            HideStatusText = "HidHide is not installed.";
            return;
        }

        bool want = HideInputDevice || (IsRemappingActive && AnyMappingMuted());
        if (want)
        {
            if (_physicalDevices.Count == 0 || !IsRemappingActive)
            {
                var detected = _hidHide.GetPhysicalControllerDevices();
                if (detected.Count > 0) _physicalDevices = detected;
            }

            var result = _hidHide.Hide(_physicalDevices);
            HideStatusText = result.Message;
            StatusMessage = result.Success ? "🟢 Physical controller hidden from other apps" : $"⚠️ NOT hidden: {result.Message}";
        }
        else
        {
            if (_hidHide.IsHiding)
            {
                _hidHide.Restore();
                HideStatusText = "Physical controller is not hidden";
                StatusMessage = "Physical controller visible to all apps";
            }
            else
            {
                HideStatusText = "Physical controller is not hidden";
            }
        }
    }

    [RelayCommand]
    private void ShowHidHideDiagnostics()
    {
        StatusMessage = "HidHide: " + _hidHide.Diagnostics().Replace(Environment.NewLine, " | ");
    }

    // ---- Live input display and macro recording -------------------------------------------------------------------

    /// <summary>Runs one pass of the live display, as the timer would.</summary>
    internal void LiveTickForTests() => OnLiveTick();

    private void OnLiveTick()
    {
        // While remapping runs the pipeline owns the pad reader; otherwise read it here so the display still works.
        // In the tester a faster thread does the reading and this just shows its newest result.
        PhysicalState? phys = IsTesterMode && _sampler is { IsRunning: true } ? _sampler.Latest : ReadPhysical();

        // Plugging a controller in or out shows in the device bar straight away
        if ((phys != null) != _hadController)
        {
            _hadController = phys != null;
            PopulateConnectedDevices();
            if (HideInputDevice && _hadController)
            {
                ApplyHidingNow();
            }
        }

        if (IsRecording && phys != null) _recorder.OnPad(phys.Buttons);
        FeedComboCapture(phys);
        CountUsage(phys);
        UpdatePressedText(phys);
        FeedStickTuning(phys);
        if (IsTesterMode && ++_testerTicks % 2 == 0) RefreshTester();

        foreach (var vm in ControllerButtons)
        {
            vm.IsPressed = phys != null && phys.Buttons.Contains(vm.Button);
            vm.AnalogValue = phys == null ? 0
                : vm.Button == ControllerButton.LeftTrigger ? phys.LeftTrigger
                : vm.Button == ControllerButton.RightTrigger ? phys.RightTrigger
                : 0;

            if (vm.HasStick)
            {
                double x = 0, y = 0;
                if (phys != null)
                {
                    (x, y) = vm.Button == ControllerButton.LeftStickClick ? (phys.LeftStickX, phys.LeftStickY) : (phys.RightStickX, phys.RightStickY);
                }
                vm.StickOffsetX = x * 12;
                vm.StickOffsetY = -y * 12; // stick up is positive, screen y grows downward
                vm.StickActive = Math.Abs(x) > 0.08 || Math.Abs(y) > 0.08;
            }
        }

        // Keys and mouse buttons are only watched while the hook runs (remapping or recording)
        IReadOnlyCollection<string> keys = Array.Empty<string>(), mouse = Array.Empty<string>();
        if (_hook.IsInstalled) (keys, mouse) = _hook.Snapshot();

        foreach (var vm in KeyboardSources) vm.IsPressed = keys.Contains(vm.SourceName, StringComparer.OrdinalIgnoreCase);
        foreach (var vm in MouseSources) vm.IsPressed = mouse.Contains(vm.SourceName, StringComparer.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void StartMacroRecording()
    {
        if (IsRecording) return;
        if (IsRemappingActive)
        {
            StatusMessage = "Stop remapping before recording, so the keys you press aren't remapped or swallowed";
            return;
        }

        SelectedTargetType = TargetType.Macro;
        _recorder.Start();

        // The hook is what sees real keys and mouse buttons; the pad is read by the live timer
        bool hooked = _hook.IsInstalled || _hook.Start();
        _hookStartedForRecording = hooked && !_pipeline.IsRunning;
        if (hooked)
        {
            _hook.KeyEvent += _recordKey;
            _hook.MouseEvent += _recordMouse;
        }

        IsRecording = true;
        RecordingHint = hooked
            ? "Recording keys, mouse and controller. Do your sequence, then click Stop."
            : "Recording the controller only (the keyboard/mouse hook could not start). Click Stop when done.";
        StatusMessage = "● Recording macro…";
    }

    [RelayCommand]
    private void StopMacroRecording()
    {
        if (!IsRecording) return;

        _hook.KeyEvent -= _recordKey;
        _hook.MouseEvent -= _recordMouse;
        if (_hookStartedForRecording) _hook.Stop();
        _hookStartedForRecording = false;

        var macro = _recorder.Stop(SelectedMacroRepeat);
        IsRecording = false;
        RecordingHint = string.Empty;

        int steps = macro.Steps.Count(s => s.Action != MacroActionType.Delay);
        if (steps == 0)
        {
            StatusMessage = "Nothing was recorded";
            return;
        }

        MacroScript = macro.ToScript();
        MacroError = string.Empty;
        StatusMessage = SelectedMacroRepeat == MacroRepeatMode.HoldUntilRelease
            ? $"Recorded {steps} steps; they stay held while the button is held. Press SAVE MAPPING to keep it."
            : $"Recorded {steps} steps. Press SAVE MAPPING to keep it.";
    }

    /// <summary>Stops the live display timer. Call when the window closes.</summary>
    public void StopLiveUpdates()
    {
        _liveTimer.Dispose();
        if (IsRecording) StopMacroRecording();
    }

    private int FindNewSlot(IReadOnlyList<int> before)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            foreach (var slot in _reader.ConnectedSlots())
            {
                if (!before.Contains(slot)) return slot;
            }
            Thread.Sleep(25);
        }
        return -1;
    }

    // Ctrl+Alt+Shift+F12: always works, even if a mapping has swallowed keys you needed
    private void OnPanicRequested()
    {
        _ui.Post(() =>
        {
            if (!IsRemappingActive) return;
            ToggleApplyCommand.Execute(null);
            StatusMessage = "Panic key pressed — remapping stopped";
        });
    }

    /// <summary>Stops polling and releases every held key and the virtual pad. Call when the window closes.</summary>
    public void Shutdown()
    {
        _sampler?.Stop();
        StopVibration();
        _stats.Save(StatsPath);
        _foregroundTimer?.Dispose();
        _foregroundTimer = null;
        _pipeline.Stop();
        _hidHide.Restore();
        _virtualService.DisconnectVirtual();
        _reader.ExcludedUserIndex = -1;
    }

    [RelayCommand]
    private void NewProfile()
    {
        var newP = Profile.CreateDefault($"New Profile {Profiles.Count + 1}");
        Profiles.Add(newP);
        SelectedProfile = newP;
        UpdateAllButtonDisplays();
        StatusMessage = $"Created profile: {newP.Name}";
    }
}
