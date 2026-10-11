using ReWASD.Core.Models;

namespace ReWASD.Engine.Input;

/// <summary>
/// Starts macros on the press edge of their trigger and cancels WhileHeld macros on release.
/// Each macro runs on its own task so the polling loop never blocks on a Delay step.
/// </summary>
public sealed class MacroRunner : IDisposable
{
    private const int TapHoldMs = 15;

    private readonly IInputBackend _backend;
    private readonly object _gate = new();
    private readonly Dictionary<MacroDefinition, CancellationTokenSource> _running = new();
    private HashSet<MacroDefinition> _previouslyHeld = new();

    public MacroRunner(IInputBackend backend)
    {
        _backend = backend;
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
            if (!held.Contains(macro) && macro.RepeatMode == MacroRepeatMode.WhileHeld) Cancel(macro);
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
        var heldKeys = new HashSet<ushort>();
        var heldButtons = new HashSet<MouseButtonKind>();

        try
        {
            do
            {
                foreach (var step in macro.Steps)
                {
                    token.ThrowIfCancellationRequested();
                    await ExecuteAsync(step, heldKeys, heldButtons, token);
                }

                // Yield between loops so a macro with no Delay step can't spin a core
                if (macro.RepeatMode == MacroRepeatMode.WhileHeld) await Task.Delay(1, token);
            }
            while (macro.RepeatMode == MacroRepeatMode.WhileHeld);
        }
        catch (OperationCanceledException)
        {
            // Normal path when the trigger is released
        }
        finally
        {
            foreach (var vk in heldKeys) _backend.KeyUp(vk);
            foreach (var b in heldButtons) _backend.MouseUp(b);

            // Drop our registration unless a newer run already replaced it
            lock (_gate)
            {
                if (_running.TryGetValue(macro, out var current) && ReferenceEquals(current, cts)) _running.Remove(macro);
            }
            cts.Dispose();
        }
    }

    private async Task ExecuteAsync(MacroStep step, HashSet<ushort> heldKeys, HashSet<MouseButtonKind> heldButtons, CancellationToken token)
    {
        switch (step.Action)
        {
            case MacroActionType.Delay:
                if (step.Amount > 0) await Task.Delay(step.Amount, token);
                break;

            case MacroActionType.Wheel:
                if (step.Amount != 0) _backend.MouseWheel(step.Amount * KeyMap.WheelNotch);
                break;

            case MacroActionType.KeyDown:
                if (KeyMap.TryGetVirtualKey(step.Value, out var down) && heldKeys.Add(down)) _backend.KeyDown(down);
                break;

            case MacroActionType.KeyUp:
                if (KeyMap.TryGetVirtualKey(step.Value, out var up) && heldKeys.Remove(up)) _backend.KeyUp(up);
                break;

            case MacroActionType.KeyTap:
                if (KeyMap.TryGetVirtualKey(step.Value, out var tap))
                {
                    heldKeys.Add(tap);
                    _backend.KeyDown(tap);
                    await Task.Delay(TapHoldMs, token);
                    _backend.KeyUp(tap);
                    heldKeys.Remove(tap);
                }
                break;

            case MacroActionType.MouseDown:
                if (KeyMap.TryResolveMouse(step.Value, out var b1, out _) && b1 is { } down1 && heldButtons.Add(down1)) _backend.MouseDown(down1);
                break;

            case MacroActionType.MouseUp:
                if (KeyMap.TryResolveMouse(step.Value, out var b2, out _) && b2 is { } up2 && heldButtons.Remove(up2)) _backend.MouseUp(up2);
                break;

            case MacroActionType.MouseClick:
                if (KeyMap.TryResolveMouse(step.Value, out var b3, out _) && b3 is { } click)
                {
                    heldButtons.Add(click);
                    _backend.MouseDown(click);
                    await Task.Delay(TapHoldMs, token);
                    _backend.MouseUp(click);
                    heldButtons.Remove(click);
                }
                break;
        }
    }

    public void Dispose() => CancelAll();
}
