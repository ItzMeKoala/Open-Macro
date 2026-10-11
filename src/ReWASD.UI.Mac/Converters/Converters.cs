using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using ReWASD.Core.Models;

namespace ReWASD.UI.Mac.Converters;

/// <summary>Binds a RadioButton to an enum property: the ConverterParameter is the enum member name the button represents.</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter != null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : BindingOperations.DoNothing;
}

/// <summary>Plain-language names for how a mapping behaves once it fires.</summary>
public sealed class MappingModeLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MappingMode m ? Labels.Mode(m) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>Plain-language names for how a macro repeats.</summary>
public sealed class MacroRepeatLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MacroRepeatMode m ? Labels.MacroRepeat(m) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>A single radius number (as the view models use) to the uniform corner radius a Border wants.</summary>
public sealed class DoubleToCornerRadiusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Avalonia.CornerRadius(value is double d ? d : 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>"#RRGGBB" text (as the view models use for legend colours) to a brush.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && Avalonia.Media.Color.TryParse(s, out var color)
            ? new Avalonia.Media.SolidColorBrush(color)
            : Avalonia.Media.Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>"M 0 0 L ..." path text (as the skins use for shapes) to a geometry.</summary>
public sealed class PathTextToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s) ? Avalonia.Media.Geometry.Parse(s) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
