using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EcoQuest.Converters;

/// <summary>CategoryId 1-4 to the category colour token (Cat.Transportasi / Energi / Sampah / Konsumsi). Always shown next to the category name.</summary>
public sealed class CategoryIdToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is int id ? id switch
        {
            1 => "Cat.Transportasi",
            2 => "Cat.Energi",
            3 => "Cat.Sampah",
            4 => "Cat.Konsumsi",
            _ => null,
        } : null;

        return key is not null && Application.Current?.TryFindResource(key) is Brush brush ? brush : Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
