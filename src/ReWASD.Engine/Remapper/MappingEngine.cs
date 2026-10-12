using System.Diagnostics;
using ReWASD.Core.Models;

namespace ReWASD.Engine.Remapper;

public class RemappedState
{
    public HashSet<ControllerButton> ActiveVirtualButtons { get; } = new();
    public HashSet<string> ActiveKeyboardKeys { get; } = new();
    public HashSet<string> ActiveMouseButtons { get; } = new();

    /// <summary>Macros whose trigger is currently held; compared by reference, so the profile's instances persist across frames.</summary>
    public HashSet<MacroDefinition> ActiveMacros { get; } = new();
    public double LeftStickX { get; set; }
    public double LeftStickY { get; set; }
    public double RightStickX { get; set; }
    public double RightStickY { get; set; }
    public double LeftTrigger { get; set; }
    public double RightTrigger { get; set; }

    /// <summary>Layer requested by an active JumpToLayer mapping this frame; the engine applies it on the next frame.</summary>
    public int? RequestedLayer { get; set; }

    /// <summary>Cursor velocity from sticks in Mouse mode, in pixels per second (positive x = right, positive y = down).</summary>
    public double MouseVelocityX { get; set; }
    public double MouseVelocityY { get; set; }

    /// <summary>Wheel velocity from sticks in Scroll mode, in notches per second (positive = up).</summary>
    public double ScrollVelocity { get; set; }
}

/// <summary>
/// Turns physical input (pad, keyboard, mouse) into outputs. Each source runs through a small state machine that
/// decides which activator (single, long, double, triple, start, release) fires; the fired binding's mode
/// (hold, repeat, toggle, one-time) then decides how its target is held.
/// </summary>
public class MappingEngine
{
    // Sources that are read from analog values; their 1:1 pass-through must not become a digital press.
    private static readonly HashSet<ControllerButton> LeftStickDirections = new()
    {
        ControllerButton.LeftStickUp, ControllerButton.LeftStickDown,
        ControllerButton.LeftStickLeft, ControllerButton.LeftStickRight
    };

    private static readonly HashSet<ControllerButton> RightStickDirections = new()
    {
        ControllerButton.RightStickUp, ControllerButton.RightStickDown,
        ControllerButton.RightStickLeft, ControllerButton.RightStickRight
    };

    public Profile CurrentProfile { get; set; }
    public bool IsActive { get; set; } = false;
    public int ActiveShiftLayerIndex { get; private set; } = 0;

    /// <summary>While true the controller does nothing (see <see cref="Profile.LockChord"/>). Raised on the thread that processes input.</summary>
    public bool IsLocked { get; private set; }
    public event Action<bool>? LockChanged;

    private readonly ComboTracker _combos = new();
    private readonly HashSet<ComboMapping> _activeCombos = new();
    private long _lockChordSince = -1;
    private bool _lockChordSpent;

    /// <summary>
    /// Held while a frame is processed. Callers that edit the profile's mappings from another
    /// thread must lock on this so the polling loop never sees a half-updated dictionary.
    /// </summary>
    public object SyncRoot { get; } = new();

    // One press-tracking record per physical source
    private sealed class Tracker
    {
        public SourceKind Kind;
        public string Name = string.Empty;
        public ControllerButton Pad;
        public bool Down;
        public long PressStart;
        public long LastRelease;
        public int Taps;
        public bool LongFired;
        public long ResolveAt = -1;
        public ButtonMapping? Mapping; // captured at press so a layer change mid-hold doesn't change the outcome
    }

    // A fired activator that is currently producing output
    private sealed class Activation
    {
        public string Source = string.Empty;
        public ActivatorBinding Binding = new();
        public long FireAt;
        public long PressStart;
        public bool HoldWithSource;
    }

    private readonly Dictionary<string, Tracker> _trackers = new();
    private readonly List<Activation> _activations = new();
    private readonly Dictionary<string, ActivatorBinding> _latched = new();
    private readonly HashSet<int> _chordHeld = new();
    private readonly HashSet<int> _toggledLayers = new();
    private int? _pendingJump;
    private ShiftLayer? _currentLayer;
    private readonly Func<long> _clockMs;

