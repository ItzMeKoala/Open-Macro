using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ReWASD.Core.Skins;

namespace ReWASD.UI.Mac.Views;

/// <summary>
/// The picture shown beside a choice or on a button: a coloured letter or PlayStation symbol in a round badge, a
/// keycap, or a vector icon. Windows draws the icons with a system icon font; macOS has none, so the same codes
/// are mapped to the vector icons in Icons.axaml.
/// </summary>
public sealed class OptionBadge : Border
{
    public static readonly StyledProperty<string?> LegendProperty = AvaloniaProperty.Register<OptionBadge, string?>(nameof(Legend));
    public static readonly StyledProperty<string?> LegendColorProperty = AvaloniaProperty.Register<OptionBadge, string?>(nameof(LegendColor));
    public static readonly StyledProperty<IconShape> ShapeProperty = AvaloniaProperty.Register<OptionBadge, IconShape>(nameof(Shape));

    // Segoe MDL2 code points the view model uses for icon legends -> the vector icon that replaces them
    private static readonly Dictionary<char, string> IconKeys = new()
    {
        [''] = "IconMouse",
        [''] = "IconCross",
        [''] = "IconArrowUp",
        [''] = "IconArrowDown",
        [''] = "IconLayers"
    };

    private static readonly IBrush KeyLegend = new SolidColorBrush(Color.Parse("#E0E6EE"));

    private readonly TextBlock _text = new()
    {
        FontWeight = FontWeight.Bold,
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly PathIcon _icon = new() { Width = 14, Height = 14, IsVisible = false };

    public OptionBadge()
    {
        Height = 26;
        Margin = new Thickness(0, 0, 10, 0);
        Padding = new Thickness(5, 0);
        Background = new SolidColorBrush(Color.Parse("#2A3140"));
        BorderBrush = new SolidColorBrush(Color.Parse("#3E485C"));
        Child = new Grid { Children = { _text, _icon } };
        Update();
    }

    public string? Legend { get => GetValue(LegendProperty); set => SetValue(LegendProperty, value); }
    public string? LegendColor { get => GetValue(LegendColorProperty); set => SetValue(LegendColorProperty, value); }
    public IconShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LegendProperty || change.Property == LegendColorProperty || change.Property == ShapeProperty) Update();
    }

    private void Update()
    {
        string legend = Legend ?? string.Empty;
        bool keycap = Shape == IconShape.KeyCap;

        CornerRadius = new CornerRadius(keycap ? 5 : 13);
        MinWidth = keycap ? 34 : Shape == IconShape.Pill ? 36 : 26;
        BorderThickness = keycap ? new Thickness(1, 1, 1, 3) : new Thickness(1);

        // Icon legends (single private-use code points) draw as vector icons in the accent colour
        if (Shape == IconShape.Glyph || (legend.Length == 1 && IconKeys.ContainsKey(legend[0])))
        {
            var geometry = legend.Length == 1 ? FindIcon(legend[0]) : null;
            _icon.Data = geometry;
            _icon.IsVisible = geometry != null;
            _icon.Foreground = FindBrush("BrushCyan");
            _text.IsVisible = false;
            return;
        }

        _icon.IsVisible = false;
        _text.IsVisible = true;
        _text.Text = legend;
        _text.FontSize = keycap ? 11 : 12;
        _text.Foreground = keycap || string.IsNullOrEmpty(LegendColor) ? KeyLegend : TryBrush(LegendColor);
    }

    private static Geometry? FindIcon(char glyph)
    {
        if (!IconKeys.TryGetValue(glyph, out var key)) return null;
        return Application.Current?.TryFindResource(key, out var resource) == true ? resource as Geometry : null;
    }

    private static IBrush? FindBrush(string key) =>
        Application.Current?.TryFindResource(key, out var resource) == true ? resource as IBrush : null;

    private static IBrush TryBrush(string? color) =>
        Color.TryParse(color, out var parsed) ? new SolidColorBrush(parsed) : KeyLegend;
}
