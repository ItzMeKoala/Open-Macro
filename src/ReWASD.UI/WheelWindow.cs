using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ReWASD.Core.Models;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI;

/// <summary>The command wheel drawn on screen while its button is held. It never takes focus, so a game keeps getting its input.</summary>
public sealed class WheelWindow : Window
{
    private const double Size = 380;
    private const double Outer = 176;
    private const double Inner = 62;

    private static readonly Brush Slice = Frozen(new SolidColorBrush(Color.FromArgb(0xE6, 0x1C, 0x1F, 0x27)));
    private static readonly Brush Chosen = Frozen(new SolidColorBrush(Color.FromRgb(0xE9, 0xC4, 0x6A)));
    private static readonly Brush Rim = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush Ink = Frozen(new SolidColorBrush(Color.FromRgb(0x17, 0x1A, 0x21)));

    private readonly MainViewModel _vm;
    private readonly Canvas _canvas = new() { Width = Size, Height = Size };
    private readonly List<Path> _slices = new();
    private readonly List<TextBlock> _labels = new();
    private readonly TextBlock _centre = new();

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    public WheelWindow(MainViewModel vm)
    {
        _vm = vm;
        Title = "Open-Macro command wheel";
        Width = Size;
        Height = Size;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = _canvas;

        _vm.PropertyChanged += OnChanged;
        _vm.WheelItems.CollectionChanged += OnItemsChanged;
        Closed += (_, _) =>
        {
            _vm.PropertyChanged -= OnChanged;
            _vm.WheelItems.CollectionChanged -= OnItemsChanged;
        };
        Rebuild();
    }

    /// <summary>The slice currently drawn as chosen (for tests), or -1.</summary>
    internal int DrawnSelection => _slices.FindIndex(p => ReferenceEquals(p.Fill, Chosen));

    internal int SliceCount => _slices.Count;
    internal string CentreText => _centre.Text;

    private void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.WheelSelectedIndex)) Highlight();
    }

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _canvas.Children.Clear();
        _slices.Clear();
        _labels.Clear();

        int count = _vm.WheelItems.Count;
        double centre = Size / 2;
        for (int i = 0; i < count; i++)
        {
            var (start, end) = CommandWheel.SliceAngles(i, count);
            var path = new Path { Data = Sector(centre, centre, Outer, Inner, start + 1.5, end - 1.5), Fill = Slice, Stroke = Rim, StrokeThickness = 1 };
            _slices.Add(path);
            _canvas.Children.Add(path);

            double mid = (start + end) / 2 * Math.PI / 180;
            double radius = (Outer + Inner) / 2;
            var label = new TextBlock
            {
                Text = _vm.WheelItems[i].Label,
                Width = 96,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, centre + radius * Math.Sin(mid) - 48);
            Canvas.SetTop(label, centre - radius * Math.Cos(mid) - 10);
            _labels.Add(label);
            _canvas.Children.Add(label);
        }

        _centre.Width = Inner * 2 - 12;
        _centre.TextAlignment = TextAlignment.Center;
        _centre.TextWrapping = TextWrapping.Wrap;
        _centre.FontSize = 14;
        _centre.FontWeight = FontWeights.Bold;
        _centre.Foreground = Brushes.White;
        Canvas.SetLeft(_centre, centre - _centre.Width / 2);
        Canvas.SetTop(_centre, centre - 10);
        if (!_canvas.Children.Contains(_centre)) _canvas.Children.Add(_centre);

        Highlight();
    }

    private void Highlight()
    {
        int chosen = _vm.WheelSelectedIndex;
        for (int i = 0; i < _slices.Count; i++)
        {
            bool on = i == chosen;
            _slices[i].Fill = on ? Chosen : Slice;
            _labels[i].Foreground = on ? Ink : Brushes.White;
        }
        _centre.Text = chosen >= 0 && chosen < _vm.WheelItems.Count ? _vm.WheelItems[chosen].Label : string.Empty;
    }

    private static Geometry Sector(double cx, double cy, double outer, double inner, double startDegrees, double endDegrees)
    {
        Point P(double r, double degrees)
        {
            double a = degrees * Math.PI / 180;
            return new Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a));      // 0 degrees is straight up, going clockwise
        }

        bool large = endDegrees - startDegrees > 180;
        var figure = new PathFigure { StartPoint = P(outer, startDegrees), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(P(outer, endDegrees), new Size(outer, outer), 0, large, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(P(inner, endDegrees), true));
        figure.Segments.Add(new ArcSegment(P(inner, startDegrees), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}
