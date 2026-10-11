using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MappingEngine _mappingEngine;
    private readonly VirtualGamepadService _virtualService;
    private readonly XInputReader _reader = new();
    private readonly KeyboardMouseInjector _injector = new(new Win32InputBackend());
    private readonly InputPipeline _pipeline;

    [ObservableProperty]
    private ObservableCollection<Profile> _profiles = new();

    [ObservableProperty]
    private Profile _selectedProfile;

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
    private MappingMode _selectedMode = MappingMode.Normal;

    [ObservableProperty]
    private int _turboRateMs = 50;

    [ObservableProperty]
    private bool _isMuted = false;

    // Collections for Inspector choices
    public ObservableCollection<ControllerButton> AvailableVirtualButtons { get; } = new();
    public ObservableCollection<string> AvailableKeyboardKeys { get; } = new();
    public ObservableCollection<string> AvailableMouseButtons { get; } = new();
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
        _pipeline = new InputPipeline(_mappingEngine, _reader, _virtualService, _injector);

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
        if (!value.ShiftLayers.ContainsKey(ActiveShiftLayer)) ActiveShiftLayer = 0;
        _mappingEngine.SetShiftLayer(ActiveShiftLayer);
        UpdateAllButtonDisplays();
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
            SelectedMode = mapping.Mode;
            TurboRateMs = mapping.TurboRateMs;
            IsMuted = mapping.IsMuted;
        }
        else
        {
            SelectedTargetType = TargetType.VirtualControllerButton;
            SelectedVirtualButton = button.Button;
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

        var target = new RemapTarget
        {
            Type = SelectedTargetType,
            VirtualButton = SelectedTargetType == TargetType.VirtualControllerButton ? SelectedVirtualButton : ControllerButton.None,
            KeyboardKey = SelectedTargetType == TargetType.KeyboardKey ? SelectedKeyboardKey : string.Empty,
            MouseButton = SelectedTargetType == TargetType.MouseButton ? SelectedMouseButton : string.Empty
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

    [RelayCommand]
    private void SelectShiftLayer(string layerIndexStr)
    {
        if (int.TryParse(layerIndexStr, out int idx) && SelectedProfile.ShiftLayers.ContainsKey(idx))
        {
            ActiveShiftLayer = idx;
            _mappingEngine.SetShiftLayer(idx);
            UpdateAllButtonDisplays();
            if (SelectedButton != null) SelectButton(SelectedButton);
            StatusMessage = $"Switched to {SelectedProfile.ShiftLayers[idx].Name}";
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

    [RelayCommand]
    private void ToggleApply()
    {
        IsRemappingActive = !IsRemappingActive;
        _mappingEngine.IsActive = IsRemappingActive;

        if (IsRemappingActive)
        {
            // Snapshot XInput slots first: the virtual pad is the slot that appears after connecting
            var before = XInputReader.GetConnectedSlots();
            bool ok = _virtualService.ConnectVirtualXbox();
            _reader.ExcludedUserIndex = ok ? FindNewSlot(before) : -1;

            _pipeline.Start();
            PopulateConnectedDevices();
            StatusMessage = ok
                ? "🟢 REMAPPING ACTIVE — Virtual Xbox 360 Controller Connected"
                : "🟡 REMAPPING ACTIVE — Virtual Output (ViGEm Driver not detected, running in simulation mode)";
        }
        else
        {
            Shutdown();
            PopulateConnectedDevices();
            StatusMessage = "⚪ REMAPPING PAUSED — Emulation Disconnected";
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
