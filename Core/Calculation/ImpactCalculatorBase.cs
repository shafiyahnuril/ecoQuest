using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public abstract class ImpactCalculatorBase : IImpactCalculator
{
    protected static readonly System.Globalization.CultureInfo Id = new("id-ID");

    public abstract InputControlType SupportedType { get; }
    public abstract decimal ResolveQuantity(ActivityInput input);
    public abstract IReadOnlyList<ValidationIssue> ValidateHard(ActivityInput input, ActivityType type);
    protected abstract string FormatQuantity(decimal quantity, ActivityType type);

    public decimal CalculateCo2(decimal quantity, decimal co2Coefficient)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        return Math.Round(quantity * co2Coefficient, ImpactConstants.Co2Decimals, MidpointRounding.AwayFromZero);
    }

    public int CalculatePoints(decimal co2SavedKg, decimal pointFactor) =>
        (int)Math.Round(co2SavedKg * ImpactConstants.PointsPerKg * pointFactor, 0, MidpointRounding.AwayFromZero);

    public string DescribeFormula(decimal quantity, ActivityType type)
    {
        var co2 = CalculateCo2(quantity, type.Co2Coefficient);
        return $"{FormatQuantity(quantity, type)} × {type.Co2Coefficient.ToString("0.###", Id)} kg CO₂/{UnitSingular(type)} = {co2.ToString("0.####", Id)} kg CO₂";
    }

    public string DescribePoints(decimal co2, ActivityType type) =>
        $"{co2.ToString("0.####", Id)} × 20 × {type.PointFactor.ToString("0.0", Id)} = {CalculatePoints(co2, type.PointFactor)} poin";

    protected virtual string UnitSingular(ActivityType t) => t.UnitLabel == "kali" ? "kejadian" : t.UnitLabel;

    protected static ValidationIssue Error(int typeId, string field, string msg) => new(typeId, field, ValidationSeverity.Error, msg);
}