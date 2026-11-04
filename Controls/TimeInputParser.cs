using System.Globalization;
using System.Text.RegularExpressions;

namespace EcoQuest.Controls;

/// <summary>Text rules of <see cref="TimeRangePicker"/>, free of WPF types for unit tests. Times snap to <see cref="SlotMinutes"/>.</summary>
public static partial class TimeInputParser
{
    public const int SlotMinutes = 5;

    [GeneratedRegex(@"^(?<h>[0-9]{1,2}):(?<m>[0-9]{2})$|^(?<h>[0-9]{1,2})(?<m>[0-9]{2})$")]
    private static partial Regex TimePattern();

    public static bool TryParse(string? text, out TimeOnly? value)
    {
        value = null;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        var match = TimePattern().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        if (hour > 23 || minute > 59)
        {
            return false;
        }

        value = SnapToSlot(new TimeOnly(hour, minute));
        return true;
    }

    public static string Format(TimeOnly? value) =>
        value is { } time ? time.ToString("HH':'mm", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Moves one slot; wraps past midnight. An empty value stays empty.</summary>
    public static TimeOnly? Step(TimeOnly? current, int direction) =>
        current?.AddMinutes(direction * SlotMinutes);

    private static TimeOnly SnapToSlot(TimeOnly time)
    {
        var slots = (int)Math.Round(time.Minute / (double)SlotMinutes, MidpointRounding.AwayFromZero);
        return new TimeOnly(time.Hour, 0).AddMinutes(slots * SlotMinutes);
    }
}
