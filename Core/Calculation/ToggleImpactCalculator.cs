using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public sealed class ToggleImpactCalculator : ImpactCalculatorBase
{
    public override InputControlType SupportedType => InputControlType.Toggle;
    public override decimal ResolveQuantity(ActivityInput i) => i.IsYes == true ? 1m : 0m;
    protected override string FormatQuantity(decimal q, ActivityType t) => q >= 1 ? "Ya (1 kali)" : "Tidak";
    public override IReadOnlyList<ValidationIssue> ValidateHard(ActivityInput i, ActivityType t) =>
        i.Quantity is { } q && q is not (0m or 1m)
            ? [Error(t.ActivityTypeId, "IsYes", "Pilih Ya atau Tidak")]
            : [];
}
