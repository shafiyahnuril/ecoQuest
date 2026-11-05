using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.ViewModels.Items;

public sealed class NumericItemViewModel(ActivityTypeDto type) : QuantityItemViewModel(type);
