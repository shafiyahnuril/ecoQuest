using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using EcoQuest.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Services;

public class DashboardServiceTests
{
    private static readonly IReadOnlyList<Level> DefaultLevels =
    [
        new(1, "Eco Starter", 0, 99),
        new(2, "Eco Explorer", 100, 499),
        new(3, "Eco Warrior", 500, 1499),
        new(4, "Eco Champion", 1500, null)
    ];

    private static readonly IReadOnlyList<ActivityType> DefaultTypes =
        [Walk(), Transit(), Lamp(), Sorting(), Tumbler(), Bags()];

    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDailyReviewRepository _reviews = Substitute.For<IDailyReviewRepository>();
    private readonly ILevelRepository _levels = Substitute.For<ILevelRepository>();
    private readonly IActivityTypeRepository _types = Substitute.For<IActivityTypeRepository>();
    private readonly UserSession _activeUser = new(Guid.NewGuid(), "dinda_21", "dinda@mail.ugm.ac.id", "Yogyakarta", null);

    public DashboardServiceTests()
    {
        _session.RequireUser().Returns(_activeUser);
        _session.Current.Returns(_activeUser);
        _levels.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultLevels);
        _types.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultTypes);
        _reviews.GetDailyTotalsAsync(_activeUser.UserId, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
                .Returns([]);
        _reviews.GetCategoryBreakdownAsync(_activeUser.UserId, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
                .Returns(ZeroBreakdown());
    }

    private DashboardService CreateSut()
        => new(_session, _clock, _users, _reviews, _levels, _types);

    private UserProfileSnapshot ProfileWith(
        int totalPoints = 150,
        decimal totalCo2 = 12.5m,
        int storedStreak = 4,
        DateOnly? lastReviewDate = null,
        int totalReviewCount = 5,
        int totalLogCount = 10)
    {
        var user = new User(
            _activeUser.UserId,
            _activeUser.UserName,
            _activeUser.Email,
            _activeUser.City,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            totalPoints: totalPoints,
            totalCo2SavedKg: totalCo2,
            currentLevelId: 2,
            currentStreak: storedStreak);
        return new UserProfileSnapshot(user, lastReviewDate, totalReviewCount, totalLogCount);
    }

    private static IReadOnlyList<CategoryTotalRow> ZeroBreakdown() =>
    [
        new(1, "Transportasi", 0m),
        new(2, "Energi", 0m),
        new(3, "Sampah", 0m),
        new(4, "Konsumsi", 0m)
    ];

    [Fact]
    public async Task Fills_missing_days_with_zero()
    {
        var today = _clock.Today;
        var from = today.AddDays(-6);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(ProfileWith(lastReviewDate: today));
        _reviews.GetDailyTotalsAsync(_activeUser.UserId, from, today, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new DailyTotalRow(today.AddDays(-4), 1.2m, 24),
                    new DailyTotalRow(today, 0.45m, 9)
                ]);

        var res = await CreateSut().GetDashboardAsync(TrendRange.Weekly);

        Assert.True(res.IsSuccess);
        Assert.Equal(7, res.Value!.Trend.Count);
        Assert.Equal(5, res.Value.Trend.Count(p => p.Co2Kg == 0m && !p.HasReview));
        Assert.Equal(2, res.Value.Trend.Count(p => p.HasReview));
    }

    [Fact]
    public async Task Weekly_is_7_points_monthly_30()
    {
        var today = _clock.Today;
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(ProfileWith(lastReviewDate: today));

        var sut = CreateSut();
        var weekly = await sut.GetDashboardAsync(TrendRange.Weekly);
        var monthly = await sut.GetDashboardAsync(TrendRange.Monthly);

        Assert.True(weekly.IsSuccess);
        Assert.Equal(7, weekly.Value!.Trend.Count);
        Assert.Equal(today.AddDays(-6), weekly.Value.Trend[0].Date);
        Assert.Equal(today, weekly.Value.Trend[^1].Date);

        Assert.True(monthly.IsSuccess);
        Assert.Equal(30, monthly.Value!.Trend.Count);
        Assert.Equal(today.AddDays(-29), monthly.Value.Trend[0].Date);
        Assert.Equal(today, monthly.Value.Trend[^1].Date);
    }

    [Fact]
    public async Task Breakdown_always_4_and_percent_sum_100()
    {
        var today = _clock.Today;
        var from = today.AddDays(-6);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(ProfileWith(lastReviewDate: today));

        _reviews.GetCategoryBreakdownAsync(_activeUser.UserId, from, today, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new CategoryTotalRow(1, "Transportasi", 1.0m),
                    new CategoryTotalRow(2, "Energi", 2.0m),
                    new CategoryTotalRow(3, "Sampah", 0m),
                    new CategoryTotalRow(4, "Konsumsi", 0.333m)
                ]);

        var sut = CreateSut();
        var res = await sut.GetDashboardAsync(TrendRange.Weekly);

        Assert.True(res.IsSuccess);
        Assert.Equal(4, res.Value!.Breakdown.Count);
        var sum = res.Value.Breakdown.Sum(b => b.Percentage);
        Assert.InRange(sum, 99.9m, 100.1m);

        _reviews.GetCategoryBreakdownAsync(_activeUser.UserId, from, today, Arg.Any<CancellationToken>())
                .Returns(ZeroBreakdown());

        var zeroRes = await sut.GetDashboardAsync(TrendRange.Weekly);
        Assert.True(zeroRes.IsSuccess);
        Assert.Equal(4, zeroRes.Value!.Breakdown.Count);
        Assert.All(zeroRes.Value.Breakdown, b => Assert.Equal(0m, b.Percentage));
    }

    [Fact]
    public async Task Empty_profile_returns_EmptyPeriod_insight_and_empty_explanation()
    {
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(ProfileWith(totalPoints: 0, totalCo2: 0m, storedStreak: 0, lastReviewDate: null, totalReviewCount: 0, totalLogCount: 0));

        var res = await CreateSut().GetDashboardAsync(TrendRange.Weekly);

        Assert.True(res.IsSuccess);
        var dto = res.Value!;
        Assert.Equal(0, dto.TotalReviewCount);
        Assert.Equal(TodayReviewState.NotSubmitted, dto.TodayState);
        Assert.Equal("Belum ada data. Total dihitung dari kuantitas aktivitas × koefisien emisi (estimasi indikatif).", dto.TotalCo2Explanation);
        var insight = Assert.Single(dto.Insights);
        Assert.Equal(InsightKind.EmptyState, insight.Kind);
    }

    [Fact]
    public async Task Populated_dashboard_sets_hero_TotalReviewCount_effective_streak_TodayState_and_at_most_3_insights()
    {
        var today = _clock.Today;
        var from = today.AddDays(-6);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(ProfileWith(totalPoints: 180, totalCo2: 24.5m, storedStreak: 5, lastReviewDate: today.AddDays(-3), totalReviewCount: 9, totalLogCount: 15));

        _reviews.GetCategoryBreakdownAsync(_activeUser.UserId, from, today, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new CategoryTotalRow(1, "Transportasi", 1.5m),
                    new CategoryTotalRow(2, "Energi", 0.5m),
                    new CategoryTotalRow(3, "Sampah", 0m),
                    new CategoryTotalRow(4, "Konsumsi", 0.3m)
                ]);
        _reviews.GetDailyTotalsAsync(_activeUser.UserId, from.AddDays(-7), from.AddDays(-1), Arg.Any<CancellationToken>())
                .Returns([new DailyTotalRow(from.AddDays(-2), 1.0m, 20)]);

        var res = await CreateSut().GetDashboardAsync(TrendRange.Weekly);

        Assert.True(res.IsSuccess);
        var dto = res.Value!;
        Assert.Equal(24.5m, dto.TotalCo2Kg);
        Assert.Equal(180, dto.TotalPoints);
        Assert.Equal(9, dto.TotalReviewCount);
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(TodayReviewState.NotSubmitted, dto.TodayState);
        Assert.Equal("Total dari 9 refleksi: jumlah kuantitas × koefisien emisi tiap aktivitas (estimasi indikatif).", dto.TotalCo2Explanation);
        Assert.Equal("Eco Explorer", dto.Level.LevelName);
        Assert.InRange(dto.Insights.Count, 1, 3);
        Assert.Equal(InsightKind.TopCategory, dto.Insights[0].Kind);
    }

    [Fact]
    public async Task DataAccessException_returns_NetworkError_failure()
    {
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Throws(new DataAccessException(ErrorCode.NetworkError, "app_get_profile"));

        var res = await CreateSut().GetDashboardAsync(TrendRange.Weekly);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, res.Error);
        Assert.Equal(Messages.NetworkError, res.Message);
    }

    [Fact]
    public void BackendRegistration_resolves_IDashboardService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        Assert.IsType<DashboardService>(sp.GetRequiredService<IDashboardService>());
    }
}
