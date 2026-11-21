
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
using EcoQuest.Services;
using EcoQuest.Infrastructure;
using EcoQuest.Tests.TestDoubles;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.Services;

public class GamificationServiceTests
{
    private static readonly IReadOnlyList<Level> DefaultLevels =
    [
        new(1, "Eco Starter", 0, 99),
        new(2, "Eco Explorer", 100, 499),
        new(3, "Eco Warrior", 500, 1499),
        new(4, "Eco Champion", 1500, null)
    ];

    private static readonly IReadOnlyList<Badge> DefaultBadges =
    [
        new(1, "Aktif", "Catat 10 aktivitas",
            CriteriaType.ActivityCount, 10m),
        new(2, "Konsisten", "Streak 5 hari",
            CriteriaType.StreakDays, 5m),
        new(3, "Hemat CO2", "Hemat 20 kg CO2",
            CriteriaType.CO2SavedThreshold, 20m),
        new(4, "Pengumpul Poin", "Kumpulkan 200 poin",
            CriteriaType.PointsThreshold, 200m)
    ];

    private readonly ISessionService _session =
        Substitute.For<ISessionService>();

    private readonly IUserRepository _users =
        Substitute.For<IUserRepository>();

    private readonly ILevelRepository _levels =
        Substitute.For<ILevelRepository>();

    private readonly IBadgeRepository _badges =
        Substitute.For<IBadgeRepository>();

    private readonly FakeClock _clock =
        new(new DateTimeOffset(
            2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    private readonly UserSession _activeUser =
        new(
            Guid.NewGuid(),
            "dinda_21",
            "dinda@mail.ugm.ac.id",
            "Yogyakarta",
            null);

    public GamificationServiceTests()
    {
        _session.RequireUser().Returns(_activeUser);

        _levels.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultLevels);

        _badges.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultBadges);

        _badges.GetUserBadgesAsync(
                _activeUser.UserId,
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UserBadge>());
    }

    private GamificationService CreateSut()
        => new(
            _session,
            _users,
            _levels,
            _badges,
            _clock);

