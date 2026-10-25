// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System;
using System.Collections.Generic;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Dtos;

public sealed record TrendPointDto(DateOnly Date, decimal Co2Kg, bool HasReview);   // hari tanpa review → 0, HasReview=false
public sealed record CategoryBreakdownDto(int CategoryId, string CategoryName, decimal Co2Kg, decimal Percentage, string Explanation);
public sealed record InsightDto(InsightKind Kind, string Text);

public sealed record LevelProgressDto(
    int LevelId, string LevelName, int MinPoints, int? MaxPoints, int CurrentPoints,
    string? NextLevelName, int? PointsToNext, double ProgressRatio);   // 0..1; 1 untuk level tertinggi

public sealed record DashboardDto(
    decimal TotalCo2Kg, string TotalCo2Explanation,     // FR-4.3 + FR-4.5
    int TotalPoints, int CurrentStreak, LevelProgressDto Level,
    TodayReviewState TodayState,
    TrendRange Range, IReadOnlyList<TrendPointDto> Trend,              // FR-4.1
    IReadOnlyList<CategoryBreakdownDto> Breakdown,                      // FR-4.2 (selalu 4 item)
    IReadOnlyList<InsightDto> Insights);                                // FR-4.4 (1–3 item)

public sealed record HistoryFilter(DateOnly? From, DateOnly? To, IReadOnlyList<int> CategoryIds);
public sealed record HistoryDayDto(Guid ReviewId, DateOnly ReviewDate, decimal TotalCo2Kg, int TotalPoints,
                                   IReadOnlyList<ActivityImpactDto> Items);   // Items boleh kosong (refleksi tanpa aktivitas)

public sealed record WeatherSuggestionDto(
    string City, DateOnly ForDate, bool IsFallback,       // IsFallback=true → saran generik (FR-6.3)
    string? ForecastSummary,                              // "Cerah berawan, 24–31°C"; null bila fallback
    int? AccuWeatherIconCode,
    string SuggestionText,                                // kalimat siap tampil
    int? SuggestedActivityTypeId, decimal? EstimatedCo2Kg);

public sealed record LeaderboardEntryDto(long Rank, string UserName, decimal TotalCo2Kg, string? LevelName, bool IsCurrentUser);
public sealed record ExportResultDto(string FilePath, int RowCount);