    public MappingEngine(Profile? initialProfile = null, Func<long>? clockMs = null)
    {
        CurrentProfile = initialProfile ?? Profile.CreateDefault();
        _clockMs = clockMs ?? (() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
    }

    public void SetShiftLayer(int layerIndex)
    {
        lock (SyncRoot)
        {
            if (CurrentProfile.ShiftLayers.ContainsKey(layerIndex))
            {
                ActiveShiftLayerIndex = layerIndex;
            }
        }
    }

    /// <summary>Forgets presses, toggles and running activations (call when remapping stops).</summary>
    public void Reset()
    {
        lock (SyncRoot) ResetState();
    }

    /// <summary>True if the key has a mapping in the active layer, so the original keystroke should be swallowed.</summary>
    public bool IsKeySuppressed(string keyName)
    {
        lock (SyncRoot) return IsActive && _currentLayer != null && LookupKey(_currentLayer, keyName) != null;
    }

    /// <summary>True if the mouse button or wheel direction has a mapping in the active layer.</summary>
    public bool IsMouseSuppressed(string mouseName)
    {
        lock (SyncRoot) return IsActive && _currentLayer != null && LookupMouse(_currentLayer, mouseName) != null;
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
        return ProcessInputs(pressedButtons, Array.Empty<string>(), Array.Empty<string>(),
            rawLeftStickX, rawLeftStickY, rawRightStickX, rawRightStickY, rawLeftTrigger, rawRightTrigger);
    }

    public RemappedState ProcessInputs(
        HashSet<ControllerButton> pressedButtons,
        IReadOnlyCollection<string> pressedKeys,
        IReadOnlyCollection<string> pressedMouse,
        double rawLeftStickX,
        double rawLeftStickY,
        double rawRightStickX,
        double rawRightStickY,
        double rawLeftTrigger,
        double rawRightTrigger)
    {
        lock (SyncRoot)
        {
            return ProcessInputsCore(pressedButtons, pressedKeys, pressedMouse,
                rawLeftStickX, rawLeftStickY, rawRightStickX, rawRightStickY, rawLeftTrigger, rawRightTrigger);
        }
    }

    private void ResetState()
    {
        _trackers.Clear();
        _activations.Clear();
        _latched.Clear();
        _chordHeld.Clear();
        _toggledLayers.Clear();
        _combos.Reset();
        _pendingJump = null;
        _currentLayer = null;
    }

    private RemappedState ProcessInputsCore(
        HashSet<ControllerButton> pressedButtons,
        IReadOnlyCollection<string> pressedKeys,
        IReadOnlyCollection<string> pressedMouse,
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
            ResetState();

            // Direct pass-through when remapping is not active
            foreach (var b in pressedButtons)
            {
                if (!IsAnalogSource(b)) result.ActiveVirtualButtons.Add(b);
            }
            result.LeftStickX = rawLeftStickX;
            result.LeftStickY = rawLeftStickY;
            result.RightStickX = rawRightStickX;
            result.RightStickY = rawRightStickY;
            result.LeftTrigger = rawLeftTrigger;
            result.RightTrigger = rawRightTrigger;
            return result;
        }

        long now = _clockMs();
        var timings = CurrentProfile.Timings ?? new ActivatorTimings();

        // Locked: the pad is ignored (everything it held is let go on this frame); keyboard and mouse mappings carry on
        pressedButtons = ApplyLock(pressedButtons, now);
        if (IsLocked)
        {
            pressedButtons = new HashSet<ControllerButton>();
            rawLeftStickX = rawLeftStickY = rawRightStickX = rawRightStickY = rawLeftTrigger = rawRightTrigger = 0;
        }

        // Chords and sequences: they take their buttons away from the ordinary mappings, and fire on their own
        _activeCombos.Clear();
        pressedButtons = _combos.Process(pressedButtons, now, CurrentProfile.Combos ?? new List<ComboMapping>(),
            Math.Max(0, CurrentProfile.ChordWindowMs), timings.PulseMs, _activeCombos);

        // 1. Pick the active layer (and note which buttons were spent on switching it)
        var consumed = new HashSet<ControllerButton>();
        var layer = ResolveLayer(pressedButtons, consumed);
        _currentLayer = layer;
        ActiveShiftLayerIndex = layer.LayerIndex;

        // 2. Gather every source that is down, plus trackers still waiting to resolve
        var sources = new List<(string Key, SourceKind Kind, string Name, ControllerButton Pad, bool Down)>();
        var seen = new HashSet<string>();

        foreach (var b in pressedButtons)
        {
            if (consumed.Contains(b)) continue;
            string key = "pad:" + b;
            sources.Add((key, SourceKind.Pad, b.ToString(), b, true));
            seen.Add(key);
        }
        foreach (var k in pressedKeys)
        {
            string key = "key:" + k.ToLowerInvariant();
            if (seen.Add(key)) sources.Add((key, SourceKind.Key, k, ControllerButton.None, true));
        }
        foreach (var m in pressedMouse)
        {
            string key = "mouse:" + m.ToLowerInvariant();
            if (seen.Add(key)) sources.Add((key, SourceKind.Mouse, m, ControllerButton.None, true));
        }
        foreach (var combo in _activeCombos)
        {
            string key = "combo:" + combo.Id;
            if (seen.Add(key)) sources.Add((key, SourceKind.Combo, combo.Id.ToString(), ControllerButton.None, true));
        }
        foreach (var (key, tracker) in _trackers)
        {
            if (!seen.Contains(key)) sources.Add((key, tracker.Kind, tracker.Name, tracker.Pad, false));
        }

        // 3. Run each source through the activator state machine
        foreach (var (key, kind, name, pad, down) in sources)
        {
            var current = kind == SourceKind.Combo ? ComboSource(name) : LookupSource(layer, kind, name, pad);

            if (!_trackers.TryGetValue(key, out var tracker))
            {
                if (!down) continue;
                tracker = new Tracker { Kind = kind, Name = name, Pad = pad, LastRelease = long.MinValue / 2 };
                _trackers[key] = tracker;
            }

            var mapping = (down && !tracker.Down) ? current : tracker.Mapping ?? current;

            if (mapping == null)
            {
                // Unmapped: a pad button passes straight through, keys and mouse buttons are left to the OS
                if (down && kind == SourceKind.Pad && !IsAnalogSource(pad)) result.ActiveVirtualButtons.Add(pad);
                if (!down) _trackers.Remove(key);
                else tracker.Down = true;
                continue;
            }

            // 1:1 triggers and stick directions are carried by the analog values instead
            if (kind == SourceKind.Pad && IsAnalogSource(pad) && mapping.Activators.Count == 0 && IsIdentity(mapping, pad))
            {
                _trackers.Remove(key);
                continue;
            }

            Tick(key, tracker, mapping, down, now, timings);
        }

        // 4. Produce output from activations and latched toggles
        for (int i = _activations.Count - 1; i >= 0; i--)
        {
            var a = _activations[i];
            bool held = _trackers.TryGetValue(a.Source, out var t) && t.Down && t.PressStart == a.PressStart;
            bool on = EvaluateActivation(a, held, now, timings.PulseMs, out bool expired);
            if (on) ApplyBinding(a.Binding, result);
            if (expired) _activations.RemoveAt(i);
        }

        foreach (var binding in _latched.Values) ApplyBinding(binding, result);

        _pendingJump = result.RequestedLayer;

        // 5. Process Analog Sticks with Deadzones & Curves.
        // A stick whose direction buttons were remapped elsewhere (e.g. to WASD) stops driving the virtual stick.
        bool leftStickFree = !AnyRemappedAway(layer, LeftStickDirections);
        bool rightStickFree = !AnyRemappedAway(layer, RightStickDirections);

        if (leftStickFree)
        {
            ApplyStick(CurrentProfile.LeftStick, layer.LeftStickMode, rawLeftStickX, rawLeftStickY, pressedButtons, result, left: true);
        }

        if (rightStickFree)
        {
            ApplyStick(CurrentProfile.RightStick, layer.RightStickMode, rawRightStickX, rawRightStickY, pressedButtons, result, left: false);
        }

        // 6. Process Triggers: a remapped trigger no longer drives the analog value
        result.LeftTrigger = IsRemappedAway(layer, ControllerButton.LeftTrigger) ? 0.0 : rawLeftTrigger;
        result.RightTrigger = IsRemappedAway(layer, ControllerButton.RightTrigger) ? 0.0 : rawRightTrigger;

        return result;
    }

