using System.Globalization;
using System.Windows.Data;

namespace ReWASD.UI.Converters;

/// <summary>Binds a RadioButton to an enum property: the ConverterParameter is the enum member name the button represents.</summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter != null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}
