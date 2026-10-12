using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReWASD.UI.Tester;

namespace ReWASD.UI.Mac.Tester;

/// <summary>A scrolling line graph of the last few seconds of some of the controller's axes, newest on the right.</summary>
public sealed class ScopeGraph : Control
{
    public static readonly StyledProperty<double[][]?> SeriesProperty = AvaloniaProperty.Register<ScopeGraph, double[][]?>(nameof(Series));
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<ScopeGraph, double>(nameof(Minimum), -1.0);
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<ScopeGraph, double>(nameof(Maximum), 1.0);

    /// <summary>Colours of the six lines: left x and y, right x and y, left and right trigger.</summary>
    public static readonly Color[] Palette =
    {
        Color.Parse("#E9C46A"), Color.Parse("#F4A261"), Color.Parse("#5BC0EB"), Color.Parse("#9B8CFF"), Color.Parse("#00E676"), Color.Parse("#FF6B6B")
    };

    private static readonly IBrush Back = new SolidColorBrush(Color.Parse("#17191E"));
    private static readonly IPen Frame = new Pen(new SolidColorBrush(Color.Parse("#383D49")), 1);
    private static readonly IPen Gridline = new Pen(new SolidColorBrush(Color.Parse("#262A33")), 1);
    private static readonly IPen Zero = new Pen(new SolidColorBrush(Color.Parse("#444A58")), 1);

    static ScopeGraph() => AffectsRender<ScopeGraph>(SeriesProperty, MinimumProperty, MaximumProperty);

    public double[][]? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Which of the six series this graph draws.</summary>
    public int[] Channels { get; set; } = { 0, 1, 2, 3, 4, 5 };

    public override void Render(DrawingContext context)
    {
        var area = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (area.Width < 20 || area.Height < 20) return;

        context.DrawRectangle(Back, Frame, area, 4, 4);

        // One vertical line a second (the graph holds six), and lines at the top, middle and bottom
        double step = TesterModel.ScopeStepMs;
        double secondWidth = area.Width * (1000.0 / step) / TesterModel.ScopeSamples;
        for (double x = area.Width - secondWidth; x > 0; x -= secondWidth) context.DrawLine(Gridline, new Point(x, 0), new Point(x, area.Height));
        double Y(double v) => area.Height - (v - Minimum) / (Maximum - Minimum) * area.Height;
        foreach (double fraction in new[] { 0.25, 0.5, 0.75 }) context.DrawLine(Gridline, new Point(0, area.Height * fraction), new Point(area.Width, area.Height * fraction));
        if (Minimum < 0 && Maximum > 0) context.DrawLine(Zero, new Point(0, Y(0)), new Point(area.Width, Y(0)));

        var series = Series;
        if (series == null) return;

        double dx = area.Width / (TesterModel.ScopeSamples - 1);
        foreach (int ch in Channels)
        {
            if (ch < 0 || ch >= series.Length || series[ch].Length < 2) continue;

            var data = series[ch];
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                double x0 = area.Width - (data.Length - 1) * dx;
                c.BeginFigure(new Point(x0, Y(Math.Clamp(data[0], Minimum, Maximum))), false);
                for (int i = 1; i < data.Length; i++) c.LineTo(new Point(x0 + i * dx, Y(Math.Clamp(data[i], Minimum, Maximum))));
                c.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Palette[ch % Palette.Length]), 1.6, lineJoin: PenLineJoin.Round), geometry);
        }
    }
}
