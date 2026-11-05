using System.Globalization;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>Returns the first value that is a non-blank string, e.g. the formula text, falling back to the activity description.</summary>
public sealed class FirstNonEmptyConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.OfType<string>().FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
