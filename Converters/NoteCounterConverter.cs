using System.Globalization;
using System.Windows.Data;

namespace EcoQuest.Converters;

/// <summary>"n/500" counter of the note box.</summary>
public sealed class NoteCounterConverter : IValueConverter
{
    public const int MaxLength = 500;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        $"{(value as string)?.Length ?? 0}/{MaxLength}";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
