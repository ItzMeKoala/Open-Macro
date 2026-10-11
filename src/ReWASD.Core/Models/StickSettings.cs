namespace ReWASD.Core.Models;

public class StickSettings
{
    public double DeadzoneMin { get; set; } = 0.05; // 5% inner deadzone
    public double DeadzoneMax { get; set; } = 0.95; // 95% outer deadzone
    public ResponseCurveType CurveType { get; set; } = ResponseCurveType.Linear;
    public double Sensitivity { get; set; } = 1.0;
    public bool InvertX { get; set; } = false;
    public bool InvertY { get; set; } = false;

    // Apply deadzone and curve to raw input coordinate (-1.0 to 1.0)
    public double TransformAxis(double input)
    {
        if (double.IsNaN(input)) return 0.0; // Math.Sign(NaN) throws

        return Math.Sign(input) * Shape(Math.Abs(input));
    }

    /// <summary>
    /// Radial variant for a whole stick: the deadzone and curve act on the vector's length, so diagonals
    /// are not squared off and direction is preserved. Prefer this over two <see cref="TransformAxis"/> calls.
    /// </summary>
    public (double X, double Y) TransformStick(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) return (0.0, 0.0);

        double magnitude = Math.Sqrt(x * x + y * y);
        double shaped = Shape(magnitude);
        if (shaped == 0.0) return (0.0, 0.0);

        double scale = shaped / magnitude;
        return (x * scale, y * scale);
    }

    // Maps a non-negative magnitude through deadzone, curve and sensitivity to 0..1
    private double Shape(double abs)
    {
        if (abs <= DeadzoneMin) return 0.0;

        // A degenerate range (Max <= Min) means everything past the deadzone is full deflection
        double range = DeadzoneMax - DeadzoneMin;
        double normalized = range <= 0 ? 1.0 : Math.Min((abs - DeadzoneMin) / range, 1.0);

        // Apply curve
        double curved = CurveType switch
        {
            ResponseCurveType.Smooth => Math.Pow(normalized, 1.5),
            ResponseCurveType.Aggressive => Math.Pow(normalized, 0.7),
            ResponseCurveType.Instant => normalized > 0.1 ? 1.0 : normalized * 10,
            _ => normalized
        };

        curved *= Sensitivity;
        return Math.Clamp(curved, 0.0, 1.0);
    }
}
