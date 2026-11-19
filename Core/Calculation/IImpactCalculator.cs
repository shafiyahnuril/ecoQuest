using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public interface IImpactCalculator
{
    InputControlType SupportedType { get; }
    decimal ResolveQuantity(ActivityInput input);                                    // unit kanonik
    IReadOnlyList<ValidationIssue> ValidateHard(ActivityInput input, ActivityType type);
    decimal CalculateCo2(decimal quantity, decimal co2Coefficient);                   // dibulatkan 4 desimal AwayFromZero
    int CalculatePoints(decimal co2SavedKg, decimal pointFactor);                     // round(co2 x 20 x pf), AwayFromZero
    string DescribeFormula(decimal quantity, ActivityType type);
}
