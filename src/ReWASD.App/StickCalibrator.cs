namespace ReWASD.UI;

public enum CalibrationKind
{
    /// <summary>Measure how far the stick drifts while untouched, to set the inner deadzone.</summary>
    Rest,

    /// <summary>Measure how far the stick really goes when pushed to its edge, to set the outer deadzone.</summary>
    Range
}

/// <summary>
/// Measures one stick: untouched for a moment (its drift), or pushed around its edge (its reach). The readings turn into the
/// deadzone values to use, so a worn stick that never rests at zero or never quite reaches full still behaves.
/// </summary>
public sealed class StickCalibrator
{
    public const long RestMs = 1500;
    public const long RangeMs = 3500;

    private long _startedAt;
    private double _largest;

    public bool IsRunning { get; private set; }
    public CalibrationKind Kind { get; private set; }

    /// <summary>0 to 1, how far through the measuring is.</summary>
    public double Progress { get; private set; }

    public void Start(CalibrationKind kind, long now)
    {
        Kind = kind;
        IsRunning = true;
        _startedAt = now;
        _largest = 0;
        Progress = 0;
    }

    public void Cancel() => IsRunning = false;

    /// <summary>
    /// Feed the stick position (-1 to 1 on each axis, or null when no controller is connected) many times a second. Returns the
    /// finished value once the time is up: the inner deadzone for <see cref="CalibrationKind.Rest"/>, the outer one for
    /// <see cref="CalibrationKind.Range"/>. Returns null while measuring or when there is nothing to measure.
    /// </summary>
    public double? Feed(double? x, double? y, long now)
    {
        if (!IsRunning) return null;

        if (x is { } px && y is { } py) _largest = Math.Max(_largest, Math.Sqrt(px * px + py * py));

        long length = Kind == CalibrationKind.Rest ? RestMs : RangeMs;
        Progress = Math.Clamp((now - _startedAt) / (double)length, 0, 1);
        if (now - _startedAt < length) return null;

        IsRunning = false;
        return Kind == CalibrationKind.Rest ? RestDeadzone(_largest) : RangeLimit(_largest);
    }

    /// <summary>A little above the worst drift seen, but never so large that the stick feels dead.</summary>
    public static double RestDeadzone(double largestDrift) => Math.Round(Math.Clamp(largestDrift * 1.25 + 0.01, 0.02, 0.45), 2);

    /// <summary>A little under the furthest the stick went, so full deflection is reachable; at least 0.6 and at most 1.</summary>
    public static double RangeLimit(double furthest) => Math.Round(Math.Clamp(furthest * 0.97, 0.6, 1.0), 2);
}
