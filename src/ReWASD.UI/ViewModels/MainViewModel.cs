using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine;
using ReWASD.Engine.HidHide;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;
using ReWASD.UI.Skins;

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
    Macro
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

    private readonly MappingEngine _mappingEngine;
    private readonly VirtualGamepadService _virtualService;
    private readonly XInputReader _reader = new();
    private readonly Win32InputBackend _inputBackend = new();
    private readonly KeyboardMouseInjector _injector;
    private readonly HidHideService _hidHide = new();
    private readonly MacroRunner _macros;
    private readonly MacroPadState _macroPad;
    private readonly LowLevelInputHook _hook;
    private readonly InputPipeline _pipeline;
    private readonly MacroRecorder _recorder = new();
    private readonly DispatcherTimer _liveTimer;
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

    public IReadOnlyList<VirtualControllerOption> VirtualControllerOptions { get; } = new[]
    {
        new VirtualControllerOption(ControllerType.Xbox360, "Xbox 360", true, "Works with every game that supports an Xbox controller"),
        new VirtualControllerOption(ControllerType.DualShock4, "DualShock 4", true, "For games and emulators that expect a PlayStation 4 controller"),
        new VirtualControllerOption(ControllerType.XboxOne, "Xbox One", false, "Not available: the ViGEm driver cannot emulate an Xbox One controller"),
        new VirtualControllerOption(ControllerType.DualSense, "DualSense (PS5)", false, "Not available: the ViGEm driver cannot emulate a DualSense controller"),
        new VirtualControllerOption(ControllerType.SwitchPro, "Switch Pro", false, "Not available: the ViGEm driver cannot emulate a Switch Pro controller")
    };

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

        var before = XInputReader.GetConnectedSlots();
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
    public string DriverStatusText => IsDriverAvailable ? "ViGEmBus: Ready" : "ViGEmBus: Not installed";
    public string HidHideStatusText => IsHidHideAvailable ? "HidHide: Ready" : "HidHide: Not installed";

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

    public MainViewModel()
    {
        _selectedProfile = Profile.CreateDefault("Default Controller Profile");
        Profiles.Add(_selectedProfile);
        LoadSavedProfiles();

        _mappingEngine = new MappingEngine(_selectedProfile);
        _virtualService = new VirtualGamepadService();
        _virtualService.Initialize();
        _injector = new KeyboardMouseInjector(_inputBackend);
        _macroPad = new MacroPadState();
        _macros = new MacroRunner(_inputBackend, _macroPad, new XInputRumbleOutput(_reader));
        _hook = new LowLevelInputHook(_mappingEngine.IsKeySuppressed, _mappingEngine.IsMouseSuppressed);
        _hook.PanicRequested += OnPanicRequested;
        _pipeline = new InputPipeline(_mappingEngine, _reader, _virtualService, _injector, _macros, _macroPad, _hook);
        _recordKey = _recorder.OnKey;
        _recordMouse = _recorder.OnMouse;

        // Drives the live button, trigger and stick display (and pad recording) from the UI thread
        _liveTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _liveTimer.Tick += OnLiveTick;
        _liveTimer.Start();

        PopulateInspectorOptions();
        PopulateConnectedDevices();
        InitializeControllerButtons();
        InitializeSourcePickers();
        BuildLayerModifierOptions();
        LoadProfileSettings(_selectedProfile);

        // Start with the A button selected
        var start = ControllerButtons.FirstOrDefault(b => b.Button == ControllerButton.A) ?? ControllerButtons.FirstOrDefault();
        if (start != null) SelectButton(start);
    }

    private static string ProfilesDirectory =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReWASD-OpenSource", "profiles");

    /// <summary>Brings back configs saved with the save button. A saved config replaces the starter one of the same name.</summary>
    private void LoadSavedProfiles()
    {
        try
        {
            if (!System.IO.Directory.Exists(ProfilesDirectory)) return;

            foreach (var file in System.IO.Directory.EnumerateFiles(ProfilesDirectory, "*.json").OrderBy(f => f))
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
        // Keep the engine on the profile being edited
        _mappingEngine.CurrentProfile = value;
        if (!value.ShiftLayers.ContainsKey(ActiveShiftLayer)) ActiveShiftLayer = 0;
        _mappingEngine.SetShiftLayer(ActiveShiftLayer);
        LoadProfileSettings(value);
        RefreshLayerOptions();
        UpdateAllButtonDisplays();
        if (SelectedButton != null) SelectButton(SelectedButton);
    }

    private void LoadProfileSettings(Profile profile)
    {
        SelectedProcessExecutable = profile.ProcessExecutable ?? string.Empty;

        // A config saved with a type this build cannot emulate falls back to Xbox 360
        SelectedVirtualController = VirtualGamepadService.IsSupported(profile.TargetControllerType) ? profile.TargetControllerType : ControllerType.Xbox360;
        profile.TargetControllerType = SelectedVirtualController; // write the fallback back so it is saved correctly
        profile.Timings ??= new ActivatorTimings();
        LongPressMs = profile.Timings.LongPressMs;
        MultiPressWindowMs = profile.Timings.MultiPressWindowMs;
        PulseMs = profile.Timings.PulseMs;
    }

    partial void OnSelectedProcessExecutableChanged(string value)
    {
        SelectedProfile.ProcessExecutable = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
        StatusMessage = "ReWASD Open Source v0.1.0-alpha — clean-room gamepad remapper";
    }

    [RelayCommand]
    private void SaveProfile()
    {
        try
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var fileName = string.Concat(SelectedProfile.Name.Select(c => invalid.Contains(c) ? '_' : c)) + ".json";
            var path = System.IO.Path.Combine(ProfilesDirectory, fileName);

            lock (_mappingEngine.SyncRoot)
            {
                ProfileSerializer.SaveToFile(SelectedProfile, path);
            }
            StatusMessage = $"Saved config to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save config: {ex.Message}";
        }
    }

    private void PopulateInspectorOptions() => BuildRemapOptions();

    private void PopulateConnectedDevices()
    {
        ConnectedDevices.Clear();
        var slots = XInputReader.GetConnectedSlots();
        foreach (var slot in slots)
        {
            string tag = slot == _reader.ExcludedUserIndex ? " (virtual output)" : "";
            ConnectedDevices.Add($"🎮 XInput Controller {slot + 1}{tag}");
        }
        if (slots.Count == 0) ConnectedDevices.Add("No XInput controller detected");
    }

    // ---- Surface: controller picture, keyboard and mouse ---------------------------------------------------------

    [ObservableProperty]
    private SkinKind _selectedSkin = SkinKind.Xbox;

    [ObservableProperty]
    private ObservableCollection<DecorShape> _surfaceDecor = new();

    public IReadOnlyList<SkinOption> SkinOptions => ControllerSkins.Options;

    partial void OnSelectedSkinChanged(SkinKind value) => ApplySkin(value);

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

    private ButtonViewModel MakeSource(SourceKind kind, SourcePlacement s) => new()
    {
        Kind = kind,
        SourceName = s.Name,
        FriendlyName = s.Name,
        DisplayName = s.Legend,
        X = s.Place.Cx - s.Place.W / 2,
        Y = s.Place.Cy - s.Place.H / 2,
        Width = s.Place.W,
        Height = s.Place.H,
        CornerRadius = s.Radius,
        LegendFontSize = s.FontSize,
        OnSelected = SelectButton
    };

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

        var first = value == InputTab.Gamepad
            ? CurrentSources.FirstOrDefault(b => b.Button == ControllerButton.A)
            : CurrentSources.FirstOrDefault();
        if (first != null) SelectButton(first);
    }

    // ---- The "Remap to" dropdown ---------------------------------------------------------------------------------

    private bool _syncingOption;

    public ObservableCollection<RemapOption> RemapOptions { get; } = new();
    public System.ComponentModel.ICollectionView RemapOptionsView { get; private set; } = null!;

    [ObservableProperty]
    private RemapOption? _selectedRemapOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMacroMode))]
    [NotifyPropertyChangedFor(nameof(IsRemapMode))]
    private TargetMode _targetMode = TargetMode.Remap;

    public bool IsMacroMode => TargetMode == TargetMode.Macro;
    public bool IsRemapMode => TargetMode == TargetMode.Remap;

    private void BuildRemapOptions()
    {
        const string Glyph = "";

        RemapOptions.Add(new RemapOption(RemapKind.Nothing, "Nothing", "Nothing (disable this input)") { Legend = "", Shape = IconShape.Glyph });

        foreach (var b in ControllerSkins.Get(SkinKind.Xbox).Layout.Keys)
        {
            RemapOptions.Add(new RemapOption(RemapKind.Pad, "Controller", FriendlyPadName(b)) { Pad = b });
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

        var view = new System.Windows.Data.ListCollectionView(RemapOptions);
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(RemapOption.Group)));
        RemapOptionsView = view;
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
        if (_syncingOption || value == null || IsMacroMode) return;
        ApplyOption(value);
    }

    partial void OnTargetModeChanged(TargetMode value)
    {
        if (_syncingOption) return;

        if (value == TargetMode.Macro) SelectedTargetType = TargetType.Macro;
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
        MacroScript = target.Macro?.ToScript() ?? string.Empty;
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
            TargetMode = target.Type == TargetType.Macro ? TargetMode.Macro : TargetMode.Remap;
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
        if (SelectedTargetType == TargetType.Macro)
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
            // Snapshot XInput slots first: the virtual pad is the slot that appears after connecting
            var before = XInputReader.GetConnectedSlots();

            // Find the physical controller's devices before the virtual pad exists, so the virtual pad is never hidden
            _physicalDevices = _hidHide.IsAvailable ? _hidHide.GetPhysicalControllerDevices() : Array.Empty<string>();

            bool ok = _virtualService.Connect(SelectedVirtualController);

            // Only an Xbox 360 pad shows up as an XInput slot that has to be ignored; a DualShock 4 does not
            _reader.ExcludedUserIndex = ok && SelectedVirtualController == ControllerType.Xbox360 ? FindNewSlot(before) : -1;

            bool wantMute = HideInputDevice || AnyMappingMuted();
            HideResult? hide = wantMute ? (ok ? _hidHide.Hide(_physicalDevices) : HideResult.Fail("Hiding needs the virtual controller, which is not connected.")) : null;
            HideStatusText = hide?.Message ?? "Physical controller is not hidden";

            _pipeline.Start();
            PopulateConnectedDevices();
            StatusMessage = ok
                ? $"🟢 REMAPPING ACTIVE — Virtual {VirtualControllerOptions.First(o => o.Type == SelectedVirtualController).Name} controller connected"
                : "🟡 REMAPPING ACTIVE — Virtual Output (ViGEm Driver not detected, running in simulation mode)";
            if (!_pipeline.KeyboardMouseHooked)
            {
                StatusMessage += " — keyboard/mouse hook unavailable";
            }
            if (hide != null)
            {
                StatusMessage += hide.Success ? " — physical controller hidden" : $" — NOT hidden: {hide.Message}";
            }
        }
        else
        {
            Shutdown();
            HideStatusText = "Physical controller is not hidden";
            StatusMessage = "⚪ REMAPPING PAUSED — Emulation Disconnected";

            // The driver removes the virtual pad asynchronously; refreshing immediately would still list it
            await Task.Delay(500);
            PopulateConnectedDevices();
        }
    }

    // Switch hiding on or off straight away when a checkbox changes while remapping is running
    private void ApplyHidingNow()
    {
        if (!IsRemappingActive) return;

        bool want = HideInputDevice || AnyMappingMuted();
        if (want && !_hidHide.IsHiding)
        {
            var result = _hidHide.Hide(_physicalDevices);
            HideStatusText = result.Message;
            StatusMessage = result.Success ? "Physical controller hidden" : $"NOT hidden: {result.Message}";
        }
        else if (!want && _hidHide.IsHiding)
        {
            _hidHide.Restore();
            HideStatusText = "Physical controller is not hidden";
            StatusMessage = "Physical controller visible again";
        }
    }

    [RelayCommand]
    private void ShowHidHideDiagnostics()
    {
        StatusMessage = "HidHide: " + _hidHide.Diagnostics().Replace(Environment.NewLine, " | ");
    }

    // ---- Live input display and macro recording -------------------------------------------------------------------

    private void OnLiveTick(object? sender, EventArgs e)
    {
        // While remapping runs the pipeline owns the pad reader; otherwise read it here so the display still works
        PhysicalState? phys = _pipeline.IsRunning ? _pipeline.LatestPhysical : _reader.Read();

        if (IsRecording && phys != null) _recorder.OnPad(phys.Buttons);

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
        _liveTimer.Stop();
        if (IsRecording) StopMacroRecording();
    }

    private static int FindNewSlot(IReadOnlyList<int> before)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            foreach (var slot in XInputReader.GetConnectedSlots())
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
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!IsRemappingActive) return;
            ToggleApplyCommand.Execute(null);
            StatusMessage = "Panic key pressed — remapping stopped";
        });
    }

    /// <summary>Stops polling and releases every held key and the virtual pad. Call when the window closes.</summary>
    public void Shutdown()
    {
        _pipeline.Stop();
        _hidHide.Restore();
        _virtualService.DisconnectVirtualXbox();
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
