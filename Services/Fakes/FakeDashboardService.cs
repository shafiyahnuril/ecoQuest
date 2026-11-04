using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;

namespace EcoQuest.Services.Fakes;

public sealed class FakeDashboardService : IDashboardService
{
    private readonly FakeContext _ctx;
    private readonly FakeStore _store;

    public FakeDashboardService(FakeContext ctx, FakeStore store)
    {
        _ctx = ctx; _store = store;
    }

    public async Task<Result<DashboardDto>> GetDashboardAsync(TrendRange range, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("dashboard")) return _ctx.NetworkFailure<DashboardDto>();

        var today = _ctx.Clock.Today;
        var span = range == TrendRange.Weekly ? 7 : 30;
        var from = today.AddDays(-(span - 1));
        var byDate = _store.Days.ToDictionary(d => d.ReviewDate);

        var trend = Enumerable.Range(0, span)
            .Select(i => from.AddDays(i))
            .Select(date => byDate.TryGetValue(date, out var day)
                ? new TrendPointDto(date, day.TotalCo2Kg, true)
                : new TrendPointDto(date, 0m, false))
            .ToList();

        var inRange = _store.Days.Where(d => d.ReviewDate >= from).SelectMany(d => d.Items).ToList();
        var rangeTotal = inRange.Sum(i => i.Co2SavedKg);

        // Percentage is 0..100 (one decimal) of the period total; the contract does not state the scale.
        var breakdown = FakeData.Categories.Select(c =>
        {
            var items = inRange.Where(i => i.CategoryId == c.CategoryId).ToList();
            var co2 = items.Sum(i => i.Co2SavedKg);
            var pct = rangeTotal == 0 ? 0m : Math.Round(co2 / rangeTotal * 100m, 1, MidpointRounding.AwayFromZero);
            return new CategoryBreakdownDto(c.CategoryId, c.CategoryName, co2, pct,
                $"{c.CategoryName}: {FakeImpact.Num(co2)} kg CO₂ dari {items.Count} aktivitas");
        }).ToList();

        return Result<DashboardDto>.Success(new DashboardDto(
            _store.TotalCo2Kg,
            _store.TotalReviewCount == 0
                ? "Belum ada data. Total dihitung dari kuantitas aktivitas × koefisien emisi (estimasi indikatif)."
                : $"Total dari {_store.TotalReviewCount} refleksi: jumlah kuantitas × koefisien emisi tiap aktivitas (estimasi indikatif).",
            _store.TotalPoints, _store.CurrentStreak, _store.LevelProgress(),
            _store.TodaySummary is null ? TodayReviewState.NotSubmitted : TodayReviewState.Submitted,
            range, trend, breakdown, BuildInsights(breakdown, rangeTotal)));
    }

    private IReadOnlyList<InsightDto> BuildInsights(List<CategoryBreakdownDto> breakdown, decimal rangeTotal)
    {
        if (_store.TotalReviewCount == 0)
            return [new InsightDto(InsightKind.EmptyState, "Catat aktivitas nyata hari ini untuk membuka insight personal.")];
        if (rangeTotal == 0)
            return [new InsightDto(InsightKind.EmptyState, "Belum ada refleksi pada periode ini. Catat aktivitas nyata hari ini untuk melihat insight.")];

        var insights = new List<InsightDto>();
        var positive = breakdown.Where(b => b.Co2Kg > 0).ToList();
        var top = positive.OrderByDescending(b => b.Co2Kg).First();
        insights.Add(new InsightDto(InsightKind.TopCategory,
            $"Kategori {top.CategoryName} paling banyak menurunkan emisimu ({FakeImpact.Num(top.Percentage, "0.#")}% dari total periode ini)."));

        var weakest = positive.OrderBy(b => b.Co2Kg).First();
        if (weakest.CategoryId != top.CategoryId)
            insights.Add(new InsightDto(InsightKind.WeakestCategory,
                $"Kategori {weakest.CategoryName} masih kecil kontribusinya. Coba tambah satu kebiasaan di kategori ini."));

        if (_store.CurrentStreak >= 2)
            insights.Add(new InsightDto(InsightKind.Streak, $"Kamu sudah refleksi {_store.CurrentStreak} hari berturut-turut. Pertahankan!"));
        return insights;
    }
}
