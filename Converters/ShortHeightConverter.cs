using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>
/// True when the bound height is at or below the short-window breakpoint (token <c>Breakpoint.ShortHeight</c>, 740 px;
/// prototype <c>@media(max-height:740px)</c> of the auth pages). Reads the token itself because StaticResource cannot feed ConverterParameter.
/// </summary>
public sealed class ShortHeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double height && Application.Current.TryFindResource("Breakpoint.ShortHeight") is double limit && height <= limit;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
