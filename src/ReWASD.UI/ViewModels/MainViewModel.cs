using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine;
using ReWASD.Engine.HidHide;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.UI.ViewModels;

public enum AppPage
{
    Remap,
    Preferences
}

/// <summary>Which kind of input is being mapped: the controller, the keyboard or the mouse.</summary>
public enum InputTab
{
    Gamepad,
    Keyboard,
    Mouse
}

/// <summary>Tabs of the binding frame. Only tabs whose feature exists are enabled; the rest are placeholders.</summary>
public enum BindingTab
{
    Main,
    Shift,
    Advanced,
    Keyboard,
    Rumble,
    Mask
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
    [NotifyPropertyChangedFor(nameof(IsSourcePickerTab))]
    private InputTab _selectedInputTab = InputTab.Gamepad;

    [ObservableProperty]
    private ObservableCollection<ButtonViewModel> _currentSources = new();

    [ObservableProperty]
    private BindingTab _selectedBindingTab = BindingTab.Main;

    // Target process for the selected profile (empty = applies to everything)
    [ObservableProperty]
    private string _selectedProcessExecutable = string.Empty;

    [ObservableProperty]
    private bool _isInspectorOpen = true;

    [ObservableProperty]
    private bool _isOutputMenuOpen;

    [ObservableProperty]
    private bool _isConfigMenuOpen;

    [ObservableProperty]
    private bool _isLayerMenuOpen;

    // Both options hide the physical pad through HidHide; hiding is per device, not per button
    [ObservableProperty]
    private bool _muteAll;

    [ObservableProperty]
    private bool _hideInputDevice;

    public IReadOnlyList<int> SlotNumbers { get; } = new[] { 1, 2, 3, 4 };

    partial void OnActiveSlotChanged(int value)
    {
        SelectedProfile.ActiveSlot = value;
    }

    public IReadOnlyList<string> LayerNames { get; } = new[] { "Main Layer", "Shift 1", "Shift 2", "Shift 3", "Shift 4" };

    public bool IsPreferencesPage => CurrentPage == AppPage.Preferences;
    public bool IsGamepadTab => SelectedInputTab == InputTab.Gamepad;
    public bool IsSourcePickerTab => SelectedInputTab != InputTab.Gamepad;

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
    private MappingMode _selectedMode = MappingMode.Normal;

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
    public ObservableCollection<ControllerButton> AvailableVirtualButtons { get; } = new();
    public ObservableCollection<string> AvailableKeyboardKeys { get; } = new();
    public ObservableCollection<string> AvailableMouseButtons { get; } = new();
    public IReadOnlyList<string> AvailableScrollDirections { get; } = new[] { ScrollUpLabel, ScrollDownLabel };
    public IReadOnlyList<MappingMode> AvailableModes { get; } = Enum.GetValues<MappingMode>();
    public IReadOnlyList<MacroRepeatMode> AvailableMacroRepeatModes { get; } = Enum.GetValues<MacroRepeatMode>();
    public ObservableCollection<ButtonViewModel> ControllerButtons { get; } = new();
    public ObservableCollection<ButtonViewModel> KeyboardSources { get; } = new();
    public ObservableCollection<ButtonViewModel> MouseSources { get; } = new();
    public ObservableCollection<string> ConnectedDevices { get; } = new();

