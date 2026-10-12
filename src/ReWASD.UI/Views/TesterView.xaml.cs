using System.Windows.Controls;

namespace ReWASD.UI.Views;

/// <summary>The full-screen controller tester. It is created when the window goes full screen and dropped when it leaves.</summary>
public partial class TesterView : UserControl
{
    /// <summary>Raised by the "Exit full screen" button.</summary>
    public event EventHandler? ExitRequested;

    public TesterView()
    {
        InitializeComponent();
        ExitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
