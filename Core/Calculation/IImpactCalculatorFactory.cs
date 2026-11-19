using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public interface IImpactCalculatorFactory
{
    IImpactCalculator For(InputControlType type);
}
