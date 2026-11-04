using System.Globalization;
using System.Text.RegularExpressions;

namespace EcoQuest.Controls;

/// <summary>
/// Text rules of <see cref="NumericStepper"/>, free of WPF types for unit tests. "." and "," are both decimal separators and there is
/// no thousands separator: "3.500" is 3,5, never 3500. Range checks are not done here; the service rejects out-of-range values.
/// </summary>
public static partial class NumericInputParser
{
    private const decimal DecimalStep = 0.5m;
    private const decimal IntegerStep = 1m;
    private const int DecimalPlaces = 2;

    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    // [0-9], not \d: \d also matches non-ASCII digits that decimal.Parse would reject.
    [GeneratedRegex(@"^-?([0-9]+[.,]?[0-9]*|[.,][0-9]+)$")]
    private static partial Regex NumberPattern();

    public static bool TryParse(string? text, bool isInteger, out decimal? value)
    {
        value = null;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        if (!NumberPattern().IsMatch(trimmed))
        {
            return false;
        }

        var parsed = decimal.Parse(trimmed.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        value = Normalize(parsed, isInteger);
        return true;
    }

    public static string Format(decimal? value, bool isInteger)
    {
        if (value is not { } number)
        {
            return string.Empty;
        }

        return isInteger
            ? number.ToString("0.####", Id)
            : Normalize(number, isInteger: false).ToString("F" + DecimalPlaces, Id);
    }

    public static decimal? Step(decimal? current, int direction, bool isInteger)
    {
        var from = current ?? 0m;
        var next = from + direction * (isInteger ? IntegerStep : DecimalStep);
        if (direction < 0)
        {
            next = Math.Max(next, Math.Min(from, 0m));
        }

        return Normalize(next, isInteger);
    }

    private static decimal Normalize(decimal value, bool isInteger) =>
        isInteger ? value : Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
}
