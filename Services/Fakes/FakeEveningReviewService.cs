using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;

namespace EcoQuest.Services.Fakes;

/// <summary>
/// Validation and preview follow SRS 4.3/4.4. TEMP until BE-09/BE-10: validation rules live here until
/// ActivityInputValidator exists in Core/Validation.
/// Errors scenario: first SubmitAsync = NetworkError (inputs must be kept), later ones = ReviewAlreadySubmitted
/// (the review is stored, so GetTodayAsync then returns the read-only summary).
/// </summary>
public sealed class FakeEveningReviewService : IEveningReviewService
{
    private const int MaxNoteLength = 500;
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png"];

    private readonly FakeContext _ctx;
    private readonly FakeStore _store;

    public FakeEveningReviewService(FakeContext ctx, FakeStore store)
    {
        _ctx = ctx; _store = store;
    }

    public async Task<Result<TodayReviewDto>> GetTodayAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("today")) return _ctx.NetworkFailure<TodayReviewDto>();

        var summary = _store.TodaySummary;
        return Result<TodayReviewDto>.Success(new TodayReviewDto(
            summary is null ? TodayReviewState.NotSubmitted : TodayReviewState.Submitted, _ctx.Clock.Today, summary));
    }

    public ReviewValidationResult Validate(IReadOnlyList<ActivityInput> items, IReadOnlyList<ActivityTypeDto> types) =>
        ValidateItems(items, types);

    public ActivityImpactDto? Preview(ActivityInput input, ActivityTypeDto type)
    {
        if (!FakeImpact.IsFilled(input, type)) return null;
        if (ValidateItems([input], [type]).HasErrors) return null;
        return FakeImpact.Build(input, type);
    }

    public async Task<Result<ReviewSummaryDto>> SubmitAsync(SubmitReviewRequest request, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);

        if (_store.TodaySummary is not null)
            return Result<ReviewSummaryDto>.Failure(ErrorCode.ReviewAlreadySubmitted, FakeMessages.ReviewAlreadySubmitted);

        var validation = ValidateItems(request.Items, FakeData.ActivityTypes);
        if (validation.HasErrors)
            return Result<ReviewSummaryDto>.Failure(ErrorCode.ValidationFailed, FakeMessages.ValidationFailed,
                FieldErrorsOf(validation));
        if (validation.HasWarnings && !request.WarningsAcknowledged)
            return Result<ReviewSummaryDto>.Failure(ErrorCode.WarningsNotAcknowledged, FakeMessages.WarningsNotAcknowledged);

        var impacts = request.Items
            .Select(i => (Input: i, Type: FakeData.ActivityTypes.FirstOrDefault(t => t.ActivityTypeId == i.ActivityTypeId)))
            .Where(x => x.Type is not null && FakeImpact.IsFilled(x.Input, x.Type))
            .Select(x => FakeImpact.Build(x.Input, x.Type!, Guid.NewGuid()))
            .ToList();

        if (_ctx.Scenario == FakeScenario.Errors)
        {
            if (_ctx.FailOnce("submit")) return _ctx.NetworkFailure<ReviewSummaryDto>();

            _store.SubmitToday(impacts);   // "another device already saved it"
            return Result<ReviewSummaryDto>.Failure(ErrorCode.ReviewAlreadySubmitted, FakeMessages.ReviewAlreadySubmitted);
        }

        return Result<ReviewSummaryDto>.Success(_store.SubmitToday(impacts));
    }

    /// <summary>Pure validation of SRS 4.3; also used directly by tests.</summary>
    public static ReviewValidationResult ValidateItems(IReadOnlyList<ActivityInput> items, IReadOnlyList<ActivityTypeDto> types)
    {
        var issues = new List<ValidationIssue>();
        var anyFilled = false;

        foreach (var item in items)
        {
            var type = types.FirstOrDefault(t => t.ActivityTypeId == item.ActivityTypeId);
            if (type is null) continue;

            var filled = FakeImpact.IsFilled(item, type);
            anyFilled |= filled;

            if (item.Note is { Length: > MaxNoteLength })
                issues.Add(Error(type, "Note", "Catatan maksimal 500 karakter"));

            if (!filled)
            {
                if (!string.IsNullOrWhiteSpace(item.Note))
                    issues.Add(Error(type, "Note", "Isi aktivitas terlebih dahulu sebelum menambah catatan/foto"));
                if (!string.IsNullOrEmpty(item.PhotoSourcePath))
                    issues.Add(Error(type, "Photo", "Isi aktivitas terlebih dahulu sebelum menambah catatan/foto"));
                continue;
            }

            switch (type.InputControlType)
            {
                case InputControlType.Numeric:
                case InputControlType.Count:
                    ValidateQuantity(item, type, issues);
                    break;
                case InputControlType.TimeRange:
                    ValidateTimeRange(item, type, issues);
                    break;
            }

            if (!string.IsNullOrEmpty(item.PhotoSourcePath)) ValidatePhoto(item, type, issues);
        }

        return new ReviewValidationResult(issues) { IsEmpty = !anyFilled };
    }

    private static void ValidateQuantity(ActivityInput item, ActivityTypeDto type, List<ValidationIssue> issues)
    {
        var q = item.Quantity ?? 0m;
        if (q < 0) { issues.Add(Error(type, "Quantity", "Nilai tidak boleh negatif")); return; }

        if (type.InputControlType == InputControlType.Count && q != decimal.Truncate(q))
        { issues.Add(Error(type, "Quantity", "Isi dengan bilangan bulat")); return; }
        if (type.InputControlType == InputControlType.Numeric && q != Math.Round(q, 2))
        { issues.Add(Error(type, "Quantity", "Maksimal 2 angka desimal")); return; }

        if (q > type.HardMaxPerDay)
            issues.Add(Error(type, "Quantity", $"Maksimal {FakeImpact.Num(type.HardMaxPerDay, "0.##")} {type.UnitLabel} per hari"));
        else if (type.PlausibleMaxPerDay is { } plausible && q > plausible)
            issues.Add(Warning(type, "Quantity",
                $"{FakeImpact.Num(q, "0.##")} {type.UnitLabel} terlihat tidak biasa untuk satu hari. Pastikan nilainya benar"));
    }

    private static void ValidateTimeRange(ActivityInput item, ActivityTypeDto type, List<ValidationIssue> issues)
    {
        if (item.StartTime is null) { issues.Add(Error(type, "StartTime", "Isi jam mulai")); return; }
        if (item.EndTime is null) { issues.Add(Error(type, "EndTime", "Isi jam selesai")); return; }
        if (item.StartTime == item.EndTime) { issues.Add(Error(type, "TimeRange", "Rentang jam tidak boleh 0")); return; }

        var hours = FakeImpact.DurationHours(item.StartTime.Value, item.EndTime.Value);
        if (hours > type.HardMaxPerDay)
            issues.Add(Error(type, "TimeRange", $"Maksimal {FakeImpact.Num(type.HardMaxPerDay, "0.##")} jam per hari"));
        else if (type.PlausibleMaxPerDay is { } plausible && hours > plausible)
            issues.Add(Warning(type, "TimeRange",
                $"Durasi lebih dari {FakeImpact.Num(plausible, "0.##")} jam terlihat tidak biasa. Pastikan sudah benar"));
    }

    private static void ValidatePhoto(ActivityInput item, ActivityTypeDto type, List<ValidationIssue> issues)
    {
        var path = item.PhotoSourcePath!;
        if (!PhotoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            issues.Add(Error(type, "Photo", "Foto harus berformat JPG atau PNG"));
        else if (File.Exists(path) && new FileInfo(path).Length > MaxPhotoBytes)
            issues.Add(Error(type, "Photo", "Ukuran foto maksimal 5 MB"));
    }

    private static IReadOnlyDictionary<string, string> FieldErrorsOf(ReviewValidationResult validation) =>
        validation.Issues.Where(i => i.Severity == ValidationSeverity.Error)
                         .GroupBy(i => $"{i.ActivityTypeId}.{i.Field}")
                         .ToDictionary(g => g.Key, g => g.First().Message);

    private static ValidationIssue Error(ActivityTypeDto t, string field, string message) =>
        new(t.ActivityTypeId, field, ValidationSeverity.Error, message);

    private static ValidationIssue Warning(ActivityTypeDto t, string field, string message) =>
        new(t.ActivityTypeId, field, ValidationSeverity.Warning, message);
}