    private UserProfileSnapshot ProfileWith(
        int totalPoints = 150,
        decimal totalCo2 = 12.5m,
        int storedStreak = 4,
        DateOnly? lastReviewDate = null,
        int totalLogCount = 10)
    {
        var user = new User(
            _activeUser.UserId,
            _activeUser.UserName,
            _activeUser.Email,
            _activeUser.City,
            new DateTimeOffset(
                2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            totalPoints: totalPoints,
            totalCo2SavedKg: totalCo2,
            currentLevelId: 2,
            currentStreak: storedStreak);

        return new UserProfileSnapshot(
            user,
            lastReviewDate,
            5,
            totalLogCount);
    }

    private void SetProfile(UserProfileSnapshot profile)
    {
        _users.GetProfileAsync(
                _activeUser.UserId,
                Arg.Any<CancellationToken>())
            .Returns(profile);
    }

    [Fact]
    public async Task GetLevelProgress_returns_correct_level_and_progress()
    {
        SetProfile(ProfileWith(totalPoints: 150));

        var result = await CreateSut().GetLevelProgressAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(2, result.Value.LevelId);
        Assert.Equal("Eco Explorer", result.Value.LevelName);
        Assert.Equal(150, result.Value.CurrentPoints);
        Assert.Equal("Eco Warrior", result.Value.NextLevelName);
        Assert.Equal(350, result.Value.PointsToNext);
        Assert.Equal(0.125, result.Value.ProgressRatio, 3);
    }

    [Fact]
    public async Task GetLevelProgress_highest_level_has_full_progress()
    {
        SetProfile(ProfileWith(totalPoints: 1800));

        var result = await CreateSut().GetLevelProgressAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(4, result.Value.LevelId);
        Assert.Equal(1.0, result.Value.ProgressRatio);
        Assert.Null(result.Value.NextLevelName);
        Assert.Null(result.Value.PointsToNext);
    }

    [Fact]
    public async Task GetBadges_returns_all_badges_including_unearned()
    {
        SetProfile(ProfileWith(lastReviewDate: _clock.Today));

        var earnedAt = new DateTimeOffset(
            2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        IReadOnlyList<UserBadge> earned =
        [
            new(_activeUser.UserId, 1, earnedAt)
        ];

        _badges.GetUserBadgesAsync(
                _activeUser.UserId,
                Arg.Any<CancellationToken>())
            .Returns(earned);

        var result = await CreateSut().GetBadgesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(4, result.Value.Count);

        var activityBadge = Assert.Single(
            result.Value, b => b.BadgeId == 1);

        Assert.True(activityBadge.IsEarned);
        Assert.Equal(earnedAt, activityBadge.EarnedAt);

        var unearnedBadges = result.Value
            .Where(b => b.BadgeId != 1);

        Assert.All(unearnedBadges, badge =>
        {
            Assert.False(badge.IsEarned);
            Assert.Null(badge.EarnedAt);
        });
    }

    [Fact]
    public async Task GetBadges_calculates_current_value_for_all_criteria()
    {
        SetProfile(ProfileWith(
            totalPoints: 150,
            totalCo2: 12.5m,
            storedStreak: 4,
            lastReviewDate: _clock.Today,
            totalLogCount: 10));

        var result = await CreateSut().GetBadgesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(10m,
            result.Value.Single(b =>
                b.CriteriaType == CriteriaType.ActivityCount)
                .CurrentValue);

        Assert.Equal(4m,
            result.Value.Single(b =>
                b.CriteriaType == CriteriaType.StreakDays)
                .CurrentValue);

        Assert.Equal(12.5m,
            result.Value.Single(b =>
                b.CriteriaType == CriteriaType.CO2SavedThreshold)
                .CurrentValue);

        Assert.Equal(150m,
            result.Value.Single(b =>
                b.CriteriaType == CriteriaType.PointsThreshold)
                .CurrentValue);
    }

    [Fact]
    public async Task GetBadges_expired_streak_has_zero_progress()
    {
        SetProfile(ProfileWith(
            storedStreak: 7,
            lastReviewDate: _clock.Today.AddDays(-2)));

        var result = await CreateSut().GetBadgesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var streakBadge = result.Value.Single(
            b => b.CriteriaType == CriteriaType.StreakDays);

        Assert.Equal(0m, streakBadge.CurrentValue);
    }

    [Fact]
    public async Task GetBadges_earned_badge_remains_earned_when_streak_expires()
    {
        SetProfile(ProfileWith(
            storedStreak: 7,
            lastReviewDate: _clock.Today.AddDays(-2)));

        var earnedAt = new DateTimeOffset(
            2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

        IReadOnlyList<UserBadge> earned =
        [
            new(_activeUser.UserId, 2, earnedAt)
        ];

        _badges.GetUserBadgesAsync(
                _activeUser.UserId,
                Arg.Any<CancellationToken>())
            .Returns(earned);

        var result = await CreateSut().GetBadgesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var streakBadge = result.Value.Single(
            b => b.CriteriaType == CriteriaType.StreakDays);

        Assert.True(streakBadge.IsEarned);
        Assert.Equal(earnedAt, streakBadge.EarnedAt);
        Assert.Equal(0m, streakBadge.CurrentValue);
    }

    [Fact]
    public async Task GetLevelProgress_missing_profile_returns_failure()
    {
        _users.GetProfileAsync(
                _activeUser.UserId,
                Arg.Any<CancellationToken>())
            .Returns((UserProfileSnapshot?)null);

        var result = await CreateSut().GetLevelProgressAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Error);
    }

    [Fact]
    public async Task GetBadges_missing_profile_returns_failure()
    {
        _users.GetProfileAsync(
                _activeUser.UserId,
                Arg.Any<CancellationToken>())
            .Returns((UserProfileSnapshot?)null);

        var result = await CreateSut().GetBadgesAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Error);
    }
}
