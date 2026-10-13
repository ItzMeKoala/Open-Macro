using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ReWASD.Core.Models;

namespace ReWASD.UI.Tester;

/// <summary>One stick's panel of the tester, as text and as the data a plot draws.</summary>
public sealed partial class TesterStickViewModel : ObservableObject
{
    public string Title { get; }

    public TesterStickViewModel(string title) => Title = title;

    /// <summary>The newest measurements; the plot redraws when this changes.</summary>
    [ObservableProperty] private StickSnapshot? _snapshot;
    [ObservableProperty] private double _deadzoneMin;
    [ObservableProperty] private double _deadzoneMax = 1;
    [ObservableProperty] private string _positionText = "x  0.000    y  0.000";
    [ObservableProperty] private string _magnitudeText = "0.000";
    [ObservableProperty] private string _angleText = "—";
    [ObservableProperty] private string _restText = "Let go of the stick to measure its drift";
    [ObservableProperty] private string _circleText = "Roll the stick slowly around its edge";
    [ObservableProperty] private string _coverageText = "0%";
    [ObservableProperty] private string _radiusText = "—";
    [ObservableProperty] private string _roundnessText = "—";
    [ObservableProperty] private string _peakText = "0.000";
    [ObservableProperty] private string _stepsText = "—";
    [ObservableProperty] private string _xText = "0.000";
    [ObservableProperty] private string _yText = "0.000";
    [ObservableProperty] private string _driftText = "—";

    public void Update(StickSnapshot s, StickSettings? settings)
    {
        Snapshot = s;
        if (settings != null)
        {
            DeadzoneMin = settings.DeadzoneMin;
            DeadzoneMax = settings.DeadzoneMax;
        }

        PositionText = $"x {Signed(s.X)}    y {Signed(s.Y)}";
        XText = Signed(s.X);
        YText = Signed(s.Y);
        DriftText = s.RestSamples < 20 ? "—" : F(s.WorstRestDrift);
        MagnitudeText = F(s.Magnitude);
        AngleText = s.Magnitude < 0.02 ? "—" : $"{s.AngleDegrees:0}°";
        PeakText = F(s.PeakMagnitude);
        StepsText = s.StepsX < 3 && s.StepsY < 3 ? "—" : $"{s.StepsX} × {s.StepsY} steps{Depth(Math.Max(s.StepsX, s.StepsY))}";
        CoverageText = $"{s.Coverage:P0}";

        RestText = s.RestSamples < 20
            ? "Let go of the stick for a moment to measure its drift"
            : $"Rest position x {Signed(s.RestX)}  y {Signed(s.RestY)}   worst drift {F(s.WorstRestDrift)}";

        if (s.HasCircle)
        {
            RadiusText = $"min {F(s.MinRadius)}   mean {F(s.MeanRadius)}   max {F(s.MaxRadius)}";
            RoundnessText = (s.CircularityError * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
            CircleText = s.CircularityError <= 0.05 ? "Very round" : s.CircularityError <= 0.10 ? "Round" : s.CircularityError <= 0.18 ? "Slightly uneven" : "Uneven";
        }
        else
        {
            RadiusText = "—";
            RoundnessText = "—";
            CircleText = s.Coverage < 0.05 ? "Roll the stick slowly around its edge, all the way out" : $"Keep going: {s.Coverage:P0} of the circle so far";
        }
    }

    // 256 steps or fewer is an 8-bit axis; a finer one reports in far more steps
    private static string Depth(int steps) => steps <= 260 ? "  ·  8-bit" : steps >= 2000 ? "  ·  16-bit" : string.Empty;

    internal static string F(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    internal static string Signed(double v) => v.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);
}

/// <summary>One trigger's panel of the tester.</summary>
public sealed partial class TesterTriggerViewModel : ObservableObject
{
    public string Title { get; }

    public TesterTriggerViewModel(string title) => Title = title;

    [ObservableProperty] private double _value;
    [ObservableProperty] private string _percentText = "0%";
    [ObservableProperty] private string _rawText = "0 / 255";
    [ObservableProperty] private string _peakText = "peak 0%";
    [ObservableProperty] private double _peak;
    [ObservableProperty] private string _startText = "starts moving at —";
    [ObservableProperty] private string _heldText = "not pressed";
    [ObservableProperty] private string _pressesText = "0 pulls";
    [ObservableProperty] private string _stepsText = "—";

