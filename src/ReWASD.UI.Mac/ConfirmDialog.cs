using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ReWASD.UI.Mac;

/// <summary>A small yes/no question window (Avalonia has no message box of its own).</summary>
internal static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1C1C1C"))
        };

        var yes = new Button { Content = "Yes, continue", Padding = new Thickness(16, 6), Margin = new Thickness(0, 0, 8, 0) };
        var no = new Button { Content = "No, cancel", Padding = new Thickness(16, 6), IsDefault = true };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new ScrollViewer
                {
                    MaxHeight = 320,
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, FontSize = 13 }
                },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { yes, no } }
            }
        };

        return await dialog.ShowDialog<bool>(owner);
    }
}
