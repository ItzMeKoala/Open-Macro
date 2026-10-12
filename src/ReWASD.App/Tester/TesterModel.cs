using ReWASD.Core.Models;
using ReWASD.Engine.Input;

namespace ReWASD.UI.Tester;

/// <summary>What the tester knows about one button.</summary>
public sealed record ButtonSnapshot(ControllerButton Button, bool IsDown, long Presses, double HeldMs, double LastHoldMs, double ShortestHoldMs, double LongestHoldMs, int Bounces);

/// <summary>What the tester knows about one stick.</summary>
public sealed record StickSnapshot(
    double X, double Y, double Magnitude, double AngleDegrees,
    double PeakMagnitude, double RestX, double RestY, double RestSamples, double WorstRestDrift,
    double[] Extents, double Coverage, double MinRadius, double MaxRadius, double MeanRadius, double Roundness, double CircularityError,
    (double X, double Y)[] Trail,
    int StepsX, int StepsY)
{
    /// <summary>True once enough of the edge has been covered for the roundness figure to mean something.</summary>
    public bool HasCircle => Coverage >= TesterModel.EnoughCoverage;
}

/// <summary>What the tester knows about one trigger.</summary>
public sealed record TriggerSnapshot(double Value, double Peak, double FirstMovement, double HeldMs, long Presses, int Steps, int[] Travel);

/// <summary>How often the controller reported something new, and how evenly.</summary>
public sealed record RateSnapshot(double ChangesPerSecond, double PeakChangesPerSecond, double AverageIntervalMs, double WorstIntervalMs, long TotalChanges, double JitterMs, double SteadyShare, long[] Histogram,
    double MedianIntervalMs = 0);

public enum ReactionState
{
    Idle,
    Waiting,
    Go,
    Result,
    TooEarly
}

/// <summary>The reaction-time game: wait for the signal, press any button, see how long it took.</summary>
public sealed record ReactionSnapshot(ReactionState State, double LastMs, double BestMs, double AverageMs, int Attempts);

/// <summary>Everything the tester shows, copied under one lock so a drawing never sees half an update.</summary>
public sealed record TesterSnapshot(
    bool Connected, double ElapsedMs,
    StickSnapshot Left, StickSnapshot Right, TriggerSnapshot LeftTrigger, TriggerSnapshot RightTrigger,
    IReadOnlyList<ButtonSnapshot> Buttons, RateSnapshot Rate,
    long LogVersion, IReadOnlyList<string> Log,
    double[][] Scope, ReactionSnapshot Reaction);

/// <summary>
/// The numbers behind the controller tester: live values, press counts and hold times, switch bounce, the shape a stick makes when
/// rolled around its edge (the circularity test), rest drift, trigger travel, how often the pad reports, and a log. Feed it every
/// reading with the time; read it back as a snapshot. Pure arithmetic, so it can be checked without a controller.
/// </summary>
public sealed class TesterModel
{
    public const int AngleBins = 72;                 // one every 5 degrees
    public const int TrailLength = 90;
    public const int ScopeSamples = 600;             // 6 seconds at one sample per 10 ms
    public const double ScopeStepMs = 10;
    public const double EnoughCoverage = 0.75;

    /// <summary>A direction counts as reached when the stick was pushed at least this far that way (not just passing through).</summary>
    public const double EdgeReach = 0.7;

    /// <summary>A stick has to sit still this long before its position counts as where it rests.</summary>
    public const double SettleMs = 200;

    /// <summary>Moving further than this from where it settled means the stick is being moved, not resting.</summary>
    public const double SettleMove = 0.03;
    public const double BounceMs = 30;               // a release and press closer than this is switch chatter, not a person
    public const double RestLimit = 0.2;             // a stick this close to centre is "resting"
    public const int MaxLog = 200;
    public const int TravelBins = 20;                // trigger travel, in twentieths of a pull
    public const int HistogramBins = 42;             // gaps between reports: 0 to 40 ms in 1 ms steps, then everything longer
    private const int MaxDistinct = 70000;

    /// <summary>The buttons the tester lists: the digital ones (sticks and triggers have their own panels).</summary>
    public static readonly ControllerButton[] ListedButtons =
    {
        ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y,
        ControllerButton.LeftBumper, ControllerButton.RightBumper,
        ControllerButton.DpadUp, ControllerButton.DpadDown, ControllerButton.DpadLeft, ControllerButton.DpadRight,
        ControllerButton.LeftStickClick, ControllerButton.RightStickClick,
        ControllerButton.View, ControllerButton.Menu, ControllerButton.Guide
    };