    /// <summary>How many readings fell in each twentieth of the pull; gaps show where the trigger skips.</summary>
    [ObservableProperty] private int[] _travel = new int[TesterModel.TravelBins];

    public void Update(TriggerSnapshot t)
    {
        Travel = t.Travel;
        StepsText = t.Steps < 3 ? "—" : $"{t.Steps} steps";
        Value = t.Value;
        PercentText = $"{t.Value:P0}";
        RawText = $"{(int)Math.Round(t.Value * 255)} / 255";
        PeakText = $"peak {t.Peak:P0}";
        Peak = t.Peak;
        StartText = t.FirstMovement > 0 ? $"starts moving at {t.FirstMovement:P1}" : "starts moving at —";
        HeldText = t.HeldMs > 0 ? $"held {t.HeldMs / 1000:0.00} s" : "not pressed";
        PressesText = t.Presses == 1 ? "1 pull" : $"{t.Presses} pulls";
    }
}

/// <summary>One row of the button list.</summary>
public sealed partial class TesterButtonRow : ObservableObject
{
    public ControllerButton Button { get; }
    public string Name { get; }

    /// <summary>The button sits on the left half of the pad (D-pad, left bumper, left stick click, View, Guide); the rest are on the right.</summary>
    public bool IsLeftSide { get; }

    public TesterButtonRow(ControllerButton button)
    {
        Button = button;
        Name = TesterModel.NameOf(button);
        IsLeftSide = button is ControllerButton.DpadUp or ControllerButton.DpadDown or ControllerButton.DpadLeft or ControllerButton.DpadRight
            or ControllerButton.LeftBumper or ControllerButton.LeftStickClick or ControllerButton.View or ControllerButton.Guide;
    }

    [ObservableProperty] private bool _isDown;
    [ObservableProperty] private string _pressesText = "0";
    [ObservableProperty] private string _holdText = "—";
    [ObservableProperty] private string _rangeText = "—";
    [ObservableProperty] private string _bounceText = "";
    [ObservableProperty] private bool _hasBounce;

    public void Update(ButtonSnapshot b)
    {
        IsDown = b.IsDown;
        PressesText = b.Presses.ToString(CultureInfo.InvariantCulture);
        HoldText = b.IsDown ? $"{b.HeldMs:0} ms" : b.Presses > 0 ? $"{b.LastHoldMs:0} ms" : "—";
        RangeText = b.Presses > 0 ? $"{b.ShortestHoldMs:0} – {b.LongestHoldMs:0} ms" : "—";
        HasBounce = b.Bounces > 0;
        BounceText = b.Bounces > 0 ? $"{b.Bounces} bounce{(b.Bounces == 1 ? "" : "s")}" : "";
    }
}

/// <summary>Everything the full-screen tester shows, refreshed from <see cref="TesterModel"/> about thirty times a second.</summary>
public sealed partial class TesterViewModel : ObservableObject
{
    public TesterModel Model { get; }

    public TesterStickViewModel Left { get; } = new("Left stick");
    public TesterStickViewModel Right { get; } = new("Right stick");
    public TesterTriggerViewModel LeftTrigger { get; } = new("Left trigger");
    public TesterTriggerViewModel RightTrigger { get; } = new("Right trigger");
    public ObservableCollection<TesterButtonRow> Buttons { get; } = new();

    /// <summary>The buttons on the left half of the pad (shown under the left stick) and on the right half (under the right stick); the rows are the ones in <see cref="Buttons"/>.</summary>
    public IReadOnlyList<TesterButtonRow> LeftButtons { get; }
    public IReadOnlyList<TesterButtonRow> RightButtons { get; }

    public TesterViewModel(TesterModel? model = null)
    {
        Model = model ?? new TesterModel();
        foreach (var b in TesterModel.ListedButtons) Buttons.Add(new TesterButtonRow(b));
        LeftButtons = Buttons.Where(b => b.IsLeftSide).ToList();
        RightButtons = Buttons.Where(b => !b.IsLeftSide).ToList();
    }

