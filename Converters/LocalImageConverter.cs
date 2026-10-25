using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace EcoQuest.Converters;

/// <summary>
/// Local file path -> frozen BitmapImage, or null when the path is empty or the file is missing/unreadable (the view then
/// falls back to the initial letter). Uses OnLoad so the file is not locked, and DecodePixelWidth (ConverterParameter,
/// default 96) so memory stays low (UIR-24).
/// </summary>
public sealed class LocalImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var width = int.TryParse(parameter as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) ? w : 96;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = width;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
