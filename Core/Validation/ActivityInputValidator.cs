using EcoQuest.Core.Calculation;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;


namespace EcoQuest.Core.Validation;

public sealed class ActivityInputValidator(IImpactCalculatorFactory factory)
{
    public const int NoteMaxLength = 500;
    private static readonly System.Globalization.CultureInfo Id = new("id-ID");

    public ReviewValidationResult Validate(IReadOnlyList<ActivityInput> items, IReadOnlyList<ActivityType> types)
    {
        var issues = new List<ValidationIssue>();
        var anyFilled = false;
        foreach (var item in items)
        {
            var type = types.SingleOrDefault(t => t.ActivityTypeId == item.ActivityTypeId)
                       ?? throw new ArgumentException($"ActivityTypeId {item.ActivityTypeId} tidak dikenal");
            var calc = factory.For(type.InputControlType);
            var hard = calc.ValidateHard(item, type);
            issues.AddRange(hard);
            var qty = hard.Count == 0 ? calc.ResolveQuantity(item) : 0m;
            var filled = qty > 0;
            anyFilled |= filled;

            if (!filled && (!string.IsNullOrWhiteSpace(item.Note) || !string.IsNullOrEmpty(item.PhotoSourcePath)) && hard.Count == 0)
                issues.Add(new(type.ActivityTypeId, "Quantity", ValidationSeverity.Error,
                    "Isi aktivitas terlebih dahulu sebelum menambah catatan/foto"));
            if (item.Note is { Length: > NoteMaxLength })
                issues.Add(new(type.ActivityTypeId, "Note", ValidationSeverity.Error, $"Catatan maksimal {NoteMaxLength} karakter"));
            if (filled && type.ExceedsPlausible(qty))
                issues.Add(new(type.ActivityTypeId, "Quantity", ValidationSeverity.Warning,
                    $"Kamu menginput {qty.ToString("0.##", Id)} {type.UnitLabel} untuk {type.TypeName} hari ini — apakah ini benar?"));
        }
        return new ReviewValidationResult(issues) { IsEmpty = !anyFilled };
    }
}