    // Holding the lock chord long enough flips the lock once; the chord's buttons never reach the mappings while it is held
    private HashSet<ControllerButton> ApplyLock(HashSet<ControllerButton> pressed, long now)
    {
        var chord = CurrentProfile.LockChord;
        if (chord == null || chord.Count == 0 || !chord.All(pressed.Contains))
        {
            _lockChordSince = -1;
            _lockChordSpent = false;
            return pressed;
        }

        if (_lockChordSince < 0) _lockChordSince = now;
        if (!_lockChordSpent && now - _lockChordSince >= Math.Max(200, CurrentProfile.LockHoldMs))
        {
            _lockChordSpent = true;
            IsLocked = !IsLocked;
            LockChanged?.Invoke(IsLocked);
        }

        return pressed.Where(b => !chord.Contains(b)).ToHashSet();
    }

    /// <summary>Locks or unlocks by hand (the window's lock button).</summary>
    public void SetLocked(bool locked)
    {
        lock (SyncRoot)
        {
            if (IsLocked == locked) return;
            IsLocked = locked;
        }
        LockChanged?.Invoke(locked);
    }

    /// <summary>Runs one stick through its deadzone and curve, then hands it to the virtual pad, the cursor, the wheel or the keys.</summary>
    private static void ApplyStick(StickSettings settings, StickMode? layerMode, double rawX, double rawY,
        HashSet<ControllerButton> pressedButtons, RemappedState result, bool left)
    {
        var mode = layerMode ?? settings.Mode;
        var (x, y) = settings.TransformStick(rawX, rawY);
        if (settings.InvertX) x = -x;
        if (settings.InvertY) y = -y;

        double precision = settings.PrecisionButton != ControllerButton.None && pressedButtons.Contains(settings.PrecisionButton)
            ? Math.Clamp(settings.PrecisionFactor, 0.05, 1.0)
            : 1.0;

        switch (mode)
        {
            case StickMode.Mouse:
                // Stick up is positive; the screen's y grows downward
                result.MouseVelocityX += x * settings.MouseSpeed * precision;
                result.MouseVelocityY += -y * settings.MouseSpeed * precision;
                break;

            case StickMode.Scroll:
                result.ScrollVelocity += y * settings.ScrollSpeed * precision;
                break;

            case StickMode.Keys:
                double t = Math.Clamp(settings.KeyThreshold, 0.1, 0.95);
                AddKey(result, settings.KeyUp, y >= t);
                AddKey(result, settings.KeyDown, y <= -t);
                AddKey(result, settings.KeyLeft, x <= -t);
                AddKey(result, settings.KeyRight, x >= t);
                break;

            default:
                if (left) (result.LeftStickX, result.LeftStickY) = (x, y);
                else (result.RightStickX, result.RightStickY) = (x, y);
                break;
        }
    }

