using CommunityToolkit.Mvvm.ComponentModel;
using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.ViewModels.Items;

public sealed partial class ToggleItemViewModel(ActivityTypeDto type) : ActivityInputItemViewModel(type)
{
    [ObservableProperty] private bool? _isYes;

    public override bool IsFilled => IsYes == true;

    protected override IReadOnlyCollection<string> ControlFields { get; } = ["IsYes"];

    public override ActivityInput ToActivityInput() =>
        new(ActivityTypeId, null, null, null, IsYes, Note, PhotoSourcePath);

    partial void OnIsYesChanged(bool? value) => NotifyInputChanged();
}
