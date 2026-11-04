using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>
/// True when the bound window width is at or below the compact breakpoint (token <c>Breakpoint.Compact</c>, 1160 px;
/// prototype <c>@media(max-width:1160px)</c>). Reads the token itself because StaticResource cannot feed ConverterParameter.
/// </summary>
public sealed class CompactWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double width && Application.Current.TryFindResource("Breakpoint.Compact") is double limit && width <= limit;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
