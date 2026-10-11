using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReWASD.Core.Models;

namespace ReWASD.UI.ViewModels;

public partial class ButtonViewModel : ObservableObject
{
    [ObservableProperty]
    private ControllerButton _button;

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
