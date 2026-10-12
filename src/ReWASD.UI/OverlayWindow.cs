using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ReWASD.UI.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI;

/// <summary>A small borderless window that stays on top and shows the controller moving, for streams and screen recordings.</summary>
public sealed class OverlayWindow : Window
{
    public ArtView Art { get; }

    public OverlayWindow(MainViewModel vm)
    {
        DataContext = vm;
        Title = "ReWASD overlay";
        Width = 420;
        Height = 300;
        MinWidth = 160;
        MinHeight = 110;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        // Clicks drag the window; the drawing itself never reacts to the mouse here
        Art = new ArtView { IsHitTestVisible = false, ShowEditing = false };
        Art.SetBinding(ArtView.SceneProperty, new Binding(nameof(MainViewModel.SkinArt)));
        Art.SetBinding(ArtView.SourcesProperty, new Binding(nameof(MainViewModel.ControllerButtons)));
        Art.SetBinding(VisibilityProperty, new Binding(nameof(MainViewModel.OverlayHasArt)) { Converter = new BooleanToVisibilityConverter() });

        var text = new TextBlock
        {
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false
        };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(MainViewModel.PressedText)));
        text.SetBinding(VisibilityProperty, new Binding(nameof(MainViewModel.OverlayHasArt)) { Converter = new InverseBooleanToVisibilityConverter() });

        var grid = new Grid { Background = Brushes.Transparent };
        grid.Children.Add(Art);
        grid.Children.Add(text);
        Content = grid;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch (InvalidOperationException) { /* the button was let go already */ }
            }
        };
    }

    private sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
