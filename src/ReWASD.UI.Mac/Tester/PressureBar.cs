using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReWASD.UI.Mac.Tester;

/// <summary>A trigger's pull as an upright bar: a track with quarter marks, filled from the bottom, and a line at the highest pull seen.</summary>
public sealed class PressureBar : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<PressureBar, double>(nameof(Value));
    public static readonly StyledProperty<double> PeakProperty = AvaloniaProperty.Register<PressureBar, double>(nameof(Peak));

    static PressureBar() => AffectsRender<PressureBar>(ValueProperty, PeakProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Peak { get => GetValue(PeakProperty); set => SetValue(PeakProperty, value); }

    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#111113")).ToImmutable();
    private static readonly IPen TrackEdge = new Pen(new SolidColorBrush(Color.Parse("#2E2E33")), 1).ToImmutable();
    private static readonly IBrush Fill = new SolidColorBrush(Color.Parse("#E9C46A")).ToImmutable();
    private static readonly IPen Mark = new Pen(new SolidColorBrush(Color.Parse("#3A3A40")), 1).ToImmutable();
    private static readonly IPen PeakLine = new Pen(new SolidColorBrush(Color.Parse("#F2D68F")), 1.5).ToImmutable();

    /// <summary>The filled part of the track for a pull, from the bottom up.</summary>
    public static Rect FillRect(Rect track, double value)
    {
        double h = track.Height * Math.Clamp(value, 0, 1);
        return new Rect(track.X, track.Bottom - h, track.Width, h);
    }

    public override void Render(DrawingContext context)
    {
        double width = Math.Min(Bounds.Width, 16);
        if (width < 4 || Bounds.Height < 10) return;

        var track = new Rect((Bounds.Width - width) / 2, 0, width, Bounds.Height);
        context.DrawRectangle(Track, TrackEdge, track, 4, 4);

        var fill = FillRect(track.Deflate(2), Value);
        if (fill.Height > 0.5) context.DrawRectangle(Fill, null, fill, 3, 3);

        foreach (double q in new[] { 0.25, 0.5, 0.75 })
        {
            double y = track.Bottom - track.Height * q;
            context.DrawLine(Mark, new Point(track.Right + 3, y), new Point(track.Right + 7, y));
        }

        if (Peak > 0.01)
        {
            double y = track.Bottom - 2 - (track.Height - 4) * Math.Clamp(Peak, 0, 1);
            context.DrawLine(PeakLine, new Point(track.X - 3, y), new Point(track.Right + 3, y));
        }
    }
}
