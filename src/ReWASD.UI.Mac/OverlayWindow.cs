using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReWASD.UI.Mac.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac;

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
        CanResize = true;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };

        // Clicks drag the window; the drawing itself never reacts to the mouse here
        Art = new ArtView { IsHitTestVisible = false, ShowEditing = false };
        Art.Bind(ArtView.SceneProperty, new Binding(nameof(MainViewModel.SkinArt)));
        Art.Bind(ArtView.SourcesProperty, new Binding(nameof(MainViewModel.ControllerButtons)));
        Art.Bind(IsVisibleProperty, new Binding(nameof(MainViewModel.OverlayHasArt)));

        var text = new TextBlock
        {
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false
        };
        text.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.PressedText)));
        text.Bind(IsVisibleProperty, new Binding(nameof(MainViewModel.OverlayHasArt)) { Converter = Avalonia.Data.Converters.BoolConverters.Not });

        Content = new Grid { Background = Brushes.Transparent, Children = { Art, text } };
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
    }
}
