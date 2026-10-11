using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;
using ReWASD.Core.Skins;

namespace ReWASD.UI.ViewModels;

/// <summary>One selectable input source: a pad button on the controller drawing, or a key or mouse button in a picker.</summary>
public partial class ButtonViewModel : ObservableObject
{
    [ObservableProperty]
    private SourceKind _kind = SourceKind.Pad;

    /// <summary>For pad sources, the button. None for keys and mouse buttons.</summary>
    [ObservableProperty]
    private ControllerButton _button;

    /// <summary>Profile name of the source: the button name, key name or mouse button name.</summary>
    [ObservableProperty]
    private string _sourceName = string.Empty;

    /// <summary>A readable name for headings and messages (for example "Left Stick Click"), independent of the picture style.</summary>
    [ObservableProperty]
    private string _friendlyName = string.Empty;

    /// <summary>What is printed on the button (a letter, a PlayStation symbol, a key name...).</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>Colour of the printed legend, as a hex string (for example Xbox's green A).</summary>
    [ObservableProperty]
    private string _legendColor = "#E0E6EE";

    [ObservableProperty]
    private double _cornerRadius = 19;

    /// <summary>When set, the source is drawn as this shape (a path, in drawing coordinates) instead of a box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShape))]
    private string? _shapeData;

    /// <summary>Cuts <see cref="ShapeData"/> down to a region of it, so a button can be part of a bigger outline.</summary>
    [ObservableProperty]
    private string? _clipData;

    public bool HasShape => !string.IsNullOrEmpty(ShapeData);

    /// <summary>Where the legend is printed for shaped sources, in drawing coordinates.</summary>
    [ObservableProperty]
    private double _legendLeft;

    [ObservableProperty]
    private double _legendTop;

    [ObservableProperty]
    private double _legendFontSize = 12;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 38;

    [ObservableProperty]
    private double _height = 38;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRemapped;

    [ObservableProperty]
    private string _targetText = string.Empty;

    // Live state of the physical input, updated many times a second
    [ObservableProperty]
    private bool _isPressed;

    /// <summary>0..1 for triggers; shown as a fill rising inside the icon.</summary>
    [ObservableProperty]
    private double _analogValue;

    /// <summary>True for the stick-click buttons, whose icons show a dot following the stick.</summary>
    [ObservableProperty]
    private bool _hasStick;

    /// <summary>True while the stick is pushed away from centre, so the dot is hidden when idle.</summary>
    [ObservableProperty]
    private bool _stickActive;

    [ObservableProperty]
    private double _stickOffsetX;

    [ObservableProperty]
    private double _stickOffsetY;

    public Action<ButtonViewModel>? OnSelected { get; set; }

    [RelayCommand]
    private void Select()
    {
        OnSelected?.Invoke(this);
    }
}

public enum RemapKind
{
    Nothing,
    Pad,
    Key,
    Mouse,
    Scroll,
    Layer
}

/// <summary>One choice in the "Remap to" dropdown, with the icon shown beside it.</summary>
public sealed partial class RemapOption : ObservableObject
{
    public RemapOption(RemapKind kind, string group, string label)
    {
        Kind = kind;
        Group = group;
        Label = label;
    }

    public RemapKind Kind { get; }
    public string Group { get; }
    public string Label { get; }

    /// <summary>For Pad options, the virtual button.</summary>
    public ControllerButton Pad { get; init; }

    /// <summary>For Key and Mouse options, the key or mouse button name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>For Scroll options +1 (up) or -1 (down); for Layer options the layer index.</summary>
    public int Number { get; init; }

    // The icon's face. Pad options change with the controller picture (Xbox letters vs PlayStation symbols).
    [ObservableProperty]
    private string _legend = string.Empty;

    [ObservableProperty]
    private string _legendColor = "#E0E6EE";

    [ObservableProperty]
    private IconShape _shape = IconShape.Circle;
}

/// <summary>One row of the virtual-controller menu.</summary>
public sealed record VirtualControllerOption(ControllerType Type, string Name, bool IsAvailable, string Note);

/// <summary>A checkbox in the layer-activation chord picker.</summary>
public partial class ModifierOption : ObservableObject
{
    public ModifierOption(ControllerButton button, string label)
    {
        Button = button;
        Label = label;
    }

    public ControllerButton Button { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isChecked;

    public Action<ModifierOption>? Changed { get; set; }

    partial void OnIsCheckedChanged(bool value) => Changed?.Invoke(this);
}
