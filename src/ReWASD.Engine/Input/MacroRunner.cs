using ReWASD.Core.Models;
using ReWASD.Engine.Actions;

namespace ReWASD.Engine.Input;

/// <summary>
/// Starts macros on the press edge of their trigger and cancels WhileHeld macros on release.
/// Each macro runs on its own task so the polling loop never blocks on a Delay step.
/// </summary>
public sealed class MacroRunner : IDisposable
{
    private const int TapHoldMs = 15;
    private const int MouseMoveStepMs = 1;
    private const int TypeKeyMs = 8;

    private readonly IInputBackend _backend;
    private readonly IPadOutput? _pad;
    private readonly IRumbleOutput? _rumble;
    private readonly ISystemActions? _system;
    private readonly object _gate = new();
    private readonly Dictionary<MacroDefinition, CancellationTokenSource> _running = new();
    private HashSet<MacroDefinition> _previouslyHeld = new();

    public MacroRunner(IInputBackend backend, IPadOutput? pad = null, IRumbleOutput? rumble = null, ISystemActions? system = null)
    {
        _backend = backend;
        _pad = pad;
        _rumble = rumble;
        _system = system;
    }

    /// <summary>Raised when a macro step that talks to the outside world fails (no network, OBS not running...), so the app can say so.</summary>
    public event Action<string>? ActionFailed;

    /// <summary>Raised each time a macro starts running (for usage counts). May come from the polling thread.</summary>
    public event Action? MacroStarted;

    /// <summary>Everything a single run has pressed or set, so it can be undone however the run ends.</summary>
    private sealed class RunState
    {
        public readonly HashSet<ushort> Keys = new();
        public readonly HashSet<MouseButtonKind> Buttons = new();
        public readonly HashSet<ControllerButton> PadButtons = new();
        public readonly HashSet<string> Axes = new(StringComparer.OrdinalIgnoreCase);
        public bool Rumbling;
    }

    /// <summary>Call once per frame with the macros whose triggers are currently held.</summary>
    public void Update(IReadOnlyCollection<MacroDefinition> held)
    {
        foreach (var macro in held)
        {
            if (!_previouslyHeld.Contains(macro)) Start(macro);
        }

        foreach (var macro in _previouslyHeld)
        {
            // Looping and hold-style macros end with the trigger; a Once macro always finishes
            if (!held.Contains(macro) && macro.RepeatMode is MacroRepeatMode.WhileHeld or MacroRepeatMode.HoldUntilRelease) Cancel(macro);
        }

        _previouslyHeld = new HashSet<MacroDefinition>(held);
    }

    /// <summary>Cancels every running macro; held keys are released as each task unwinds.</summary>
    public void CancelAll()
    {
        List<MacroDefinition> running;
        lock (_gate) running = _running.Keys.ToList();
        foreach (var macro in running) Cancel(macro);
        _previouslyHeld.Clear();
    }

