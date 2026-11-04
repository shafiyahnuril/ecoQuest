using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>
/// CSS <c>clamp(min, vh × height, max)</c> of the bound height (the root element's <c>ActualHeight</c>, i.e. the client area, like a CSS viewport).
/// <c>ConverterParameter</c> is a token prefix; the numbers live in Spacing.xaml as <c>{prefix}.Min</c>, <c>.Max</c> and <c>.Vh</c>.
/// Returns a double, or a bottom-only <see cref="Thickness"/> when the target property is a margin.
/// </summary>
public sealed class VhClampConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double height || parameter is not string prefix)
        {
            return Binding.DoNothing;
        }

        var result = Clamp(height, Token(prefix + ".Min"), Token(prefix + ".Max"), Token(prefix + ".Vh"));
        return targetType == typeof(Thickness) ? new Thickness(0, 0, 0, result) : result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>max(min, min(vh × height, max)); <paramref name="vh"/> is a fraction (0.047 = 4.7vh).</summary>
    public static double Clamp(double height, double min, double max, double vh) => Math.Max(min, Math.Min(vh * height, max));

    private static double Token(string key) => (double)Application.Current.FindResource(key);
}
