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
    private readonly InputPipeline _pipeline;

    [ObservableProperty]
    private ObservableCollection<Profile> _profiles = new();

    [ObservableProperty]
    private Profile _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreferencesPage))]
    private AppPage _currentPage = AppPage.Remap;

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

    public bool IsDriverAvailable => _virtualService.IsDriverAvailable;
    public bool IsHidHideAvailable => _hidHide.IsAvailable;
    public string DriverStatusText => IsDriverAvailable ? "ViGEmBus: Ready" : "ViGEmBus: Not installed";
    public string HidHideStatusText => IsHidHideAvailable ? "HidHide: Ready" : "HidHide: Not installed";

    [ObservableProperty]
    private int _activeSlot = 1;

    [ObservableProperty]
    private int _activeShiftLayer = 0;

    [ObservableProperty]
    private bool _isRemappingActive = false;

    [ObservableProperty]
    private string _statusMessage = "Ready — Emulation Idle";

    [ObservableProperty]
    private ButtonViewModel? _selectedButton;

    // Mapping Inspector Properties
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

    // Collections for Inspector choices
    public ObservableCollection<ControllerButton> AvailableVirtualButtons { get; } = new();
    public ObservableCollection<string> AvailableKeyboardKeys { get; } = new();
    public ObservableCollection<string> AvailableMouseButtons { get; } = new();
    public IReadOnlyList<string> AvailableScrollDirections { get; } = new[] { ScrollUpLabel, ScrollDownLabel };
    public IReadOnlyList<MappingMode> AvailableModes { get; } = Enum.GetValues<MappingMode>();
    public IReadOnlyList<MacroRepeatMode> AvailableMacroRepeatModes { get; } = Enum.GetValues<MacroRepeatMode>();
    public ObservableCollection<ButtonViewModel> ControllerButtons { get; } = new();
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
        _pipeline = new InputPipeline(_mappingEngine, _reader, _virtualService, _injector, _macros, _macroPad);

        PopulateInspectorOptions();
        PopulateConnectedDevices();
        InitializeControllerButtons();

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
        SelectedProcessExecutable = value.ProcessExecutable ?? string.Empty;
        if (!value.ShiftLayers.ContainsKey(ActiveShiftLayer)) ActiveShiftLayer = 0;
        _mappingEngine.SetShiftLayer(ActiveShiftLayer);
        UpdateAllButtonDisplays();
    }

    partial void OnSelectedProcessExecutableChanged(string value)
    {
        SelectedProfile.ProcessExecutable = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

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

        string[] commonKeys = { "Space", "W", "A", "S", "D", "E", "R", "F", "ShiftKey", "ControlKey", "Tab", "Escape", "Q", "C", "V", "1", "2", "3", "4" };
        foreach (var k in commonKeys) AvailableKeyboardKeys.Add(k);

        string[] mouseButtons = { "Left Click", "Right Click", "Middle Click", "Wheel Up", "Wheel Down", "Mouse Button 4", "Mouse Button 5" };
        foreach (var m in mouseButtons) AvailableMouseButtons.Add(m);
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
        var vm = new ButtonViewModel
        {
            Button = btn,
            DisplayName = label,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            OnSelected = SelectButton
        };
        ControllerButtons.Add(vm);
    }

    public void SelectButton(ButtonViewModel button)
    {
        foreach (var b in ControllerButtons) b.IsSelected = false;
        button.IsSelected = true;
        SelectedButton = button;

        // Load existing mapping from active ShiftLayer
        var layer = SelectedProfile.ShiftLayers[ActiveShiftLayer];
        if (layer.Mappings.TryGetValue(button.Button, out var mapping))
        {
            SelectedTargetType = mapping.Target.Type;
            SelectedVirtualButton = mapping.Target.VirtualButton != ControllerButton.None ? mapping.Target.VirtualButton : ControllerButton.A;
            SelectedKeyboardKey = !string.IsNullOrEmpty(mapping.Target.KeyboardKey) ? mapping.Target.KeyboardKey : "Space";
            SelectedMouseButton = !string.IsNullOrEmpty(mapping.Target.MouseButton) ? mapping.Target.MouseButton : "Left Click";
            SelectedScrollDirection = mapping.Target.ScrollDelta < 0 ? ScrollDownLabel : ScrollUpLabel;
            MacroScript = mapping.Target.Macro?.ToScript() ?? string.Empty;
            SelectedMacroRepeat = mapping.Target.Macro?.RepeatMode ?? MacroRepeatMode.Once;
            MacroError = string.Empty;
            SelectedMode = mapping.Mode;
            TurboRateMs = mapping.TurboRateMs;
            IsMuted = mapping.IsMuted;
        }
        else
        {
            SelectedTargetType = TargetType.VirtualControllerButton;
            SelectedVirtualButton = button.Button;
            SelectedScrollDirection = ScrollUpLabel;
            MacroScript = string.Empty;
            SelectedMacroRepeat = MacroRepeatMode.Once;
            MacroError = string.Empty;
            SelectedMode = MappingMode.Normal;
            TurboRateMs = 50;
            IsMuted = false;
        }
    }

    public void UpdateAllButtonDisplays()
    {
        var layer = SelectedProfile.ShiftLayers.TryGetValue(ActiveShiftLayer, out var l)
            ? l
            : SelectedProfile.ShiftLayers[0];

        foreach (var btn in ControllerButtons)
        {
            if (layer.Mappings.TryGetValue(btn.Button, out var mapping) && mapping.Target.Type != TargetType.None)
            {
                btn.IsRemapped = mapping.Target.Type != TargetType.VirtualControllerButton || mapping.Target.VirtualButton != btn.Button;
                btn.TargetText = mapping.Target.DisplayText;
            }
            else
            {
                btn.IsRemapped = false;
                btn.TargetText = btn.DisplayName;
            }
        }
    }

    [RelayCommand]
    private void SaveCurrentMapping()
    {
        if (SelectedButton == null) return;

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
            Macro = macro
        };

        // The pipeline thread reads these mappings, so edit under the engine's lock
        lock (_mappingEngine.SyncRoot)
        {
            SelectedProfile.ShiftLayers[ActiveShiftLayer].Mappings[SelectedButton.Button] = new ButtonMapping
            {
                SourceButton = SelectedButton.Button,
                Target = target,
                Mode = SelectedMode,
                TurboRateMs = TurboRateMs,
                IsMuted = IsMuted
            };
        }

        UpdateAllButtonDisplays();
        StatusMessage = $"Mapped {SelectedButton.Button} -> {target.DisplayText}";
    }

    [RelayCommand]
    private void ResetCurrentMapping()
    {
        if (SelectedButton == null) return;

        lock (_mappingEngine.SyncRoot)
        {
            SelectedProfile.ShiftLayers[ActiveShiftLayer].Mappings[SelectedButton.Button] = new ButtonMapping
            {
                SourceButton = SelectedButton.Button,
                Target = RemapTarget.Default(SelectedButton.Button),
                Mode = MappingMode.Normal
            };
        }

        SelectButton(SelectedButton);
        UpdateAllButtonDisplays();
        StatusMessage = $"Reset {SelectedButton.Button} to default";
    }

    partial void OnActiveShiftLayerChanged(int value)
    {
        // The layer dropdown binds here; ignore indexes the profile doesn't have (e.g. -1 while the list refreshes)
        if (!SelectedProfile.ShiftLayers.TryGetValue(value, out var layer)) return;

        _mappingEngine.SetShiftLayer(value);
        UpdateAllButtonDisplays();
        if (SelectedButton != null) SelectButton(SelectedButton);
        StatusMessage = $"Switched to {layer.Name}";
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
            bool wantMute = MuteAll || HideInputDevice
                || SelectedProfile.ShiftLayers.Values.Any(l => l.Mappings.Values.Any(m => m.IsMuted));
            var devicesToHide = wantMute && _hidHide.IsAvailable ? _hidHide.GetPresentGamingDevices() : Array.Empty<string>();

            bool ok = _virtualService.ConnectVirtualXbox();
            _reader.ExcludedUserIndex = ok ? FindNewSlot(before) : -1;

            bool muted = ok && _hidHide.Hide(devicesToHide);

            _pipeline.Start();
            PopulateConnectedDevices();
            StatusMessage = ok
                ? "🟢 REMAPPING ACTIVE — Virtual Xbox 360 Controller Connected"
                : "🟡 REMAPPING ACTIVE — Virtual Output (ViGEm Driver not detected, running in simulation mode)";
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
