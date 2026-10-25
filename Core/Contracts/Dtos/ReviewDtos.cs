// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System;
using System.Collections.Generic;
using System.Linq;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Dtos;

/// Input mentah dari satu baris form. Hanya field yang relevan untuk tipe tsb yang diisi.
public sealed record ActivityInput(
    int ActivityTypeId,
    decimal? Quantity,        // Numeric (km) & Count (kali). null/0 = tidak dilakukan
    TimeOnly? StartTime,      // TimeRange
    TimeOnly? EndTime,        // TimeRange (boleh < StartTime → lintas tengah malam)
    bool? IsYes,              // Toggle. null/false = tidak
    string? Note,             // ≤ 500 karakter
    string? PhotoSourcePath)  // path file asli yang dipilih pengguna (akan disalin oleh PhotoStorageService)
{
    public static ActivityInput Empty(int activityTypeId) => new(activityTypeId, null, null, null, null, null, null);
}

public sealed record SubmitReviewRequest(IReadOnlyList<ActivityInput> Items, bool WarningsAcknowledged);

public sealed record ValidationIssue(int ActivityTypeId, string Field, ValidationSeverity Severity, string Message);
// Field ∈ { "Quantity", "StartTime", "EndTime", "TimeRange", "IsYes", "Note", "Photo" }

public sealed record ReviewValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool HasErrors   => Issues.Any(i => i.Severity == ValidationSeverity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);
    public bool IsEmpty     { get; init; }   // true bila tidak ada aktivitas yang diisi sama sekali
}

/// Hasil kalkulasi satu aktivitas — dipakai untuk live preview, ringkasan, riwayat, dan tooltip (FR-4.5).
public sealed record ActivityImpactDto(
    Guid? LogId, int ActivityTypeId, string TypeName, int CategoryId, string CategoryName,
    InputControlType InputControlType, decimal Quantity, string UnitLabel,
    TimeOnly? StartTime, TimeOnly? EndTime,
    decimal Co2SavedKg, int PointsEarned,
    decimal Co2CoefficientUsed, decimal PointFactorUsed,
    string FormulaExplanation,          // contoh: "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂"
    string PointsExplanation,           // contoh: "0,45 × 20 × 1,0 = 9 poin"
    string? Note, string? PhotoPath);   // PhotoPath relatif (ADR-011)

public sealed record LevelChangeDto(string OldLevelName, string NewLevelName);
public sealed record BadgeDto(int BadgeId, string Name, string Description, CriteriaType CriteriaType,
                              decimal CriteriaValue, bool IsEarned, DateTimeOffset? EarnedAt,
                              decimal CurrentValue);   // progres saat ini (mis. streak 4 dari 7)

public sealed record ReviewSummaryDto(
    Guid ReviewId, DateOnly ReviewDate, DateTimeOffset SubmittedAt,
    decimal TotalCo2SavedKg, int TotalPointsEarned,
    IReadOnlyList<ActivityImpactDto> Items,          // hanya aktivitas yang dilakukan
    string Insight,                                  // satu kalimat edukatif (Skema v2 §6.2)
    int CurrentStreak,
    LevelChangeDto? LevelUp,                         // null bila tidak naik level
    IReadOnlyList<BadgeDto> NewBadges);              // kosong bila tidak ada

public enum TodayReviewState { NotSubmitted, Submitted }
public sealed record TodayReviewDto(TodayReviewState State, DateOnly Today, ReviewSummaryDto? Summary);
