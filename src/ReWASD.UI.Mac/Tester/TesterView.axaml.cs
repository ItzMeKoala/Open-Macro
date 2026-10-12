using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReWASD.UI.Mac.Tester;

/// <summary>The full-screen controller tester. It is created when the window goes full screen and dropped when it leaves.</summary>
public partial class TesterView : UserControl
{
    /// <summary>Raised by the "Exit full screen" button.</summary>
    public event EventHandler? ExitRequested;

    public TesterView()
    {
        InitializeComponent();
        this.FindControl<Button>("ExitButton")!.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
