using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Data.Errors;

namespace EcoQuest.Services;

/// <summary>
/// Layanan agregasi Dashboard: hero CO2, tren 7/30 hari dengan pengisian celah tanggal kosong, breakdown 4 kategori, dan insight (SDD §6.6).
/// </summary>
public sealed class DashboardService(
    ISessionService session,
    IClock clock,
    IUserRepository users,
    IDailyReviewRepository reviews,
    ILevelRepository levelsRepo,
    IActivityTypeRepository typesRepo) : IDashboardService
{
    private static readonly CultureInfo IdCulture = new("id-ID");

    public async Task<Result<DashboardDto>> GetDashboardAsync(TrendRange range, CancellationToken ct = default)
    {
        var user = session.RequireUser();
        var today = clock.Today;
        var span = range == TrendRange.Weekly ? 7 : 30;
        var from = today.AddDays(-(span - 1));

        try
        {
            var profileT = users.GetProfileAsync(user.UserId, ct);
            var totalsT = reviews.GetDailyTotalsAsync(user.UserId, from, today, ct);
            var catsT = reviews.GetCategoryBreakdownAsync(user.UserId, from, today, ct);
            var prevT = reviews.GetDailyTotalsAsync(user.UserId, from.AddDays(-7), from.AddDays(-1), ct);
            var levelsT = levelsRepo.GetAllAsync(ct);
            var typesT = typesRepo.GetAllAsync(ct);

            await Task.WhenAll(profileT, totalsT, catsT, prevT, levelsT, typesT);

            var profile = await profileT;
            if (profile is null)
                return Result<DashboardDto>.Failure(ErrorCode.NotFound, Messages.Unknown);

            var totals = await totalsT;
            var cats = await catsT;
            var prev = await prevT;
            var levels = await levelsT;
            var activityTypes = await typesT;

            var byDate = totals.ToDictionary(t => t.ReviewDate);
            var trend = Enumerable.Range(0, span)
                .Select(i => from.AddDays(i))
                .Select(d => byDate.TryGetValue(d, out var t)
                    ? new TrendPointDto(d, t.TotalCo2SavedKg, HasReview: true)
                    : new TrendPointDto(d, 0m, HasReview: false))
                .ToList();

            var periodTotal = cats.Sum(c => c.TotalCo2SavedKg);
            var breakdown = cats
                .Select(c =>
                {
                    var pct = periodTotal == 0m
                        ? 0m
                        : Math.Round(c.TotalCo2SavedKg / periodTotal * 100m, 1, MidpointRounding.AwayFromZero);
                    return new CategoryBreakdownDto(
                        c.CategoryId,
                        c.CategoryName,
                        c.TotalCo2SavedKg,
                        pct,
                        $"{c.CategoryName}: {c.TotalCo2SavedKg.ToString("0.##", IdCulture)} kg CO₂ pada periode ini");
                })
                .ToList();

            var explanation = profile.TotalReviewCount == 0
                ? "Belum ada data. Total dihitung dari kuantitas aktivitas × koefisien emisi (estimasi indikatif)."
                : $"Total dari {profile.TotalReviewCount} refleksi: jumlah kuantitas × koefisien emisi tiap aktivitas (estimasi indikatif).";

            var effectiveStreak = StreakCalculator.Effective(
                profile.User.CurrentStreak,
                profile.LastReviewDate,
                today);
            var levelProgress = LevelProgressFactory.ToDto(
                LevelResolver.Progress(profile.User.TotalPoints, levels));
            var todayState = profile.LastReviewDate == today
                ? TodayReviewState.Submitted
                : TodayReviewState.NotSubmitted;

            var periodLabel = range == TrendRange.Weekly ? "7 hari terakhir" : "30 hari terakhir";
            var previousTotal = prev.Sum(p => p.TotalCo2SavedKg);
            var insights = BuildInsights(
                profile.TotalReviewCount,
                periodTotal,
                previousTotal,
                periodLabel,
                range,
                breakdown,
                activityTypes,
                effectiveStreak);

            return Result<DashboardDto>.Success(new DashboardDto(
                profile.User.TotalCo2SavedKg,
                explanation,
                profile.User.TotalPoints,
                effectiveStreak,
                levelProgress,
                todayState,
                range,
                trend,
                breakdown,
                insights,
                profile.TotalReviewCount));
        }
        catch (DataAccessException ex)
        {
            return Result<DashboardDto>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    private static IReadOnlyList<InsightDto> BuildInsights(
        int totalReviewCount,
        decimal periodTotal,
        decimal previousTotal,
        string periodLabel,
        TrendRange range,
        IReadOnlyList<CategoryBreakdownDto> breakdown,
        IReadOnlyList<ActivityType> activityTypes,
        int effectiveStreak)
    {
        if (totalReviewCount == 0 || periodTotal <= 0m)
            return [InsightGenerator.EmptyPeriod()];

        var candidates = new List<InsightDto?>
        {
            InsightGenerator.TopCategory(breakdown, periodLabel),
            range == TrendRange.Weekly ? InsightGenerator.WeekOverWeek(periodTotal, previousTotal) : null,
            InsightGenerator.Equivalence(periodTotal),
            BuildWeakestCategoryInsight(breakdown, activityTypes),
            InsightGenerator.Streak(effectiveStreak)
        };

        return candidates
            .Where(i => i is not null)
            .Cast<InsightDto>()
            .Take(3)
            .ToList();
    }

    private static InsightDto? BuildWeakestCategoryInsight(
        IReadOnlyList<CategoryBreakdownDto> breakdown,
        IReadOnlyList<ActivityType> activityTypes)
    {
        var weakest = breakdown
            .Where(c => c.Co2Kg == 0m)
            .OrderBy(c => c.CategoryId)
            .FirstOrDefault();

        if (weakest is null)
            return null;

        var sampleType = activityTypes
            .Where(t => t.CategoryId == weakest.CategoryId)
            .OrderBy(t => t.DisplayOrder)
            .FirstOrDefault();

        if (sampleType is null)
            return null;

        var estimatedCo2 = Math.Round(1m * sampleType.Co2Coefficient, 4, MidpointRounding.AwayFromZero);
        var suggestion = $"{sampleType.TypeName} (1 {sampleType.UnitLabel})";
        return InsightGenerator.WeakestCategory(breakdown, suggestion, estimatedCo2);
    }
}
