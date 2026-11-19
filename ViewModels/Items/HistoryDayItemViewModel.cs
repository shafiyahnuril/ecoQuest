using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>One day card of the history list; also the data of the detail panel while selected.</summary>
public sealed class HistoryDayItemViewModel
{
    public HistoryDayItemViewModel(
        HistoryDayDto day, IReadOnlyDictionary<int, string> descriptions, IUiText text, Func<string?, string?> resolvePhoto)
    {
        ReviewId = day.ReviewId;
        DateText = UiFormat.LongDate(day.ReviewDate);
        TotalText = UiFormat.Co2(day.TotalCo2Kg);
        PointsText = string.Format(CultureInfo.CurrentCulture, text.Get("History.Points"), UiFormat.Count(day.TotalPoints));
        Lines = day.Items
            .Select(i => new HistoryLineItemViewModel(i, descriptions.GetValueOrDefault(i.ActivityTypeId), text, resolvePhoto))
            .ToList();
        EmptyText = text.Get("History.NoActivities");
        AutomationName = string.Format(CultureInfo.CurrentCulture, text.Get("History.DayAutomation"), DateText, TotalText);
    }

    public Guid ReviewId { get; }

    public string DateText { get; }

    public string TotalText { get; }

    public string PointsText { get; }

    public IReadOnlyList<HistoryLineItemViewModel> Lines { get; }

    public bool HasLines => Lines.Count > 0;

    public string EmptyText { get; }

    public string AutomationName { get; }
}