    [ObservableProperty] private bool _connected;
    [ObservableProperty] private string _controllerName = "No controller";
    [ObservableProperty] private string _sessionText = "00:00:00";
    [ObservableProperty] private string _rateText = "—";

    /// <summary>The report rate as a bare number for a big readout ("—" until a stick has moved), and whether there is one.</summary>
    [ObservableProperty] private string _rateNumberText = "—";
    [ObservableProperty] private bool _hasRate;
    [ObservableProperty] private string _intervalText = "—";
    [ObservableProperty] private string _peakRateText = "—";
    [ObservableProperty] private string _totalText = "0 changes";
    [ObservableProperty] private string _jitterText = "—";

    /// <summary>How many report gaps of each length were seen (1 ms bins, 0 to 40 ms and over).</summary>
    [ObservableProperty] private long[] _histogram = new long[TesterModel.HistogramBins];

    /// <summary>A short note after copying or saving the report.</summary>
    [ObservableProperty] private string _notice = string.Empty;

    /// <summary>The display is frozen so the numbers can be read; the test keeps recording underneath.</summary>
    [ObservableProperty] private bool _isPaused;

    // The reaction game
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReactionGo), nameof(ReactionWait), nameof(ReactionBad), nameof(ReactionDone))]
    private ReactionState _reactionState = ReactionState.Idle;

    public bool ReactionGo => ReactionState == ReactionState.Go;
    public bool ReactionWait => ReactionState == ReactionState.Waiting;
    public bool ReactionBad => ReactionState == ReactionState.TooEarly;
    public bool ReactionDone => ReactionState == ReactionState.Result;
    [ObservableProperty] private string _reactionText = "Click here to start";
    [ObservableProperty] private string _reactionDetail = "Then press any button when the box turns green";

    /// <summary>The graph's six lines: left x and y, right x and y, left and right trigger.</summary>
    [ObservableProperty] private double[][] _scope = Array.Empty<double[]>();

    /// <summary>Copies the model's newest numbers into the texts the screen shows.</summary>
    public void Refresh(TesterSnapshot snap, string controllerName, StickSettings? leftSettings, StickSettings? rightSettings)
    {
        Connected = snap.Connected;
        ControllerName = snap.Connected ? controllerName : "No controller";

        var span = TimeSpan.FromMilliseconds(Math.Max(0, snap.ElapsedMs));
        SessionText = $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";

        var rate = snap.Rate;
        RateText = rate.TotalChanges < 2 ? "Move a stick to measure" : $"{rate.ChangesPerSecond:0} changes/s";
        HasRate = rate.TotalChanges >= 2;
        RateNumberText = HasRate ? $"{rate.ChangesPerSecond:0}" : "—";
        IntervalText = rate.TotalChanges < 3 ? "—" : $"avg {rate.AverageIntervalMs:0.0} ms   worst {rate.WorstIntervalMs:0.0} ms";
        PeakRateText = rate.TotalChanges < 2 ? "—" : $"peak {rate.PeakChangesPerSecond:0}/s";
        TotalText = $"{rate.TotalChanges:N0} changes";
        JitterText = rate.TotalChanges < 6 ? "—" : $"usual gap {rate.MedianIntervalMs:0.0} ms   jitter {rate.JitterMs:0.0} ms";

        var r = snap.Reaction;
        ReactionState = r.State;
        ReactionText = r.State switch
        {
            ReactionState.Waiting => "Wait for it…",
            ReactionState.Go => "NOW!",
            ReactionState.Result => $"{r.LastMs:0} ms",
            ReactionState.TooEarly => "Too early!",
            _ => "Click here to start"
        };
        ReactionDetail = r.Attempts == 0
            ? "Then press any button when the box turns green"
            : $"best {r.BestMs:0} ms   average {r.AverageMs:0} ms   {r.Attempts} {(r.Attempts == 1 ? "try" : "tries")}";

        Histogram = snap.Rate.Histogram;

        Left.Update(snap.Left, leftSettings);
        Right.Update(snap.Right, rightSettings);
        LeftTrigger.Update(snap.LeftTrigger);
        RightTrigger.Update(snap.RightTrigger);
        for (int i = 0; i < Buttons.Count && i < snap.Buttons.Count; i++) Buttons[i].Update(snap.Buttons[i]);
        Scope = snap.Scope;
    }
}