    // Names for the log and the lists, in controller words rather than enum words
    public static string NameOf(ControllerButton button) => button switch
    {
        ControllerButton.LeftBumper => "Left bumper",
        ControllerButton.RightBumper => "Right bumper",
        ControllerButton.DpadUp => "D-pad up",
        ControllerButton.DpadDown => "D-pad down",
        ControllerButton.DpadLeft => "D-pad left",
        ControllerButton.DpadRight => "D-pad right",
        ControllerButton.LeftStickClick => "Left stick click",
        ControllerButton.RightStickClick => "Right stick click",
        ControllerButton.View => "View / Back",
        ControllerButton.Menu => "Menu / Start",
        ControllerButton.Guide => "Guide / Home",
        _ => button.ToString()
    };

    private sealed class ButtonStat
    {
        public bool Down;
        public double DownAt;
        public double ReleasedAt = double.NegativeInfinity;
        public long Presses;
        public double LastHold;
        public double Shortest = double.PositiveInfinity;
        public double Longest;
        public int Bounces;
    }

    private sealed class StickStat
    {
        public double X, Y, Peak;
        public double RestSumX, RestSumY, RestCount, WorstRest;
        public double AnchorX = double.NaN, AnchorY, StillSince;
        public readonly double[] Extents = new double[AngleBins];
        public readonly Queue<(double X, double Y)> Trail = new();
        public readonly HashSet<double> StepsX = new(), StepsY = new();
    }

    private sealed class TriggerStat
    {
        public double Value, Peak;
        public double FirstMovement = double.NaN;
        public bool Down;
        public double DownAt;
        public double Held;
        public long Presses;
        public readonly HashSet<double> Steps = new();
        public readonly int[] Travel = new int[TravelBins];
    }

    private readonly object _gate = new();
    private readonly Func<DateTime> _wallClock;
    private readonly Dictionary<ControllerButton, ButtonStat> _buttons = new();
    private readonly StickStat _left = new(), _right = new();
    private readonly TriggerStat _lt = new(), _rt = new();
    private readonly List<string> _log = new();
    private readonly double[][] _scope = Enumerable.Range(0, 6).Select(_ => new double[ScopeSamples]).ToArray();
    private int _scopeFilled;
    private double _lastScopeAt = double.NegativeInfinity;

    private bool _connected;
    private double _firstSeenAt = double.NaN;
    private double _now;
    private PhysicalState? _previous;
    private readonly Queue<double> _recentChanges = new();
    private double _lastChangeAt = double.NaN;
    private readonly Queue<double> _recentIntervals = new();
    private long _totalChanges;
    private readonly long[] _intervalBins = new long[HistogramBins];
    private double _peakRate;
    private long _logVersion;

    private ReactionState _reaction = ReactionState.Idle;
    private double _goAt, _goShownAt, _lastReaction;
    private readonly List<double> _reactions = new();

    public TesterModel(Func<DateTime>? wallClock = null)
    {
        _wallClock = wallClock ?? (() => DateTime.Now);
        foreach (var b in ListedButtons) _buttons[b] = new ButtonStat();
    }

