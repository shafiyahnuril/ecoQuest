using CommunityToolkit.Mvvm.ComponentModel;
using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.ViewModels.Items;

public sealed partial class TimeRangeItemViewModel(ActivityTypeDto type) : ActivityInputItemViewModel(type)
{
    [ObservableProperty] private TimeOnly? _startTime;
    [ObservableProperty] private TimeOnly? _endTime;

    public override bool IsFilled => StartTime is not null || EndTime is not null;

    protected override IReadOnlyCollection<string> ControlFields { get; } = ["StartTime", "EndTime", "TimeRange"];

    public override ActivityInput ToActivityInput() =>
        new(ActivityTypeId, null, StartTime, EndTime, null, Note, PhotoSourcePath);

    partial void OnStartTimeChanged(TimeOnly? value) => NotifyInputChanged();

    partial void OnEndTimeChanged(TimeOnly? value) => NotifyInputChanged();
}
