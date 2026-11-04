using CommunityToolkit.Mvvm.ComponentModel;
using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.ViewModels.Items;

public abstract partial class QuantityItemViewModel : ActivityInputItemViewModel
{
    [ObservableProperty] private decimal? _quantity;

    protected QuantityItemViewModel(ActivityTypeDto type) : base(type)
    {
    }

    public override bool IsFilled => Quantity is { } quantity && quantity != 0m;

    protected override IReadOnlyCollection<string> ControlFields { get; } = ["Quantity"];

    public override ActivityInput ToActivityInput() =>
        new(ActivityTypeId, Quantity, null, null, null, Note, PhotoSourcePath);

    partial void OnQuantityChanged(decimal? value) => NotifyInputChanged();
}