    private static void AddKey(RemappedState result, string key, bool held)
    {
        if (held && !string.IsNullOrWhiteSpace(key)) result.ActiveKeyboardKeys.Add(key);
    }

    // ---- Layers ------------------------------------------------------------------------------------------------

    private ShiftLayer ResolveLayer(HashSet<ControllerButton> pad, HashSet<ControllerButton> consumed)
    {
        var layers = CurrentProfile.ShiftLayers;
        int effective = 0;

        foreach (var l in layers.Values)
        {
            if (l.LayerIndex <= 0 || l.ModifierButtons.Count == 0) continue;

            bool held = l.ModifierButtons.All(pad.Contains);
            bool wasHeld = _chordHeld.Contains(l.LayerIndex);

            if (held)
            {
                _chordHeld.Add(l.LayerIndex);
                foreach (var b in l.ModifierButtons) consumed.Add(b);
            }
            else
            {
                _chordHeld.Remove(l.LayerIndex);
            }

            bool active;
            if (l.Type == ShiftType.Toggle)
            {
                if (held && !wasHeld && !_toggledLayers.Remove(l.LayerIndex)) _toggledLayers.Add(l.LayerIndex);
                active = _toggledLayers.Contains(l.LayerIndex);
            }
            else
            {
                active = held;
            }

            if (active && l.LayerIndex > effective) effective = l.LayerIndex;
        }

        // A JumpToLayer mapping from the previous frame wins over the held chords
        if (_pendingJump is int jump && layers.ContainsKey(jump)) effective = jump;

        return layers.TryGetValue(effective, out var found) ? found : layers[0];
    }

