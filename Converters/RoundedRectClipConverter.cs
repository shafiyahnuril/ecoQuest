using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EcoQuest.Converters;

/// <summary>
/// (width, height) to a rounded-rectangle clip whose corner radius is the converter parameter (invariant number, e.g. "17").
/// Border.CornerRadius does not clip its children in WPF, so a hero that must hide an overflowing illustration clips its content with this.
/// </summary>
public sealed class RoundedRectClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double width || values[1] is not double height
            || double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0
            || !double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius))
        {
            return Geometry.Empty;
        }

        var clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        clip.Freeze();
        return clip;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
