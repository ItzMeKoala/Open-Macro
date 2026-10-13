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
        Title = "Open-Macro overlay";
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
        Art = new ArtView { IsHitTestVisible = false, ShowEditing = false, Sources = vm.ControllerButtons };

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

        var grid = new Grid { Background = Brushes.Transparent };
        grid.Children.Add(Art);
        grid.Children.Add(text);
        Content = grid;

        // Follow the view model directly (rather than through bindings) so the window is right the moment it exists
        void Refresh()
        {
            if (!ReferenceEquals(Art.Scene, vm.SkinArt)) Art.Scene = vm.SkinArt;
            Art.Visibility = vm.OverlayHasArt ? Visibility.Visible : Visibility.Collapsed;
            text.Visibility = vm.OverlayHasArt ? Visibility.Collapsed : Visibility.Visible;
            text.Text = vm.PressedText;
        }

        System.ComponentModel.PropertyChangedEventHandler onChanged = (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SkinArt) or nameof(MainViewModel.OverlayHasArt) or nameof(MainViewModel.PressedText)) Refresh();
        };
        vm.PropertyChanged += onChanged;
        Closed += (_, _) => vm.PropertyChanged -= onChanged;
        Refresh();

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch (InvalidOperationException) { /* the button was let go already */ }
            }
        };
    }
}
