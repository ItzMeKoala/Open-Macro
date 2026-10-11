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
        double sign = Math.Sign(input);
        double abs = Math.Abs(input);

        if (abs <= DeadzoneMin) return 0.0;
        if (abs >= DeadzoneMax) return sign * 1.0;

        // Normalize between DeadzoneMin and DeadzoneMax
        double normalized = (abs - DeadzoneMin) / (DeadzoneMax - DeadzoneMin);

        // Apply curve
        double curved = CurveType switch
        {
            ResponseCurveType.Smooth => Math.Pow(normalized, 1.5),
            ResponseCurveType.Aggressive => Math.Pow(normalized, 0.7),
            ResponseCurveType.Instant => normalized > 0.1 ? 1.0 : normalized * 10,
            _ => normalized
        };

        curved *= Sensitivity;
        curved = Math.Clamp(curved, 0.0, 1.0);

        return sign * curved;
    }
}
