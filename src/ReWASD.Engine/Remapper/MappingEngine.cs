using System.Diagnostics;
using ReWASD.Core.Models;

namespace ReWASD.Engine.Remapper;

public class RemappedState
{
    public HashSet<ControllerButton> ActiveVirtualButtons { get; } = new();
    public HashSet<string> ActiveKeyboardKeys { get; } = new();
    public HashSet<string> ActiveMouseButtons { get; } = new();
    public double LeftStickX { get; set; }
    public double LeftStickY { get; set; }
    public double RightStickX { get; set; }
    public double RightStickY { get; set; }
    public double LeftTrigger { get; set; }
    public double RightTrigger { get; set; }
}

public class MappingEngine
{
    public Profile CurrentProfile { get; set; }
    public bool IsActive { get; set; } = false;
    public int ActiveShiftLayerIndex { get; private set; } = 0;

    // Not thread-safe: ProcessInputs is expected to be called from a single polling loop.
    private readonly Dictionary<ControllerButton, bool> _toggleStates = new();
    private readonly Dictionary<ControllerButton, long> _pressStartMs = new();
    private HashSet<ControllerButton> _previousPressed = new();
    private readonly Func<long> _clockMs;

    public MappingEngine(Profile? initialProfile = null, Func<long>? clockMs = null)
    {
        CurrentProfile = initialProfile ?? Profile.CreateDefault();
        _clockMs = clockMs ?? (() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
    }

    public void SetShiftLayer(int layerIndex)
    {
        if (CurrentProfile.ShiftLayers.ContainsKey(layerIndex))
        {
            ActiveShiftLayerIndex = layerIndex;
        }
    }

    public RemappedState ProcessInputs(
        HashSet<ControllerButton> pressedButtons,
        double rawLeftStickX,
        double rawLeftStickY,
        double rawRightStickX,
        double rawRightStickY,
        double rawLeftTrigger,
        double rawRightTrigger)
    {
        var result = new RemappedState();

        if (!IsActive)
        {
            // Direct pass-through when remapping is not active
            foreach (var b in pressedButtons) result.ActiveVirtualButtons.Add(b);
            result.LeftStickX = rawLeftStickX;
            result.LeftStickY = rawLeftStickY;
            result.RightStickX = rawRightStickX;
            result.RightStickY = rawRightStickY;
            result.LeftTrigger = rawLeftTrigger;
            result.RightTrigger = rawRightTrigger;
            return result;
        }

        // 1. Determine active shift layer based on held modifier buttons
        int effectiveLayer = 0;
        foreach (var layer in CurrentProfile.ShiftLayers.Values)
        {
            if (layer.LayerIndex > 0 && layer.ModifierButton != ControllerButton.None)
            {
                if (pressedButtons.Contains(layer.ModifierButton))
                {
                    effectiveLayer = layer.LayerIndex;
                    break;
                }
            }
        }
        ActiveShiftLayerIndex = effectiveLayer;

        var activeLayer = CurrentProfile.ShiftLayers.TryGetValue(effectiveLayer, out var layerObj)
            ? layerObj
            : CurrentProfile.ShiftLayers[0];

        // 2. Track press edges for Turbo timing and Toggle state
        long now = _clockMs();
        foreach (var button in pressedButtons)
        {
            if (_previousPressed.Contains(button)) continue;
            _pressStartMs[button] = now;
            if (activeLayer.Mappings.TryGetValue(button, out var pressed) && pressed.Mode == MappingMode.Toggle)
            {
                _toggleStates[button] = !_toggleStates.GetValueOrDefault(button);
            }
        }
        foreach (var button in _previousPressed)
        {
            if (!pressedButtons.Contains(button)) _pressStartMs.Remove(button);
        }
        _previousPressed = new HashSet<ControllerButton>(pressedButtons);

        // 3. Process Button Mappings
        foreach (var button in pressedButtons)
        {
            if (!activeLayer.Mappings.TryGetValue(button, out var mapping))
            {
                // Fall back to 1:1 if not explicitly remapped
                result.ActiveVirtualButtons.Add(button);
                continue;
            }

            switch (mapping.Mode)
            {
                case MappingMode.Turbo:
                    long rate = Math.Max(1, mapping.TurboRateMs);
                    if (((now - _pressStartMs[button]) / rate) % 2 == 0) ApplyMapping(mapping, result);
                    break;
                case MappingMode.Toggle:
                    break; // Output is driven by toggle state below, not by the physical hold
                default:
                    ApplyMapping(mapping, result);
                    break;
            }
        }

        // Toggled-on buttons stay active after release
        foreach (var (button, isOn) in _toggleStates)
        {
            if (isOn && activeLayer.Mappings.TryGetValue(button, out var toggled) && toggled.Mode == MappingMode.Toggle)
            {
                ApplyMapping(toggled, result);
            }
        }

        // 4. Process Analog Sticks with Deadzones & Curves
        result.LeftStickX = CurrentProfile.LeftStick.TransformAxis(rawLeftStickX);
        result.LeftStickY = CurrentProfile.LeftStick.TransformAxis(rawLeftStickY);
        result.RightStickX = CurrentProfile.RightStick.TransformAxis(rawRightStickX);
        result.RightStickY = CurrentProfile.RightStick.TransformAxis(rawRightStickY);

        if (CurrentProfile.LeftStick.InvertX) result.LeftStickX = -result.LeftStickX;
        if (CurrentProfile.LeftStick.InvertY) result.LeftStickY = -result.LeftStickY;
        if (CurrentProfile.RightStick.InvertX) result.RightStickX = -result.RightStickX;
        if (CurrentProfile.RightStick.InvertY) result.RightStickY = -result.RightStickY;

        // 5. Process Triggers
        result.LeftTrigger = rawLeftTrigger;
        result.RightTrigger = rawRightTrigger;

        return result;
    }

    private void ApplyMapping(ButtonMapping mapping, RemappedState state)
    {
        switch (mapping.Target.Type)
        {
            case TargetType.VirtualControllerButton:
                if (mapping.Target.VirtualButton != ControllerButton.None)
                {
                    state.ActiveVirtualButtons.Add(mapping.Target.VirtualButton);
                }
                break;

            case TargetType.KeyboardKey:
                if (!string.IsNullOrEmpty(mapping.Target.KeyboardKey))
                {
                    state.ActiveKeyboardKeys.Add(mapping.Target.KeyboardKey);
                }
                break;

            case TargetType.MouseButton:
                if (!string.IsNullOrEmpty(mapping.Target.MouseButton))
                {
                    state.ActiveMouseButtons.Add(mapping.Target.MouseButton);
                }
                break;
        }
    }
}
