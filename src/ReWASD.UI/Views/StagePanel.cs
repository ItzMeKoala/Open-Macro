using System.Windows;
using System.Windows.Controls;

namespace ReWASD.UI.Views;

/// <summary>
/// Lays out the controller drawing (first child) and the live panel (second child). The drawing never grows past
/// <see cref="StageHeight"/> while there is room for the live panel under it: a bigger window shows more detail instead of a bigger
/// controller. When the window is too short for the panel, the drawing gets all the room and the panel is left out.
/// </summary>
public sealed class StagePanel : Panel
{
    public static readonly DependencyProperty DeckWantedProperty = DependencyProperty.Register(nameof(DeckWanted), typeof(bool), typeof(StagePanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty StageHeightProperty = DependencyProperty.Register(nameof(StageHeight), typeof(double), typeof(StagePanel),
        new FrameworkPropertyMetadata(480.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty MinDeckHeightProperty = DependencyProperty.Register(nameof(MinDeckHeight), typeof(double), typeof(StagePanel),
        new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty MaxDeckHeightProperty = DependencyProperty.Register(nameof(MaxDeckHeight), typeof(double), typeof(StagePanel),
        new FrameworkPropertyMetadata(620.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The live panel belongs on this screen at all (the gamepad tab).</summary>
    public bool DeckWanted { get => (bool)GetValue(DeckWantedProperty); set => SetValue(DeckWantedProperty, value); }

    /// <summary>The height the controller drawing keeps once the live panel has room.</summary>
    public double StageHeight { get => (double)GetValue(StageHeightProperty); set => SetValue(StageHeightProperty, value); }

    /// <summary>The live panel is only shown when it gets at least this much height.</summary>
    public double MinDeckHeight { get => (double)GetValue(MinDeckHeightProperty); set => SetValue(MinDeckHeightProperty, value); }

    /// <summary>Past this the live panel stops growing and the drawing takes the rest.</summary>
    public double MaxDeckHeight { get => (double)GetValue(MaxDeckHeightProperty); set => SetValue(MaxDeckHeightProperty, value); }

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
        Split(height, InternalChildren.Count > 1 && DeckWanted, StageHeight, MinDeckHeight, MaxDeckHeight);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (stage, deck) = SplitFor(availableSize.Height);
        if (InternalChildren.Count > 0) InternalChildren[0].Measure(new Size(availableSize.Width, stage));
        if (InternalChildren.Count > 1) InternalChildren[1].Measure(new Size(availableSize.Width, deck));
        double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? (InternalChildren.Count > 0 ? InternalChildren[0].DesiredSize.Height : 0) : availableSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (stage, deck) = SplitFor(finalSize.Height);
        if (InternalChildren.Count > 0) InternalChildren[0].Arrange(new Rect(0, 0, finalSize.Width, stage));
        if (InternalChildren.Count > 1) InternalChildren[1].Arrange(new Rect(0, stage, finalSize.Width, deck));

        bool shown = deck > 0;
        if (InternalChildren.Count > 1)
        {
            var wanted = shown ? Visibility.Visible : Visibility.Collapsed;       // left out means gone, not squeezed to nothing
            if (InternalChildren[1].Visibility != wanted) InternalChildren[1].Visibility = wanted;
        }
        if (shown != IsDeckShown)
        {
            IsDeckShown = shown;
            DeckShownChanged?.Invoke(this, EventArgs.Empty);
        }
        return finalSize;
    }
}
