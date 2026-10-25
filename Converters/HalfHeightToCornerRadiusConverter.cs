using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ecoQuest.Converters;

/// <summary>
/// Turns an element height into a fully rounded (pill) corner radius, i.e. height / 2.
/// CSS clamps border-radius to half the height; WPF does not, so a fixed radius larger than that gives elliptical ends.
/// </summary>
public sealed class HalfHeightToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var height = value is double d && !double.IsNaN(d) && !double.IsInfinity(d) ? d : 0;
        return new CornerRadius(height / 2);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
