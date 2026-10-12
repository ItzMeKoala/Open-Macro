using System.Windows;
using System.Windows.Controls;

namespace ReWASD.UI.Views;

public partial class InputSurface : UserControl
{
    public InputSurface()
    {
        InitializeComponent();

        // If the detailed picture ever fails to build or animate, show the plain outline instead of a broken picture
        Art.RenderFailed += (_, _) =>
        {
            Art.Visibility = Visibility.Collapsed;
            Legacy.Visibility = Visibility.Visible;
        };
        KeysArt.RenderFailed += (_, _) =>
        {
            KeysArt.Visibility = Visibility.Collapsed;
            Legacy.Visibility = Visibility.Visible;
        };
        MouseArt.RenderFailed += (_, _) =>
        {
            MouseArt.Visibility = Visibility.Collapsed;
            Legacy.Visibility = Visibility.Visible;
        };
    }
}
