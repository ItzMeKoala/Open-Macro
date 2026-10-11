using System.Collections.Concurrent;
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

    // Toggle states for buttons configured with MappingMode.Toggle
    private readonly ConcurrentDictionary<ControllerButton, bool> _toggleStates = new();
    
    // Turbo timers tracking
    private readonly ConcurrentDictionary<ControllerButton, long> _turboLastTicks = new();

    public MappingEngine(Profile? initialProfile = null)
    {
        CurrentProfile = initialProfile ?? Profile.CreateDefault();
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

        // 2. Process Button Mappings
        foreach (var button in pressedButtons)
        {
            if (activeLayer.Mappings.TryGetValue(button, out var mapping))
            {
                ApplyMapping(mapping, result);
            }
            else
            {
                // Fall back to 1:1 if not explicitly remapped
                result.ActiveVirtualButtons.Add(button);
            }
        }

        // 3. Process Analog Sticks with Deadzones & Curves
        result.LeftStickX = CurrentProfile.LeftStick.TransformAxis(rawLeftStickX);
        result.LeftStickY = CurrentProfile.LeftStick.TransformAxis(rawLeftStickY);
        result.RightStickX = CurrentProfile.RightStick.TransformAxis(rawRightStickX);
        result.RightStickY = CurrentProfile.RightStick.TransformAxis(rawRightStickY);

        // 4. Process Triggers
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
