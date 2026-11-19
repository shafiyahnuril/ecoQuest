using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public sealed class DistanceImpactCalculator : ImpactCalculatorBase
{
    public override InputControlType SupportedType => InputControlType.Numeric;
    public override decimal ResolveQuantity(ActivityInput i) => i.Quantity ?? 0m;
    protected override string FormatQuantity(decimal q, ActivityType t) => $"{q.ToString("0.00", Id)} km";

    public override IReadOnlyList<ValidationIssue> ValidateHard(ActivityInput i, ActivityType t)
    {
        var list = new List<ValidationIssue>();
        var q = i.Quantity ?? 0m;
        if (q < 0) list.Add(Error(t.ActivityTypeId, "Quantity", "Jarak tidak boleh negatif"));
        else if (decimal.Round(q, 2) != q) list.Add(Error(t.ActivityTypeId, "Quantity", "Maksimal 2 angka di belakang koma"));
        else if (t.ExceedsHard(q)) list.Add(Error(t.ActivityTypeId, "Quantity",
            $"Melebihi batas maksimum {t.HardMaxPerDay.ToString("N0", Id)} km per hari"));
        return list;
    }
}
