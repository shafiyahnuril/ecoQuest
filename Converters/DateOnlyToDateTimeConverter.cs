using System.Globalization;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>DatePicker binds DateTime?; ViewModels hold DateOnly? (Asia/Jakarta calendar date, no time zone).</summary>
public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : null;
}
