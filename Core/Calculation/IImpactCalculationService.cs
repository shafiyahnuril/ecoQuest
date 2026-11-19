using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public interface IImpactCalculationService
{
    ImpactResult Calculate(ActivityInput input, ActivityType type);
}
