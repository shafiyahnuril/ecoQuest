using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>The quantity as the user entered it ("3,00 km", "21:00–23:30 (2 j 30 m)", "2 kali", "Ya"), built from an impact the service returned.</summary>
public static class ActivityQuantityText
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public static string Of(ActivityImpactDto item, IUiText text) => item.InputControlType switch
    {
        InputControlType.TimeRange => TimeRange(item, text),
        InputControlType.Toggle => text.Get("Switch.Yes"),
        InputControlType.Count => $"{UiFormat.Count((int)item.Quantity)} {item.UnitLabel}",
        _ => $"{item.Quantity.ToString("N2", Id)} {item.UnitLabel}",
    };

    private static string TimeRange(ActivityImpactDto item, IUiText text)
    {
        if (item.StartTime is not { } start || item.EndTime is not { } end)
        {
            return string.Empty;
        }

        var range = $"{start:HH:mm}–{end:HH:mm}";
        var duration = end - start;
        return duration == TimeSpan.Zero ? range : $"{range} ({Duration(duration, text)})";
    }

    private static string Duration(TimeSpan duration, IUiText text) => (duration.Hours, duration.Minutes) switch
    {
        (var hours, 0) => string.Format(CultureInfo.CurrentCulture, text.Get("Summary.DurationHours"), hours),
        (0, var minutes) => string.Format(CultureInfo.CurrentCulture, text.Get("Summary.DurationMinutes"), minutes),
        var (hours, minutes) => string.Format(CultureInfo.CurrentCulture, text.Get("Summary.Duration"), hours, minutes),
    };
}
