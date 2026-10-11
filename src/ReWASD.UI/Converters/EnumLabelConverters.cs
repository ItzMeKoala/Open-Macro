using System.Globalization;
using System.Windows.Data;
using ReWASD.Core.Models;

namespace ReWASD.UI.Converters;

/// <summary>Plain-language names for how a mapping behaves once it fires.</summary>
public class MappingModeLabelConverter : IValueConverter
{
    public static string Label(MappingMode mode) => Labels.Mode(mode);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MappingMode m ? Label(m) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Plain-language names for how a macro repeats.</summary>
public class MacroRepeatLabelConverter : IValueConverter
{
    public static string Label(MacroRepeatMode mode) => Labels.MacroRepeat(mode);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MacroRepeatMode m ? Label(m) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
