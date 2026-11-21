
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Data.Errors;

namespace EcoQuest.Services;

/// <summary>
/// Layanan agregasi Dashboard:
/// hero CO2, tren 7/30 hari, breakdown kategori,
/// dan insight deterministik (BE-17b).
/// </summary>
public sealed class DashboardService(
    ISessionService session,
    IClock clock,
    IUserRepository users,
    IDailyReviewRepository reviews,
    ILevelRepository levelsRepo,
    IActivityTypeRepository typesRepo,
    IImpactCalculationService impactCalculation) : IDashboardService
{
    private static readonly CultureInfo IdCulture = new("id-ID");

    public async Task<Result<DashboardDto>> GetDashboardAsync(
        TrendRange range,
        CancellationToken ct = default)
    {
        var user = session.RequireUser();
        var today = clock.Today;
        var span = range == TrendRange.Weekly ? 7 : 30;
        var from = today.AddDays(-(span - 1));

        try
        {
            var profileT = users.GetProfileAsync(user.UserId, ct);
            var totalsT = reviews.GetDailyTotalsAsync(
                user.UserId, from, today, ct);
            var catsT = reviews.GetCategoryBreakdownAsync(
                user.UserId, from, today, ct);
            var prevT = reviews.GetDailyTotalsAsync(
                user.UserId,
                from.AddDays(-7),
                from.AddDays(-1),
                ct);
            var levelsT = levelsRepo.GetAllAsync(ct);
            var typesT = typesRepo.GetAllAsync(ct);

            await Task.WhenAll(
                profileT,
                totalsT,
                catsT,
                prevT,
                levelsT,
                typesT);

            var profile = await profileT;

            if (profile is null)
            {
                return Result<DashboardDto>.Failure(
                    ErrorCode.NotFound,
                    Messages.Unknown);
            }

            var totals = await totalsT;
            var cats = await catsT;
            var prev = await prevT;
            var levels = await levelsT;
            var activityTypes = await typesT;

            // Tren harian dengan tanggal kosong diisi nol.
            var byDate = totals.ToDictionary(t => t.ReviewDate);

            var trend = Enumerable.Range(0, span)
                .Select(i => from.AddDays(i))
                .Select(d => byDate.TryGetValue(d, out var t)
                    ? new TrendPointDto(
                        d,
                        t.TotalCo2SavedKg,
                        HasReview: true)
                    : new TrendPointDto(
                        d,
                        0m,
                        HasReview: false))
                .ToList();

            // Breakdown kategori.
            var periodTotal = cats.Sum(c => c.TotalCo2SavedKg);

            var breakdown = cats
                .Select(c =>
                {
                    var pct = periodTotal == 0m
                        ? 0m
                        : Math.Round(
                            c.TotalCo2SavedKg / periodTotal * 100m,
                            1,
                            MidpointRounding.AwayFromZero);

                    return new CategoryBreakdownDto(
                        c.CategoryId,
                        c.CategoryName,
                        c.TotalCo2SavedKg,
                        pct,
                        $"{c.CategoryName}: " +
                        $"{c.TotalCo2SavedKg.ToString("0.##", IdCulture)} " +
                        "kg CO₂ pada periode ini");
                })
                .ToList();

            // Penjelasan total dampak.
            var explanation = profile.TotalReviewCount == 0
                ? "Belum ada data. Total dihitung dari kuantitas " +
                  "aktivitas × koefisien emisi (estimasi indikatif)."
                : $"Total dari {profile.TotalReviewCount} refleksi: " +
                  "jumlah kuantitas × koefisien emisi tiap aktivitas " +
                  "(estimasi indikatif).";

            // Streak, level, dan status review hari ini.
            var effectiveStreak = StreakCalculator.Effective(
                profile.User.CurrentStreak,
                profile.LastReviewDate,
                today);

            var levelProgress = LevelProgressFactory.ToDto(
                LevelResolver.Progress(
                    profile.User.TotalPoints,
                    levels));

            var todayState = profile.LastReviewDate == today
                ? TodayReviewState.Submitted
                : TodayReviewState.NotSubmitted;

            // Total periode sebelumnya.
            var previousTotal = prev.Sum(p => p.TotalCo2SavedKg);

            // Integrasi BE-17b.
            var insights = BuildInsights(
                profile.TotalReviewCount,
                periodTotal,
                previousTotal,
                range,
                breakdown,
                activityTypes,
                effectiveStreak);

            return Result<DashboardDto>.Success(
                new DashboardDto(
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
            return Result<DashboardDto>.Failure(
                ex.Code,
                Messages.For(ex.Code));
        }
    }

    /// <summary>
    /// Menghubungkan DashboardService dengan rules BE-17b.
    /// Rules tidak bergantung pada DTO dashboard.
    /// </summary>
    private IReadOnlyList<InsightDto> BuildInsights(
        int totalReviewCount,
        decimal periodTotal,
        decimal previousTotal,
        TrendRange range,
        IReadOnlyList<CategoryBreakdownDto> breakdown,
        IReadOnlyList<ActivityType> activityTypes,
        int effectiveStreak)
    {
        // Konversi DTO menjadi data sederhana untuk Core/Rules.
        var categoryData = breakdown
            .Select(c => (
                CategoryId: c.CategoryId,
                CategoryName: c.CategoryName,
                Co2: c.Co2Kg))
            .ToList();

        // Jika belum pernah melakukan review,
        // tampilkan EmptyState.
        var effectivePeriodTotal = totalReviewCount == 0
            ? 0m
            : periodTotal;

        // Penyusunan insight berdasarkan prioritas I1-I6.
        var generated = DashboardInsightRules.ForDashboard(
            categoryData,
            effectivePeriodTotal,
            range == TrendRange.Weekly
                ? previousTotal
                : null,
            effectiveStreak,
            range,
            activityTypes,
            EstimateSuggestedCo2);

        // Konversi hasil rules menjadi DTO untuk UI.
        return generated
            .Select(i => new InsightDto(i.Kind, i.Text))
            .ToList();
    }

    /// <summary>
    /// Menghitung estimasi CO2 untuk satu contoh aktivitas
    /// menggunakan layanan kalkulasi EcoQuest.
    /// </summary>
    private decimal EstimateSuggestedCo2(ActivityType type)
    {
        // Gunakan contoh kuantitas 1 unit,
        // atau batas maksimum jika lebih kecil.
        var quantity = Math.Min(1m, type.HardMaxPerDay);

        if (type.PlausibleMaxPerDay.HasValue)
        {
            quantity = Math.Min(
                quantity,
                type.PlausibleMaxPerDay.Value);
        }

        if (quantity <= 0m)
            return 0m;

        ActivityInput input;

        switch (type.InputControlType)
        {
            case InputControlType.Numeric:
            case InputControlType.Count:
                input = new ActivityInput(
                    type.ActivityTypeId,
                    quantity,
                    null,
                    null,
                    null,
                    null,
                    null);
                break;

            case InputControlType.TimeRange:
                // Contoh durasi maksimal satu jam.
                // Durasi disesuaikan dengan batas aktivitas.
                var minutes = Math.Max(
                    1,
                    (int)Math.Floor(quantity * 60m));

                var start = new TimeOnly(8, 0);
                var end = start.AddMinutes(minutes);

                input = new ActivityInput(
                    type.ActivityTypeId,
                    null,
                    start,
                    end,
                    null,
                    null,
                    null);
                break;

            case InputControlType.Toggle:
                input = new ActivityInput(
                    type.ActivityTypeId,
                    null,
                    null,
                    null,
                    true,
                    null,
                    null);
                break;

            default:
                return 0m;
        }

        var result = impactCalculation.Calculate(input, type);

        return result.Co2SavedKg;
    }
}
