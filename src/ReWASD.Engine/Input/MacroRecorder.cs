using System.Diagnostics;
using ReWASD.Core.Models;

namespace ReWASD.Engine.Input;

/// <summary>
/// Turns live key, mouse and pad activity into a macro, with the pauses between events preserved.
/// Feed it events from any thread; it ignores releases of things that were already held when recording began,
/// so the click that starts a recording never ends up in it.
/// </summary>
public sealed class MacroRecorder
{
    /// <summary>Gaps shorter than this are treated as simultaneous.</summary>
    public const int MinRecordedDelayMs = 10;

    private readonly object _gate = new();
    private readonly Func<long> _clockMs;
    private readonly List<MacroStep> _steps = new();
    private readonly HashSet<string> _keysDown = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _mouseDown = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ControllerButton> _padDown = new();
    private long _lastEventMs;
    private bool _anyEvent;

    public MacroRecorder(Func<long>? clockMs = null)
    {
        _clockMs = clockMs ?? (() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
    }

    public bool IsRecording { get; private set; }

    /// <summary>Number of steps captured so far (excluding the pauses between them).</summary>
    public int StepCount
    {
        get { lock (_gate) return _steps.Count(s => s.Action != MacroActionType.Delay); }
    }

    public void Start()
    {
        lock (_gate)
        {
            _steps.Clear();
            _keysDown.Clear();
            _mouseDown.Clear();
            _padDown.Clear();
            _anyEvent = false;
            IsRecording = true;
        }
    }

    public void OnKey(string name, bool down)
    {
        lock (_gate)
        {
            if (!IsRecording) return;

            if (down)
            {
                if (!_keysDown.Add(name)) return; // auto-repeat while held
                Add(MacroActionType.KeyDown, name);
            }
            else
            {
                if (!_keysDown.Remove(name)) return; // released something pressed before recording started
                Add(MacroActionType.KeyUp, name);
            }
        }
    }

    public void OnMouse(string name, bool down)
    {
        lock (_gate)
        {
            if (!IsRecording) return;

            if (name.StartsWith("Wheel", StringComparison.OrdinalIgnoreCase))
            {
                if (down) Add(MacroActionType.Wheel, string.Empty, name.Contains("Up", StringComparison.OrdinalIgnoreCase) ? 1 : -1);
                return;
            }

            if (down)
            {
                if (!_mouseDown.Add(name)) return;
                Add(MacroActionType.MouseDown, name);
            }
            else
            {
                if (!_mouseDown.Remove(name)) return;
                Add(MacroActionType.MouseUp, name);
            }
        }
    }

    /// <summary>Call with the pad buttons currently held; presses and releases are worked out from the difference.</summary>
    public void OnPad(IReadOnlyCollection<ControllerButton> held)
    {
        lock (_gate)
        {
            if (!IsRecording) return;

            foreach (var b in held)
            {
                if (_padDown.Add(b)) Add(MacroActionType.PadDown, b.ToString());
            }

            foreach (var b in _padDown.Where(b => !held.Contains(b)).ToList())
            {
                _padDown.Remove(b);
                Add(MacroActionType.PadUp, b.ToString());
            }
        }
    }

    /// <summary>
    /// Ends the recording. <paramref name="dropStopClick"/> removes a trailing left click, which is the click on the
    /// Stop button itself. In HoldUntilRelease mode the trailing releases are dropped so the recorded inputs stay held.
    /// </summary>
    public MacroDefinition Stop(MacroRepeatMode mode, bool dropStopClick = true)
    {
        lock (_gate)
        {
            IsRecording = false;
            var steps = _steps.Select(s => s.Clone()).ToList();

            if (dropStopClick) DropTrailingLeftClick(steps);

            if (mode == MacroRepeatMode.HoldUntilRelease)
            {
                MacroDefinition.TrimTrailingReleases(steps);
            }
            else
            {
                // Don't leave anything stuck down when the macro ends. Work out what is still down from the steps
                // themselves, since dropping the stop click may have removed a press.
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var mouse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pad = new HashSet<string>();
                foreach (var s in steps)
                {
                    switch (s.Action)
                    {
                        case MacroActionType.KeyDown: keys.Add(s.Value); break;
                        case MacroActionType.KeyUp: keys.Remove(s.Value); break;
                        case MacroActionType.MouseDown: mouse.Add(s.Value); break;
                        case MacroActionType.MouseUp: mouse.Remove(s.Value); break;
                        case MacroActionType.PadDown: pad.Add(s.Value); break;
                        case MacroActionType.PadUp: pad.Remove(s.Value); break;
                    }
                }

                foreach (var k in keys) steps.Add(new MacroStep { Action = MacroActionType.KeyUp, Value = k });
                foreach (var m in mouse) steps.Add(new MacroStep { Action = MacroActionType.MouseUp, Value = m });
                foreach (var b in pad) steps.Add(new MacroStep { Action = MacroActionType.PadUp, Value = b });
            }

            return new MacroDefinition { RepeatMode = mode, Steps = steps };
        }
    }

    private static void DropTrailingLeftClick(List<MacroStep> steps)
    {
        // The Stop click arrives as [Delay] MouseDown:Left Click, [Delay] MouseUp:Left Click at the very end
        int i = steps.Count - 1;
        bool removedUp = false, removedDown = false;

        while (i >= 0)
        {
            var s = steps[i];
            if (s.Action == MacroActionType.Delay) { steps.RemoveAt(i); i--; continue; }
            if (!removedUp && s.Action == MacroActionType.MouseUp && IsLeftClick(s)) { steps.RemoveAt(i); removedUp = true; i--; continue; }
            if (removedUp && !removedDown && s.Action == MacroActionType.MouseDown && IsLeftClick(s)) { steps.RemoveAt(i); removedDown = true; i--; continue; }
            break;
        }

        // A click we only saw half of (down without up) is also the Stop click
        if (!removedUp && steps.Count > 0 && steps[^1].Action == MacroActionType.MouseDown && IsLeftClick(steps[^1])) steps.RemoveAt(steps.Count - 1);
        while (steps.Count > 0 && steps[^1].Action == MacroActionType.Delay) steps.RemoveAt(steps.Count - 1);
    }

    private static bool IsLeftClick(MacroStep s) => s.Value.Equals("Left Click", StringComparison.OrdinalIgnoreCase);

    private void Add(MacroActionType action, string value, int amount = 0)
    {
        long now = _clockMs();
        if (_anyEvent)
        {
            long gap = now - _lastEventMs;
            if (gap >= MinRecordedDelayMs) _steps.Add(new MacroStep { Action = MacroActionType.Delay, Amount = (int)Math.Min(gap, 600_000) });
        }

        _steps.Add(new MacroStep { Action = action, Value = value, Amount = amount });
        _lastEventMs = now;
        _anyEvent = true;
    }
}
