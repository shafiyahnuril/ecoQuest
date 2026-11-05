using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.ViewModels.Items;

public static class ActivityInputItemFactory
{
    public static ActivityInputItemViewModel Create(ActivityTypeDto type) => type.InputControlType switch
    {
        InputControlType.Numeric => new NumericItemViewModel(type),
        InputControlType.Count => new CountItemViewModel(type),
        InputControlType.TimeRange => new TimeRangeItemViewModel(type),
        InputControlType.Toggle => new ToggleItemViewModel(type),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type.InputControlType, null),
    };
}