    /// <summary>Forgets everything counted and measured so far (the connection state stays).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            foreach (var b in ListedButtons) _buttons[b] = new ButtonStat();
            ResetStick(_left);
            ResetStick(_right);
            ResetTrigger(_lt);
            ResetTrigger(_rt);
            _log.Clear();
            _logVersion++;
            _scopeFilled = 0;
            _lastScopeAt = double.NegativeInfinity;
            _recentChanges.Clear();
            _recentIntervals.Clear();
            Array.Clear(_intervalBins);
            _lastChangeAt = double.NaN;
            _totalChanges = 0;
            _peakRate = 0;
            _firstSeenAt = _connected ? _now : double.NaN;
            _previous = null;
            _reaction = ReactionState.Idle;
            _reactions.Clear();
        }
    }

    /// <summary>Forgets just one stick's circle, drift and trail (the stick's own reset button).</summary>
    public void ResetStickMeasurements(bool left)
    {
        lock (_gate) ResetStick(left ? _left : _right);
    }

    private static void ResetStick(StickStat s)
    {
        s.Peak = 0;
        s.RestSumX = s.RestSumY = s.RestCount = s.WorstRest = 0;
        s.AnchorX = double.NaN;
        Array.Clear(s.Extents);
        s.Trail.Clear();
        s.StepsX.Clear();
        s.StepsY.Clear();
    }

    private static void ResetTrigger(TriggerStat t)
    {
        t.Peak = 0;
        t.FirstMovement = double.NaN;
        t.Presses = 0;
        t.Down = false;
        t.Held = 0;
        t.Steps.Clear();
        Array.Clear(t.Travel);
    }

    /// <summary>Takes one reading (null = no controller) at the given time in milliseconds.</summary>
    public void Feed(PhysicalState? state, double nowMs)
    {
        lock (_gate)
        {
            _now = nowMs;

            if (state == null)
            {
                if (_connected) Log("Controller disconnected");
                _connected = false;
                ReleaseEverything(nowMs);
                _previous = null;
                return;
            }

            if (!_connected)
            {
                _connected = true;
                _firstSeenAt = double.IsNaN(_firstSeenAt) ? nowMs : _firstSeenAt;
                Log("Controller connected");
            }

            CountChange(state, nowMs);
            UpdateReaction(state, nowMs);
            foreach (var b in ListedButtons) UpdateButton(b, state.Buttons.Contains(b), nowMs);
            UpdateStick(_left, state.LeftStickX, state.LeftStickY, nowMs);
            UpdateStick(_right, state.RightStickX, state.RightStickY, nowMs);
            UpdateTrigger(_lt, "Left trigger", state.LeftTrigger, nowMs);
            UpdateTrigger(_rt, "Right trigger", state.RightTrigger, nowMs);
            Sample(state, nowMs);
            _previous = state;
        }
    }

    // ---- how often the pad reports ----------------------------------------------------------------------------------

    // Only the sticks and triggers make a steady stream; buttons change when a finger does, so they say nothing about the pad's rate
    private static bool AxesDiffer(PhysicalState a, PhysicalState b) =>
        a.LeftStickX != b.LeftStickX || a.LeftStickY != b.LeftStickY || a.RightStickX != b.RightStickX || a.RightStickY != b.RightStickY
        || a.LeftTrigger != b.LeftTrigger || a.RightTrigger != b.RightTrigger;

    /// <summary>A pause longer than this means the person stopped moving, not that the pad stalled.</summary>
    public const double IdleGapMs = 120;

    private void CountChange(PhysicalState state, double now)
    {
        if (_previous == null || !AxesDiffer(_previous, state)) return;

        _totalChanges++;
        _recentChanges.Enqueue(now);
        if (!double.IsNaN(_lastChangeAt) && now >= _lastChangeAt && now - _lastChangeAt <= IdleGapMs)
        {
            double gap = now - _lastChangeAt;
            _intervalBins[Math.Min(HistogramBins - 1, (int)gap)]++;
            _recentIntervals.Enqueue(gap);
            if (_recentIntervals.Count > 200) _recentIntervals.Dequeue();
        }
        _lastChangeAt = now;

        while (_recentChanges.Count > 0 && now - _recentChanges.Peek() > 1000) _recentChanges.Dequeue();
        _peakRate = Math.Max(_peakRate, _recentChanges.Count);
    }

    // ---- the reaction-time game ----------------------------------------------------------------------------------------

    /// <summary>Starts a round: a random wait of one to four seconds, then the signal.</summary>
    public void StartReaction(double nowMs, Random? random = null)
    {
        lock (_gate)
        {
            _reaction = ReactionState.Waiting;
            _goAt = nowMs + (random ?? Random.Shared).Next(1200, 4000);
        }
    }

    public void CancelReaction()
    {
        lock (_gate) _reaction = ReactionState.Idle;
    }

    // A new press of any button, or a trigger pulled past halfway
    private bool NewPress(PhysicalState s) =>
        ListedButtons.Any(b => s.Buttons.Contains(b) && !_buttons[b].Down)
        || (s.LeftTrigger > 0.5 && _lt.Value <= 0.5) || (s.RightTrigger > 0.5 && _rt.Value <= 0.5);

    private void UpdateReaction(PhysicalState s, double now)
    {
        switch (_reaction)
        {
            case ReactionState.Waiting:
                if (NewPress(s))
                {
                    _reaction = ReactionState.TooEarly;
                    Log("Reaction test: pressed too early");
                }
                else if (now >= _goAt)
                {
                    _reaction = ReactionState.Go;
                    _goShownAt = now;
                }
                break;

            case ReactionState.Go:
                if (NewPress(s))
                {
                    _lastReaction = now - _goShownAt;
                    _reactions.Add(_lastReaction);
                    _reaction = ReactionState.Result;
                    Log($"Reaction test: {_lastReaction:0} ms");
                }
                break;
        }
    }

    // ---- buttons ------------------------------------------------------------------------------------------------------

    private void UpdateButton(ControllerButton button, bool down, double now)
    {
        var s = _buttons[button];
        if (down && !s.Down)
        {
            s.Down = true;
            s.DownAt = now;
            s.Presses++;
            bool bounce = now - s.ReleasedAt < BounceMs;
            if (bounce) s.Bounces++;
            Log($"{NameOf(button)} pressed" + (bounce ? $"  (bounce: only {now - s.ReleasedAt:0.#} ms after release)" : string.Empty));
        }
        else if (!down && s.Down)
        {
            s.Down = false;
            s.ReleasedAt = now;
            s.LastHold = now - s.DownAt;
            s.Shortest = Math.Min(s.Shortest, s.LastHold);
            s.Longest = Math.Max(s.Longest, s.LastHold);
            Log($"{NameOf(button)} released after {s.LastHold:0} ms");
        }
    }

    // ---- sticks -------------------------------------------------------------------------------------------------------

    private static void UpdateStick(StickStat s, double x, double y, double now)
    {
        s.X = x;
        s.Y = y;
        if (s.StepsX.Count < MaxDistinct) s.StepsX.Add(x);
        if (s.StepsY.Count < MaxDistinct) s.StepsY.Add(y);
        double mag = Math.Sqrt(x * x + y * y);
        s.Peak = Math.Max(s.Peak, mag);

        // Drift is where the stick sits when nobody is touching it: near the centre and still for a moment. Readings taken while the
        // stick passes through the middle on its way somewhere else are not drift.
        if (double.IsNaN(s.AnchorX) || Math.Abs(x - s.AnchorX) > SettleMove || Math.Abs(y - s.AnchorY) > SettleMove)
        {
            s.AnchorX = x;
            s.AnchorY = y;
            s.StillSince = now;
        }
        if (mag < RestLimit && now - s.StillSince >= SettleMs)
        {
            s.RestSumX += x;
            s.RestSumY += y;
            s.RestCount++;
            s.WorstRest = Math.Max(s.WorstRest, mag);
        }

        if (mag >= RestLimit)
        {
            double degrees = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (degrees < 0) degrees += 360;
            int bin = Math.Min(AngleBins - 1, (int)(degrees / (360.0 / AngleBins)));
            s.Extents[bin] = Math.Max(s.Extents[bin], mag);
        }

        var last = s.Trail.Count > 0 ? s.Trail.Last() : (X: double.NaN, Y: double.NaN);
        if (double.IsNaN(last.X) || Math.Abs(last.X - x) > 0.01 || Math.Abs(last.Y - y) > 0.01)
        {
            s.Trail.Enqueue((x, y));
            while (s.Trail.Count > TrailLength) s.Trail.Dequeue();
        }
    }

    private static StickSnapshot Snapshot(StickStat s)
    {
        double mag = Math.Sqrt(s.X * s.X + s.Y * s.Y);
        double degrees = Math.Atan2(s.Y, s.X) * 180.0 / Math.PI;
        if (degrees < 0) degrees += 360;

        // Only directions the stick was pushed all the way count towards the circle. The error is how far, on average, the edge it
        // reached is from a perfect circle of full reach (1.0): falling short and overshooting (the corners of a square gate) both count.
        var covered = s.Extents.Where(e => e >= EdgeReach).ToArray();
        double coverage = covered.Length / (double)AngleBins;
        double min = covered.Length > 0 ? covered.Min() : 0;
        double max = covered.Length > 0 ? covered.Max() : 0;
        double mean = covered.Length > 0 ? covered.Average() : 0;
        double error = covered.Length > 0 ? Math.Sqrt(covered.Average(e => (e - 1) * (e - 1))) : 0;
        double roundness = covered.Length > 0 ? Math.Max(0, 1 - error) : 0;

        return new StickSnapshot(
            s.X, s.Y, mag, mag < 1e-9 ? 0 : degrees,
            s.Peak,
            s.RestCount > 0 ? s.RestSumX / s.RestCount : 0, s.RestCount > 0 ? s.RestSumY / s.RestCount : 0, s.RestCount, s.WorstRest,
            (double[])s.Extents.Clone(), coverage, min, max, mean, roundness, error,
            s.Trail.ToArray(), s.StepsX.Count, s.StepsY.Count);
    }

    // ---- triggers (pressure) ---------------------------------------------------------------------------------------------

    private void UpdateTrigger(TriggerStat t, string name, double value, double now)
    {
        if (value != t.Value)
        {
            // Every distinct reading is a step the trigger can report; counting where they fall shows gaps in its travel
            if (t.Steps.Count < MaxDistinct) t.Steps.Add(value);
            t.Travel[Math.Clamp((int)(value * TravelBins), 0, TravelBins - 1)]++;
        }
        t.Value = value;
        t.Peak = Math.Max(t.Peak, value);
        if (value > 0.004 && (double.IsNaN(t.FirstMovement) || value < t.FirstMovement)) t.FirstMovement = value;

        bool down = value > 0.05;
        if (down && !t.Down)
        {
            t.Down = true;
            t.DownAt = now;
            t.Presses++;
        }
        else if (!down && t.Down)
        {
            t.Down = false;
            Log($"{name} released after {now - t.DownAt:0} ms");
        }
        t.Held = t.Down ? now - t.DownAt : 0;
    }

    // ---- the scrolling graph ---------------------------------------------------------------------------------------------

    private void Sample(PhysicalState s, double now)
    {
        if (now - _lastScopeAt < ScopeStepMs) return;
        _lastScopeAt = now;

        if (_scopeFilled == ScopeSamples)
        {
            foreach (var channel in _scope) Array.Copy(channel, 1, channel, 0, ScopeSamples - 1);
            _scopeFilled--;
        }
        int i = _scopeFilled++;
        _scope[0][i] = s.LeftStickX;
        _scope[1][i] = s.LeftStickY;
        _scope[2][i] = s.RightStickX;
        _scope[3][i] = s.RightStickY;
        _scope[4][i] = s.LeftTrigger;
        _scope[5][i] = s.RightTrigger;
    }

    // ---- the log and the rest ----------------------------------------------------------------------------------------------

    private void Log(string text)
    {
        _log.Insert(0, $"{_wallClock():HH:mm:ss.fff}  {text}");
        if (_log.Count > MaxLog) _log.RemoveAt(_log.Count - 1);
        _logVersion++;
    }

    private void ReleaseEverything(double now)
    {
        foreach (var b in ListedButtons) UpdateButton(b, false, now);
        UpdateTrigger(_lt, "Left trigger", 0, now);
        UpdateTrigger(_rt, "Right trigger", 0, now);
        _left.X = _left.Y = _right.X = _right.Y = 0;
    }

    public TesterSnapshot Snapshot()
    {
        lock (_gate)
        {
            double elapsed = _connected && !double.IsNaN(_firstSeenAt) ? _now - _firstSeenAt : 0;

            var buttons = ListedButtons.Select(b =>
            {
                var s = _buttons[b];
                return new ButtonSnapshot(b, s.Down, s.Presses, s.Down ? _now - s.DownAt : 0, s.LastHold,
                    double.IsInfinity(s.Shortest) ? 0 : s.Shortest, s.Longest, s.Bounces);
            }).ToList();

            double perSecond = _recentChanges.Count(t => _now - t <= 1000);
            double average = _recentIntervals.Count > 0 ? _recentIntervals.Average() : 0;
            double worst = _recentIntervals.Count > 0 ? _recentIntervals.Max() : 0;

            // How even the reports are: the spread of the gaps, and the share of gaps within a quarter of the usual one
            double jitter = _recentIntervals.Count > 1 ? Math.Sqrt(_recentIntervals.Average(g => (g - average) * (g - average))) : 0;
            double steady = _recentIntervals.Count > 0 && average > 0 ? _recentIntervals.Count(g => Math.Abs(g - average) <= average * 0.25) / (double)_recentIntervals.Count : 0;
            double median = 0;
            if (_recentIntervals.Count > 0)
            {
                var sorted = _recentIntervals.OrderBy(g => g).ToArray();
                median = sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
            }

            var scope = _scope.Select(c => c.AsSpan(0, _scopeFilled).ToArray()).ToArray();

            return new TesterSnapshot(
                _connected, elapsed,
                Snapshot(_left), Snapshot(_right),
                Snapshot(_lt), Snapshot(_rt),
                buttons, new RateSnapshot(perSecond, _peakRate, average, worst, _totalChanges, jitter, steady, (long[])_intervalBins.Clone(), median),
                _logVersion, _log.ToList(), scope,
                new ReactionSnapshot(_reaction, _lastReaction, _reactions.Count > 0 ? _reactions.Min() : 0, _reactions.Count > 0 ? _reactions.Average() : 0, _reactions.Count));
        }
    }

    private static TriggerSnapshot Snapshot(TriggerStat t) =>
        new(t.Value, t.Peak, double.IsNaN(t.FirstMovement) ? 0 : t.FirstMovement, t.Held, t.Presses, t.Steps.Count, (int[])t.Travel.Clone());
}
