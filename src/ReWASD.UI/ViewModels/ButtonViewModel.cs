using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;

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

    [ObservableProperty]
    private string _displayName = string.Empty;

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

    public Action<ButtonViewModel>? OnSelected { get; set; }

    [RelayCommand]
    private void Select()
    {
        OnSelected?.Invoke(this);
    }
}

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
