using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ecoQuest.Converters;

/// <summary>
/// Builds a capsule clip (radius = min(width, height) / 2) from (width, height) so that filled bars keep
/// identical, round ends at any width, including very small values and 0.
/// </summary>
public sealed class RoundedClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double width || values[1] is not double height
            || double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return Geometry.Empty;
        }

        var radius = Math.Min(width, height) / 2;
        var clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        clip.Freeze();
        return clip;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