    private void Start(MacroDefinition macro)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            // Restarting a macro that is still running replaces it
            if (_running.Remove(macro, out var old)) old.Cancel();
            _running[macro] = cts;
        }
        MacroStarted?.Invoke();
        _ = RunAsync(macro, cts);
    }

    private void Cancel(MacroDefinition macro)
    {
        lock (_gate)
        {
            if (_running.Remove(macro, out var cts)) cts.Cancel();
        }
    }

    private async Task RunAsync(MacroDefinition macro, CancellationTokenSource cts)
    {
        var token = cts.Token;
        var state = new RunState();

        try
        {
            bool broke = false;
            do
            {
                foreach (var step in macro.Steps)
                {
                    token.ThrowIfCancellationRequested();
                    if (await ExecuteAsync(step, state, token))
                    {
                        broke = true;
                        break;
                    }
                }

                // Yield between loops so a macro with no Delay step can't spin a core
                if (!broke && macro.RepeatMode == MacroRepeatMode.WhileHeld) await Task.Delay(1, token);
            }
            while (!broke && macro.RepeatMode == MacroRepeatMode.WhileHeld);

            // Hold-style macros keep what they pressed down until the trigger is released (which cancels this wait)
            if (!broke && macro.RepeatMode == MacroRepeatMode.HoldUntilRelease) await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException)
        {
            // Normal path when the trigger is released
        }
        finally
        {
            foreach (var vk in state.Keys) _backend.KeyUp(vk);
            foreach (var b in state.Buttons) _backend.MouseUp(b);
            foreach (var b in state.PadButtons) _pad?.SetButton(b, false);
            foreach (var axis in state.Axes) _pad?.SetAxis(axis, null);
            if (state.Rumbling) _rumble?.Rumble(0, 0);

            // Drop our registration unless a newer run already replaced it
            lock (_gate)
            {
                if (_running.TryGetValue(macro, out var current) && ReferenceEquals(current, cts)) _running.Remove(macro);
            }
            cts.Dispose();
        }
    }

    /// <summary>Runs one step. Returns true if the step ends the macro (Break).</summary>
    private async Task<bool> ExecuteAsync(MacroStep step, RunState s, CancellationToken token)
    {
        switch (step.Action)
        {
            case MacroActionType.Break:
                return true;

            case MacroActionType.Delay:
                if (step.Amount > 0) await Task.Delay(step.Amount, token);
                break;

            case MacroActionType.Wheel:
                if (step.Amount != 0) _backend.MouseWheel(step.Amount * KeyMap.WheelNotch);
                break;

            case MacroActionType.MouseMove:
                await MoveMouseAsync(step, token);
                break;

            case MacroActionType.KeyDown:
                if (KeyMap.TryGetVirtualKey(step.Value, out var down) && s.Keys.Add(down)) _backend.KeyDown(down);
                break;

            case MacroActionType.KeyUp:
                if (KeyMap.TryGetVirtualKey(step.Value, out var up) && s.Keys.Remove(up)) _backend.KeyUp(up);
                break;

            case MacroActionType.KeyTap:
                if (KeyMap.TryGetVirtualKey(step.Value, out var tap))
                {
                    s.Keys.Add(tap);
                    _backend.KeyDown(tap);
                    await Task.Delay(TapHoldMs, token);
                    _backend.KeyUp(tap);
                    s.Keys.Remove(tap);
                }
                break;

            case MacroActionType.MouseDown:
                if (KeyMap.TryResolveMouse(step.Value, out var b1, out _) && b1 is { } down1 && s.Buttons.Add(down1)) _backend.MouseDown(down1);
                break;

            case MacroActionType.MouseUp:
                if (KeyMap.TryResolveMouse(step.Value, out var b2, out _) && b2 is { } up2 && s.Buttons.Remove(up2)) _backend.MouseUp(up2);
                break;

            case MacroActionType.MouseClick:
                if (KeyMap.TryResolveMouse(step.Value, out var b3, out _) && b3 is { } click)
                {
                    s.Buttons.Add(click);
                    _backend.MouseDown(click);
                    await Task.Delay(TapHoldMs, token);
                    _backend.MouseUp(click);
                    s.Buttons.Remove(click);
                }
                break;

            case MacroActionType.PadDown:
                if (Enum.TryParse<ControllerButton>(step.Value, true, out var padDown) && s.PadButtons.Add(padDown)) _pad?.SetButton(padDown, true);
                break;

            case MacroActionType.PadUp:
                if (Enum.TryParse<ControllerButton>(step.Value, true, out var padUp) && s.PadButtons.Remove(padUp)) _pad?.SetButton(padUp, false);
                break;

            case MacroActionType.PadTap:
                if (Enum.TryParse<ControllerButton>(step.Value, true, out var padTap))
                {
                    s.PadButtons.Add(padTap);
                    _pad?.SetButton(padTap, true);
                    await Task.Delay(TapHoldMs, token);
                    _pad?.SetButton(padTap, false);
                    s.PadButtons.Remove(padTap);
                }
                break;

            case MacroActionType.PadAxis:
                if (_pad != null)
                {
                    s.Axes.Add(step.Value);
                    _pad.SetAxis(step.Value, step.Amount / 100.0);
                }
                break;

            case MacroActionType.OpenUrl:
                _system?.OpenUrl(step.Value);
                break;

            case MacroActionType.LaunchApp:
                _system?.LaunchApp(step.Value);
                break;

            case MacroActionType.Shell:
                _system?.RunShell(step.Value);
                break;

            case MacroActionType.Webhook:
                if (_system != null && WebhookRequest.TryParse(step.Value, out var webhook, out _))
                {
                    await GuardAsync("Webhook", () => _system.SendWebhookAsync(webhook, token), token);
                }
                break;

            case MacroActionType.Obs:
                if (_system != null && ObsRequest.TryParse(step.Value, out var obs, out _))
                {
                    await GuardAsync("OBS", () => _system.SendObsAsync(obs, token), token);
                }
                break;

            case MacroActionType.TypeText:
                await TypeTextAsync(step.Value, token);
                break;

            case MacroActionType.Rumble:
                if (_rumble != null && MacroDefinition.TryParseRumble(step.Value, out double left, out double right))
                {
                    s.Rumbling = true;
                    _rumble.Rumble(left, right);
                    if (step.Amount > 0)
                    {
                        await Task.Delay(step.Amount, token);
                        _rumble.Rumble(0, 0);
                        s.Rumbling = false;
                    }
                }
                break;
        }

        return false;
    }

    // A network call that fails is reported and the macro goes on; cancelling the macro still cancels the call
    private async Task GuardAsync(string what, Func<Task> action, CancellationToken token)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ActionFailed?.Invoke($"{what} failed: {ex.Message}");
        }
    }

    private async Task TypeTextAsync(string text, CancellationToken token)
    {
        foreach (char c in text)
        {
            if (!KeyMap.TryGetTypedKey(c, out ushort vk, out bool shift)) continue;

            if (shift) _backend.KeyDown(0xA0);
            _backend.KeyDown(vk);
            try
            {
                await Task.Delay(TypeKeyMs, token);
            }
            finally
            {
                _backend.KeyUp(vk);
                if (shift) _backend.KeyUp(0xA0);
            }
        }
    }

    // Spread the movement over a few 1ms ticks so large moves are not a single teleport
    private async Task MoveMouseAsync(MacroStep step, CancellationToken token)
    {
        (int dx, int dy) = step.Value.ToLowerInvariant() switch
        {
            "up" => (0, -1),
            "down" => (0, 1),
            "left" => (-1, 0),
            "right" => (1, 0),
            _ => (0, 0)
        };
        if ((dx == 0 && dy == 0) || step.Amount <= 0) return;

        int remaining = step.Amount;
        int ticks = Math.Min(remaining, 10);
        for (int i = 0; i < ticks; i++)
        {
            int chunk = remaining / (ticks - i);
            remaining -= chunk;
            _backend.MouseMove(dx * chunk, dy * chunk);
            if (i < ticks - 1) await Task.Delay(MouseMoveStepMs, token);
        }
    }

    public void Dispose() => CancelAll();
}
