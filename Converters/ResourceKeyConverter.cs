using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>
/// Turns a resource key held by a ViewModel (e.g. "Icon.Home", "Nav.Dashboard") into the resource itself,
/// so ViewModels carry keys but never System.Windows types.
/// </summary>
public sealed class ResourceKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? Application.Current.TryFindResource(key) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
