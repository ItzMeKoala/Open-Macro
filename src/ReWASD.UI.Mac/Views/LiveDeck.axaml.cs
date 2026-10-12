using Avalonia;
using Avalonia.Controls;

namespace ReWASD.UI.Mac.Views;

/// <summary>The live readings under the controller. Shows more as it gets more room: the graph needs height, the timing tile width.</summary>
public partial class LiveDeck : UserControl
{
    /// <summary>Below this height the graph is left out.</summary>
    public const double ScopeHeight = 380;

    /// <summary>Below this width the timing tile is left out.</summary>
    public const double TimingWidth = 900;

    public LiveDeck()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Fit(e.NewSize);
    }

    private void Fit(Size size)
    {
        var scope = this.FindControl<Border>("ScopeTile")!;
        scope.IsVisible = size.Height >= ScopeHeight;
        scope.Height = Math.Clamp(size.Height * 0.42, 150, 240);

        bool wide = size.Width >= TimingWidth;
        var timing = this.FindControl<Border>("TimingTile")!;
        timing.IsVisible = wide;
        var tiles = this.FindControl<Grid>("Tiles")!;
        tiles.ColumnDefinitions[5].Width = new GridLength(wide ? 10 : 0);
        tiles.ColumnDefinitions[6].Width = wide ? new GridLength(0.9, GridUnitType.Star) : new GridLength(0);
    }
}
