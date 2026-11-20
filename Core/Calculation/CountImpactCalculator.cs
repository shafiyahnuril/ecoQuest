using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public sealed class CountImpactCalculator : ImpactCalculatorBase
{
    public override InputControlType SupportedType => InputControlType.Count;
    public override decimal ResolveQuantity(ActivityInput i) => i.Quantity ?? 0m;
    protected override string FormatQuantity(decimal q, ActivityType t) => $"{q.ToString("0", Id)} kali";

    public override IReadOnlyList<ValidationIssue> ValidateHard(ActivityInput i, ActivityType t)
    {
        var list = new List<ValidationIssue>();
        var q = i.Quantity ?? 0m;
        if (q < 0) list.Add(Error(t.ActivityTypeId, "Quantity", "Jumlah tidak boleh negatif"));
        else if (decimal.Truncate(q) != q) list.Add(Error(t.ActivityTypeId, "Quantity", "Masukkan bilangan bulat"));
        else if (t.ExceedsHard(q)) list.Add(Error(t.ActivityTypeId, "Quantity",
            $"Melebihi batas maksimum {t.HardMaxPerDay.ToString("N0", Id)} kali per hari"));
        return list;
    }
}
