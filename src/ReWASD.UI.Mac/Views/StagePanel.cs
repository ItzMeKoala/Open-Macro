using Avalonia;
using Avalonia.Controls;

namespace ReWASD.UI.Mac.Views;

/// <summary>
/// Lays out the controller drawing (first child) and the live panel (second child). The drawing never grows past
/// <see cref="StageHeight"/> while there is room for the live panel under it: a bigger window shows more detail instead of a bigger
/// controller. When the window is too short for the panel, the drawing gets all the room and the panel is left out.
/// </summary>
public sealed class StagePanel : Panel
{
    public static readonly StyledProperty<bool> DeckWantedProperty = AvaloniaProperty.Register<StagePanel, bool>(nameof(DeckWanted), true);
    public static readonly StyledProperty<double> StageHeightProperty = AvaloniaProperty.Register<StagePanel, double>(nameof(StageHeight), 480);
    public static readonly StyledProperty<double> MinDeckHeightProperty = AvaloniaProperty.Register<StagePanel, double>(nameof(MinDeckHeight), 200);
    public static readonly StyledProperty<double> MaxDeckHeightProperty = AvaloniaProperty.Register<StagePanel, double>(nameof(MaxDeckHeight), 620);

    static StagePanel() => AffectsMeasure<StagePanel>(DeckWantedProperty, StageHeightProperty, MinDeckHeightProperty, MaxDeckHeightProperty);

    /// <summary>The live panel belongs on this screen at all (the gamepad tab).</summary>
    public bool DeckWanted { get => GetValue(DeckWantedProperty); set => SetValue(DeckWantedProperty, value); }

    /// <summary>The height the controller drawing keeps once the live panel has room.</summary>
    public double StageHeight { get => GetValue(StageHeightProperty); set => SetValue(StageHeightProperty, value); }

    /// <summary>The live panel is only shown when it gets at least this much height.</summary>
    public double MinDeckHeight { get => GetValue(MinDeckHeightProperty); set => SetValue(MinDeckHeightProperty, value); }

    /// <summary>Past this the live panel stops growing and the drawing takes the rest.</summary>
    public double MaxDeckHeight { get => GetValue(MaxDeckHeightProperty); set => SetValue(MaxDeckHeightProperty, value); }

    /// <summary>Whether the live panel is laid out at the moment.</summary>
    public bool IsDeckShown { get; private set; }

    /// <summary>Raised after a layout pass that showed or left out the live panel.</summary>
    public event EventHandler? DeckShownChanged;

    /// <summary>How the height is shared: the drawing's height and the panel's (zero when the panel is left out).</summary>
    public static (double Stage, double Deck) Split(double height, bool deckWanted, double stageHeight, double minDeck, double maxDeck)
    {
        if (!deckWanted || double.IsInfinity(height) || height - stageHeight < minDeck) return (height, 0);
        double deck = Math.Min(height - stageHeight, maxDeck);
        return (height - deck, deck);
    }

    private (double Stage, double Deck) SplitFor(double height) =>
        Split(height, Children.Count > 1 && DeckWanted, StageHeight, MinDeckHeight, MaxDeckHeight);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (stage, deck) = SplitFor(availableSize.Height);
        if (Children.Count > 0) Children[0].Measure(new Size(availableSize.Width, stage));
        if (Children.Count > 1) Children[1].Measure(new Size(availableSize.Width, deck));
        double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? (Children.Count > 0 ? Children[0].DesiredSize.Height : 0) : availableSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (stage, deck) = SplitFor(finalSize.Height);
        if (Children.Count > 0) Children[0].Arrange(new Rect(0, 0, finalSize.Width, stage));
        if (Children.Count > 1) Children[1].Arrange(new Rect(0, stage, finalSize.Width, deck));

        bool shown = deck > 0;
        if (Children.Count > 1 && Children[1].IsVisible != shown) Children[1].IsVisible = shown;     // left out means gone, not squeezed to nothing
        if (shown != IsDeckShown)
        {
            IsDeckShown = shown;
            DeckShownChanged?.Invoke(this, EventArgs.Empty);
        }
        return finalSize;
    }
}
