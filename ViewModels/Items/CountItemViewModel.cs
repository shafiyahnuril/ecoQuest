using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.ViewModels.Items;

public sealed class CountItemViewModel(ActivityTypeDto type) : QuantityItemViewModel(type);
