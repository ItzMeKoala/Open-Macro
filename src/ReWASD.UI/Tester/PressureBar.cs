using System.Windows;
using System.Windows.Media;

namespace ReWASD.UI.TesterControls;

/// <summary>A trigger's pull as an upright bar: a track with quarter marks, filled from the bottom, and a line at the highest pull seen.</summary>
public sealed class PressureBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(PressureBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PeakProperty = DependencyProperty.Register(nameof(Peak), typeof(double), typeof(PressureBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Peak { get => (double)GetValue(PeakProperty); set => SetValue(PeakProperty, value); }

    private static Brush Solid(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static Pen Line(string hex, double width)
    {
        var p = new Pen(Solid(hex), width);
        p.Freeze();
        return p;
    }

    private static readonly Brush Track = Solid("#111113");
    private static readonly Pen TrackEdge = Line("#2E2E33", 1);
    private static readonly Brush Fill = Solid("#E9C46A");
    private static readonly Pen Mark = Line("#3A3A40", 1);
    private static readonly Pen PeakLine = Line("#F2D68F", 1.5);

    /// <summary>The filled part of the track for a pull, from the bottom up.</summary>
    public static Rect FillRect(Rect track, double value)
    {
        double h = track.Height * Math.Clamp(value, 0, 1);
        return new Rect(track.X, track.Bottom - h, track.Width, h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = Math.Min(ActualWidth, 16);
        if (width < 4 || ActualHeight < 10) return;

        var track = new Rect((ActualWidth - width) / 2, 0, width, ActualHeight);
        dc.DrawRoundedRectangle(Track, TrackEdge, track, 4, 4);

        var inner = track;
        inner.Inflate(-2, -2);
        var fill = FillRect(inner, Value);
        if (fill.Height > 0.5) dc.DrawRoundedRectangle(Fill, null, fill, 3, 3);

        foreach (double q in new[] { 0.25, 0.5, 0.75 })
        {
            double y = track.Bottom - track.Height * q;
            dc.DrawLine(Mark, new Point(track.Right + 3, y), new Point(track.Right + 7, y));
        }

        if (Peak > 0.01)
        {
            double y = track.Bottom - 2 - (track.Height - 4) * Math.Clamp(Peak, 0, 1);
            dc.DrawLine(PeakLine, new Point(track.X - 3, y), new Point(track.Right + 3, y));
        }
    }
}
