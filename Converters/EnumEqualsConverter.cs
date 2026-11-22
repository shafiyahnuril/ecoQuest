using System.Globalization;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>True when the bound enum value has the name given in ConverterParameter (one-way; used by segmented range toggles).</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