    // A shift layer inherits everything it does not override from the Main layer
    private ButtonMapping? LookupSource(ShiftLayer layer, SourceKind kind, string name, ControllerButton pad) => kind switch
    {
        SourceKind.Pad => LookupPad(layer, pad),
        SourceKind.Key => LookupKey(layer, name),
        _ => LookupMouse(layer, name)
    };

    // A combo is run through the same activator machinery as any button, with its own action as the single outcome
    private ButtonMapping? ComboSource(string id)
    {
        var combo = CurrentProfile.Combos?.FirstOrDefault(c => c.Id.ToString() == id);
        if (combo == null) return null;
        return new ButtonMapping { Target = combo.Binding.Target, Mode = combo.Binding.Mode, TurboRateMs = combo.Binding.TurboRateMs };
    }

    private ButtonMapping? LookupPad(ShiftLayer layer, ControllerButton button)
    {
        if (layer.Mappings.TryGetValue(button, out var m)) return m;
        if (layer.LayerIndex != 0 && CurrentProfile.ShiftLayers.TryGetValue(0, out var main) && main.Mappings.TryGetValue(button, out m)) return m;
        return null;
    }

    private ButtonMapping? LookupKey(ShiftLayer layer, string name)
    {
        if (layer.KeyboardMappings.TryGetValue(name, out var m)) return m;
        if (layer.LayerIndex != 0 && CurrentProfile.ShiftLayers.TryGetValue(0, out var main) && main.KeyboardMappings.TryGetValue(name, out m)) return m;
        return null;
    }

    private ButtonMapping? LookupMouse(ShiftLayer layer, string name)
    {
        if (layer.MouseMappings.TryGetValue(name, out var m)) return m;
        if (layer.LayerIndex != 0 && CurrentProfile.ShiftLayers.TryGetValue(0, out var main) && main.MouseMappings.TryGetValue(name, out m)) return m;
        return null;
    }

    // ---- Activators --------------------------------------------------------------------------------------------

    private void Tick(string key, Tracker t, ButtonMapping mapping, bool down, long now, ActivatorTimings timings)
    {
        var single = mapping.GetBinding(ActivatorType.Single);
        var longB = mapping.GetBinding(ActivatorType.Long);
        var doubleB = mapping.GetBinding(ActivatorType.Double);
        var tripleB = mapping.GetBinding(ActivatorType.Triple);
        var startB = mapping.GetBinding(ActivatorType.Start);
        var releaseB = mapping.GetBinding(ActivatorType.Release);

        int maxTap = tripleB != null ? 3 : doubleB != null ? 2 : 1;
        bool hasLong = longB != null;

        ActivatorBinding? ForTaps(int taps) => taps switch { 1 => single, 2 => doubleB, 3 => tripleB, _ => null };

        // Press edge
        if (down && !t.Down)
        {
            t.Down = true;
            t.PressStart = now;
            t.Mapping = mapping;
            t.LongFired = false;

            bool continuing = t.Taps > 0 && t.ResolveAt >= 0 && now - t.LastRelease <= timings.MultiPressWindowMs;
            t.Taps = continuing ? t.Taps + 1 : 1;
            if (t.Taps > maxTap) t.Taps = 1;
            t.ResolveAt = -1;

            if (startB != null) Fire(key, t, startB, now, holdWithSource: true);

            // The final tap of a sequence fires on press so it can be held. A lone Single must wait if a Long exists.
            if (t.Taps == maxTap && !(t.Taps == 1 && hasLong))
            {
                var b = ForTaps(t.Taps);
                if (b != null) Fire(key, t, b, now, holdWithSource: true);
            }
        }

        // Held long enough for the Long activator
        if (down && t.Down && hasLong && !t.LongFired && t.Taps == 1 && now - t.PressStart >= timings.LongPressMs)
        {
            t.LongFired = true;
            Fire(key, t, longB!, now, holdWithSource: true);
        }

        // Release edge
        if (!down && t.Down)
        {
            t.Down = false;
            t.LastRelease = now;

            if (releaseB != null) Fire(key, t, releaseB, now, holdWithSource: false);

            if (t.LongFired)
            {
                t.Taps = 0;
                t.LongFired = false;
            }
            else if (t.Taps == maxTap)
            {
                // Already fired on press, except a lone Single that was waiting on the Long threshold
                if (t.Taps == 1 && hasLong && single != null) Fire(key, t, single, now, holdWithSource: false);
                t.Taps = 0;
            }
            else
            {
                t.ResolveAt = now + timings.MultiPressWindowMs; // wait to see whether another press follows
            }
        }

        // Window expired with no further press: fire whatever the tap count reached
        if (!down && t.ResolveAt >= 0 && now >= t.ResolveAt)
        {
            var b = ForTaps(t.Taps);
            if (b != null) Fire(key, t, b, now, holdWithSource: false);
            t.Taps = 0;
            t.ResolveAt = -1;
        }

        if (!t.Down && t.ResolveAt < 0 && t.Taps == 0) _trackers.Remove(key);
    }

