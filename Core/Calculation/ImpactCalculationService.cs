using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public sealed class ImpactCalculationService(IImpactCalculatorFactory factory) : IImpactCalculationService
{
    public ImpactResult Calculate(ActivityInput input, ActivityType type)
    {
        var calc = factory.For(type.InputControlType);
        var qty  = calc.ResolveQuantity(input);
        var co2  = calc.CalculateCo2(qty, type.Co2Coefficient);
        var pts  = calc.CalculatePoints(co2, type.PointFactor);
        var (s, e) = type.InputControlType == InputControlType.TimeRange ? (input.StartTime, input.EndTime) : (null, null);
        return new ImpactResult(type.ActivityTypeId, qty, s, e, co2, pts, type.Co2Coefficient, type.PointFactor);
    }
}