    public MainViewModel()
    {
        _selectedProfile = Profile.CreateDefault("Default Controller Profile");
        _profiles.Add(_selectedProfile);
        _profiles.Add(Profile.CreateDefault("Apex Legends (High Sensitivity)"));
        _profiles.Add(Profile.CreateDefault("Call of Duty (Tactical Flipped)"));

        _mappingEngine = new MappingEngine(_selectedProfile);
        _virtualService = new VirtualGamepadService();
        _virtualService.Initialize();
        _injector = new KeyboardMouseInjector(_inputBackend);
        _macroPad = new MacroPadState();
        _macros = new MacroRunner(_inputBackend, _macroPad, new XInputRumbleOutput(_reader));
        _hook = new LowLevelInputHook(_mappingEngine.IsKeySuppressed, _mappingEngine.IsMouseSuppressed);
        _hook.PanicRequested += OnPanicRequested;
        _pipeline = new InputPipeline(_mappingEngine, _reader, _virtualService, _injector, _macros, _macroPad, _hook);

        PopulateInspectorOptions();
        PopulateConnectedDevices();
        InitializeControllerButtons();
        InitializeSourcePickers();
        BuildLayerModifierOptions();
        LoadProfileSettings(_selectedProfile);

        // Select first button by default
        if (ControllerButtons.Count > 0)
        {
            SelectButton(ControllerButtons[0]);
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
    private void ShowPage(string page)
    {
        if (Enum.TryParse<AppPage>(page, out var parsed)) CurrentPage = parsed;
    }

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
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReWASD-OpenSource", "profiles");
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var fileName = string.Concat(SelectedProfile.Name.Select(c => invalid.Contains(c) ? '_' : c)) + ".json";
            var path = System.IO.Path.Combine(dir, fileName);

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

    private void PopulateInspectorOptions()
    {
        foreach (ControllerButton btn in Enum.GetValues<ControllerButton>())
        {
            if (btn != ControllerButton.None)
                AvailableVirtualButtons.Add(btn);
        }

        foreach (var k in KeyOptions()) AvailableKeyboardKeys.Add(k);
        foreach (var m in LowLevelInputHook.MouseNames) AvailableMouseButtons.Add(m);
    }

    private static IEnumerable<string> KeyOptions()
    {
        for (char c = 'A'; c <= 'Z'; c++) yield return c.ToString();
        for (char c = '0'; c <= '9'; c++) yield return c.ToString();
        for (int f = 1; f <= 12; f++) yield return "F" + f;
        foreach (var n in KeyMap.NamedKeys) yield return n;
    }

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

    private void InitializeControllerButtons()
    {
        ControllerButtons.Clear();

        // Coordinates based on center canvas layout (650x420)
        AddButton(ControllerButton.LeftStickClick, "LS", 185, 140, 48, 48);
        AddButton(ControllerButton.DpadUp, "▲", 245, 205, 34, 34);
        AddButton(ControllerButton.DpadDown, "▼", 245, 275, 34, 34);
        AddButton(ControllerButton.DpadLeft, "◀", 210, 240, 34, 34);
        AddButton(ControllerButton.DpadRight, "▶", 280, 240, 34, 34);

        AddButton(ControllerButton.View, "⧉", 275, 145, 34, 28);
        AddButton(ControllerButton.Guide, "🗲", 325, 125, 42, 42);
        AddButton(ControllerButton.Menu, "☰", 385, 145, 34, 28);

        AddButton(ControllerButton.RightStickClick, "RS", 370, 225, 48, 48);
        AddButton(ControllerButton.Y, "Y", 445, 125, 36, 36);
        AddButton(ControllerButton.X, "X", 410, 155, 36, 36);
        AddButton(ControllerButton.B, "B", 480, 155, 36, 36);
        AddButton(ControllerButton.A, "A", 445, 185, 36, 36);

        AddButton(ControllerButton.LeftBumper, "LB", 180, 75, 65, 32);
        AddButton(ControllerButton.RightBumper, "RB", 445, 75, 65, 32);
        AddButton(ControllerButton.LeftTrigger, "LT", 175, 25, 75, 36);
        AddButton(ControllerButton.RightTrigger, "RT", 440, 25, 75, 36);

        UpdateAllButtonDisplays();
    }

    private void AddButton(ControllerButton btn, string label, double x, double y, double w, double h)
    {
        ControllerButtons.Add(new ButtonViewModel
        {
            Kind = SourceKind.Pad,
            Button = btn,
            SourceName = btn.ToString(),
            DisplayName = label,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            OnSelected = SelectButton
        });
    }

    private void InitializeSourcePickers()
    {
        foreach (var key in KeyOptions())
        {
            KeyboardSources.Add(new ButtonViewModel
            {
                Kind = SourceKind.Key,
                SourceName = key,
                DisplayName = key,
                Width = key.Length > 2 ? 88 : 44,
                Height = 38,
                OnSelected = SelectButton
            });
        }

        foreach (var name in LowLevelInputHook.MouseNames)
        {
            MouseSources.Add(new ButtonViewModel
            {
                Kind = SourceKind.Mouse,
                SourceName = name,
                DisplayName = name,
                Width = 130,
                Height = 38,
                OnSelected = SelectButton
            });
        }

        UpdateAllButtonDisplays();
    }

    partial void OnSelectedInputTabChanged(InputTab value)
    {
        CurrentSources = value switch
        {
            InputTab.Keyboard => KeyboardSources,
            InputTab.Mouse => MouseSources,
            _ => new ObservableCollection<ButtonViewModel>()
        };

        var first = value switch
        {
            InputTab.Keyboard => KeyboardSources.FirstOrDefault(),
            InputTab.Mouse => MouseSources.FirstOrDefault(),
            _ => ControllerButtons.FirstOrDefault()
        };
        if (first != null) SelectButton(first);
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
            ? $"Cleared {SelectedActivator} on {source.DisplayName}"
            : $"Mapped {source.DisplayName} ({SelectedActivator}) -> {target.DisplayText}";
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
        StatusMessage = $"Reset {source.DisplayName} to default";
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

    [RelayCommand]
    private void SelectSlot(string slotStr)
    {
        if (int.TryParse(slotStr, out int slot))
        {
            ActiveSlot = slot;
            SelectedProfile.ActiveSlot = slot;
            StatusMessage = $"Active Slot: {slot}";
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

            // "Mute" hides the physical pad from games. Capture it before the virtual pad exists so only real devices are hidden.
            bool wantMute = MuteAll || HideInputDevice || AnyMappingMuted();
            var devicesToHide = wantMute && _hidHide.IsAvailable ? _hidHide.GetPresentGamingDevices() : Array.Empty<string>();

            bool ok = _virtualService.ConnectVirtualXbox();
            _reader.ExcludedUserIndex = ok ? FindNewSlot(before) : -1;

            bool muted = ok && _hidHide.Hide(devicesToHide);

            _pipeline.Start();
            PopulateConnectedDevices();
            StatusMessage = ok
                ? "🟢 REMAPPING ACTIVE — Virtual Xbox 360 Controller Connected"
                : "🟡 REMAPPING ACTIVE — Virtual Output (ViGEm Driver not detected, running in simulation mode)";
            if (!_pipeline.KeyboardMouseHooked)
            {
                StatusMessage += " — keyboard/mouse hook unavailable";
            }
            if (wantMute)
            {
                StatusMessage += muted ? " — physical controller hidden"
                    : _hidHide.IsAvailable ? " — could not hide physical controller"
                    : " — install HidHide to mute the physical controller";
            }
        }
        else
        {
            Shutdown();
            StatusMessage = "⚪ REMAPPING PAUSED — Emulation Disconnected";

            // The driver removes the virtual pad asynchronously; refreshing immediately would still list it
            await Task.Delay(500);
            PopulateConnectedDevices();
        }
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
