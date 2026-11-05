namespace EcoQuest.ViewModels.Items;

/// <summary>One category header with its activity rows; the dot colour comes from <see cref="CategoryId"/> (CategoryIdToBrushConverter).</summary>
public sealed record ActivityGroupViewModel(int CategoryId, string Name, IReadOnlyList<ActivityInputItemViewModel> Items);
