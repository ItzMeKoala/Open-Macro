using ReWASD.Core.Models;

namespace ReWASD.Engine.Remapper;

/// <summary>
/// Watches the pad's buttons for combos. A chord (several buttons pressed together) is active while they stay held, and the
/// buttons are taken away from the ordinary mappings; a button that belongs to a chord waits a moment before acting alone, so
/// that pressing A then B almost together is one chord and not "A, then B". A sequence (buttons pressed in order) is active for a
/// short pulse when the last one is pressed.
/// </summary>
public sealed class ComboTracker
{
    private sealed class ChordState
    {
        public bool Active;
        public bool Rejected;   // the buttons came down too far apart: not a chord until they are all let go
    }

    private sealed class SequenceState
    {
        public int Index;
        public long LastAt;
        public long PulseUntil;
    }

    private readonly Dictionary<ControllerButton, long> _down = new();
    private readonly HashSet<ControllerButton> _previous = new();
    private readonly HashSet<ControllerButton> _latched = new();   // chord buttons still held after the chord ended: not passed on until let go
    private readonly Dictionary<Guid, ChordState> _chords = new();
    private readonly Dictionary<Guid, SequenceState> _sequences = new();

    public void Reset()
    {
        _down.Clear();
        _previous.Clear();
        _latched.Clear();
        _chords.Clear();
        _sequences.Clear();
    }

    /// <summary>
    /// Takes the buttons held this frame and returns the ones the ordinary mappings should see. Combos that are firing are added
    /// to <paramref name="active"/>.
    /// </summary>
    public HashSet<ControllerButton> Process(HashSet<ControllerButton> pressed, long now, IReadOnlyList<ComboMapping> combos,
        int chordWindowMs, int pulseMs, HashSet<ComboMapping> active)
    {
        if (combos.Count == 0)
        {
            if (_down.Count > 0 || _chords.Count > 0 || _sequences.Count > 0) Reset();
            return pressed;
        }

        // Which buttons just went down, and since when each has been held
        var pressedNow = new List<ControllerButton>();
        foreach (var b in pressed)
        {
            if (!_down.ContainsKey(b)) { _down[b] = now; pressedNow.Add(b); }
        }
        foreach (var b in _down.Keys.Where(b => !pressed.Contains(b)).ToList()) _down.Remove(b);
        _latched.RemoveWhere(b => !pressed.Contains(b));
        _previous.Clear();
        foreach (var b in pressed) _previous.Add(b);

        var consumed = new HashSet<ControllerButton>(_latched);
        var chords = combos.Where(c => c.Kind == ComboKind.Chord && c.Buttons.Count >= 2).ToList();
        var known = new HashSet<Guid>(combos.Select(c => c.Id));
        foreach (var gone in _chords.Keys.Where(k => !known.Contains(k)).ToList()) _chords.Remove(gone);
        foreach (var gone in _sequences.Keys.Where(k => !known.Contains(k)).ToList()) _sequences.Remove(gone);

        // ---- chords
        foreach (var chord in chords)
        {
            if (!_chords.TryGetValue(chord.Id, out var state)) _chords[chord.Id] = state = new ChordState();

            bool allDown = chord.Buttons.All(_down.ContainsKey);
            if (!allDown)
            {
                if (state.Active)
                {
                    // One button let go: the chord is over, and any still held stay silent until released too
                    foreach (var b in chord.Buttons.Where(_down.ContainsKey)) _latched.Add(b);
                }
                state.Active = false;
                state.Rejected = false;
                continue;
            }

            if (!state.Active && !state.Rejected)
            {
                long first = chord.Buttons.Min(b => _down[b]);
                long last = chord.Buttons.Max(b => _down[b]);
                if (last - first <= chordWindowMs) state.Active = true;
                else state.Rejected = true;
            }

            if (state.Active)
            {
                active.Add(chord);
                foreach (var b in chord.Buttons) consumed.Add(b);
            }
        }

        consumed.UnionWith(_latched);

        // ---- sequences: every new press moves each sequence along
        foreach (var seq in combos.Where(c => c.Kind == ComboKind.Sequence && c.Buttons.Count >= 2))
        {
            if (!_sequences.TryGetValue(seq.Id, out var state)) _sequences[seq.Id] = state = new SequenceState();

            foreach (var button in pressedNow)
            {
                if (state.Index > 0 && now - state.LastAt > seq.SequenceGapMs) state.Index = 0;

                if (button == seq.Buttons[state.Index]) state.Index++;
                else state.Index = button == seq.Buttons[0] ? 1 : 0;
                state.LastAt = now;

                if (state.Index >= seq.Buttons.Count)
                {
                    state.Index = 0;
                    state.PulseUntil = now + Math.Max(pulseMs, 20);
                }
            }

            if (now < state.PulseUntil) active.Add(seq);
        }

        // ---- a button that could still turn into part of a chord waits for the window to pass
        var visible = new HashSet<ControllerButton>();
        foreach (var b in pressed)
        {
            if (consumed.Contains(b)) continue;
            if (IsWaiting(b, chords, now, chordWindowMs)) continue;
            visible.Add(b);
        }
        return visible;
    }

    // Waiting means: it went down less than a window ago, and some chord containing it could still be completed in time
    private bool IsWaiting(ControllerButton button, List<ComboMapping> chords, long now, int windowMs)
    {
        long since = _down[button];
        if (now - since >= windowMs) return false;

        foreach (var chord in chords)
        {
            if (!chord.Buttons.Contains(button)) continue;
            if (_chords.TryGetValue(chord.Id, out var state) && (state.Active || state.Rejected)) continue;

            // Every member already held must have come down within the window of this one; otherwise the chord can no longer form
            bool feasible = chord.Buttons.Where(_down.ContainsKey).All(b => Math.Abs(_down[b] - since) <= windowMs);
            if (feasible) return true;
        }
        return false;
    }
}
