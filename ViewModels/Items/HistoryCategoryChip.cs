using CommunityToolkit.Mvvm.ComponentModel;

namespace EcoQuest.ViewModels.Items;

public sealed partial class HistoryCategoryChip(int categoryId, string name, Action selectionChanged) : ObservableObject
{
    public int CategoryId { get; } = categoryId;

    public string Name { get; } = name;

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => selectionChanged();
}
