using System.Windows;
using System.Windows.Media;
using ReWASD.UI.Tester;

namespace ReWASD.UI.TesterControls;

/// <summary>A scrolling line graph of the last few seconds of some of the controller's axes, newest on the right.</summary>
public sealed class ScopeGraph : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(double[][]), typeof(ScopeGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ScopeGraph),
        new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ScopeGraph),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Colours of the six lines: left x and y, right x and y, left and right trigger.</summary>
    public static readonly string[] Palette = { "#E9C46A", "#F4A261", "#5BC0EB", "#9B8CFF", "#00E676", "#FF6B6B" };

    private static Brush B(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static Pen P(string hex, double width)
    {
        var pen = new Pen(B(hex), width);
        pen.Freeze();
        return pen;
    }

    private static readonly Brush Back = B("#17191E");
    private static readonly Pen Frame = P("#383D49", 1);
    private static readonly Pen Gridline = P("#262A33", 1);
    private static readonly Pen Zero = P("#444A58", 1);
    private static readonly Pen[] Lines = Palette.Select(c => { var p = new Pen(B(c), 1.6) { LineJoin = PenLineJoin.Round }; p.Freeze(); return p; }).ToArray();

    public double[][]? Series { get => (double[][]?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Which of the six series this graph draws.</summary>
    public int[] Channels { get; set; } = { 0, 1, 2, 3, 4, 5 };

    protected override void OnRender(DrawingContext context)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 20 || h < 20) return;

        context.DrawRoundedRectangle(Back, Frame, new Rect(0, 0, w, h), 4, 4);

        // One vertical line a second (the graph holds six), and lines at a quarter, a half and three quarters
        double secondWidth = w * (1000.0 / TesterModel.ScopeStepMs) / TesterModel.ScopeSamples;
        for (double x = w - secondWidth; x > 0; x -= secondWidth) context.DrawLine(Gridline, new Point(x, 0), new Point(x, h));
        double Y(double v) => h - (v - Minimum) / (Maximum - Minimum) * h;
        foreach (double fraction in new[] { 0.25, 0.5, 0.75 }) context.DrawLine(Gridline, new Point(0, h * fraction), new Point(w, h * fraction));
        if (Minimum < 0 && Maximum > 0) context.DrawLine(Zero, new Point(0, Y(0)), new Point(w, Y(0)));

        var series = Series;
        if (series == null) return;

        double dx = w / (TesterModel.ScopeSamples - 1);
        foreach (int ch in Channels)
        {
            if (ch < 0 || ch >= series.Length || series[ch].Length < 2) continue;

            var data = series[ch];
            double x0 = w - (data.Length - 1) * dx;
            var figure = new PathFigure { StartPoint = new Point(x0, Y(Math.Clamp(data[0], Minimum, Maximum))), IsClosed = false, IsFilled = false };
            for (int i = 1; i < data.Length; i++) figure.Segments.Add(new LineSegment(new Point(x0 + i * dx, Y(Math.Clamp(data[i], Minimum, Maximum))), true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            context.DrawGeometry(null, Lines[ch % Lines.Length], geometry);
        }
    }
}