    private void Fire(string sourceKey, Tracker t, ActivatorBinding binding, long now, bool holdWithSource)
    {
        if (binding.Mode == MappingMode.Toggle)
        {
            string latchKey = sourceKey + "|" + binding.Type;
            if (!_latched.Remove(latchKey)) _latched[latchKey] = binding;
            return;
        }

        _activations.Add(new Activation
        {
            Source = sourceKey,
            Binding = binding,
            FireAt = now,
            PressStart = t.PressStart,
            HoldWithSource = holdWithSource
        });
    }

    private static bool EvaluateActivation(Activation a, bool sourceHeld, long now, int pulseMs, out bool expired)
    {
        long age = now - a.FireAt;
        bool inPulse = age < pulseMs;
        bool holding = a.HoldWithSource && sourceHeld;

        switch (a.Binding.Mode)
        {
            case MappingMode.OneTime:
                expired = !inPulse;
                return inPulse;

            case MappingMode.Turbo:
                expired = a.HoldWithSource ? !sourceHeld : !inPulse;
                if (!holding) return !a.HoldWithSource && inPulse;
                long rate = Math.Max(1, a.Binding.TurboRateMs);
                return (age / rate) % 2 == 0;

            default: // Normal: held for as long as the source is held, or a short pulse if it already let go
                expired = a.HoldWithSource ? !sourceHeld : !inPulse;
                return a.HoldWithSource ? sourceHeld : inPulse;
        }
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    private static bool IsAnalogSource(ControllerButton b) =>
        b is ControllerButton.LeftTrigger or ControllerButton.RightTrigger
        || LeftStickDirections.Contains(b) || RightStickDirections.Contains(b);

    private static bool IsIdentity(ButtonMapping mapping, ControllerButton source) =>
        mapping.Mode == MappingMode.Normal
        && mapping.Activators.Count == 0
        && mapping.Target.Type == TargetType.VirtualControllerButton
        && mapping.Target.VirtualButton == source;

    private bool IsRemappedAway(ShiftLayer layer, ControllerButton button)
    {
        var m = LookupPad(layer, button);
        return m != null && !IsIdentity(m, button);
    }

    private bool AnyRemappedAway(ShiftLayer layer, IEnumerable<ControllerButton> buttons) =>
        buttons.Any(b => IsRemappedAway(layer, b));

    private static void ApplyBinding(ActivatorBinding binding, RemappedState state)
    {
        var target = binding.Target;
        switch (target.Type)
        {
            case TargetType.VirtualControllerButton:
                if (target.VirtualButton != ControllerButton.None)
                {
                    state.ActiveVirtualButtons.Add(target.VirtualButton);
                }
                break;

            case TargetType.KeyboardKey:
                if (!string.IsNullOrEmpty(target.KeyboardKey))
                {
                    state.ActiveKeyboardKeys.Add(target.KeyboardKey);
                }
                break;

            case TargetType.MouseButton:
                if (!string.IsNullOrEmpty(target.MouseButton))
                {
                    state.ActiveMouseButtons.Add(target.MouseButton);
                }
                break;

            case TargetType.MouseScroll:
                // Scrolling is expressed as a wheel "button" so the injector fires one notch per press edge (and Turbo repeats it)
                if (target.ScrollDelta != 0)
                {
                    state.ActiveMouseButtons.Add(target.ScrollDelta > 0 ? "Wheel Up" : "Wheel Down");
                }
                break;

            case TargetType.Macro:
                if (target.Macro is { Steps.Count: > 0 } macro)
                {
                    state.ActiveMacros.Add(macro);
                }
                break;

            case TargetType.JumpToLayer:
                state.RequestedLayer = target.Layer;
                break;
        }
    }
}
