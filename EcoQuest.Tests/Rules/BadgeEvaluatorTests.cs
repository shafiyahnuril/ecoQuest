
using System;
using System.Collections.Generic;
using System.Linq;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using Xunit;

namespace EcoQuest.Tests.Rules;

public class BadgeEvaluatorTests
{
    private static readonly IReadOnlyList<Badge> Badges =
        new List<Badge>
        {
            new(1, "Langkah Pertama", "Selesaikan satu aktivitas",
                CriteriaType.ActivityCount, 1m),

            new(2, "Konsisten 3 Hari", "Streak tiga hari",
                CriteriaType.StreakDays, 3m),

            new(3, "Konsisten 7 Hari", "Streak tujuh hari",
                CriteriaType.StreakDays, 7m),

            new(4, "Pengumpul Poin", "Kumpulkan 100 poin",
                CriteriaType.PointsThreshold, 100m),

            new(5, "Penyelamat Bumi", "Hemat 10 kg CO2",
                CriteriaType.CO2SavedThreshold, 10m)
        };

    [Fact]
    public void NewlyEarned_FirstActivity_ReturnsFirstBadge()
    {
        var context = new BadgeContext(1, 1, 0.5m, 10);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, Array.Empty<int>(), context);

        Assert.Single(result);
        Assert.Equal(1, result[0].BadgeId);
    }

    [Fact]
    public void NewlyEarned_AlreadyOwned_DoesNotDuplicate()
    {
        var context = new BadgeContext(1, 1, 0.5m, 10);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, new[] { 1 }, context);

        Assert.Empty(result);
    }

    [Fact]
    public void NewlyEarned_ThreeDayStreak_UnlocksThreeDayBadge()
    {
        var context = new BadgeContext(1, 3, 0.5m, 10);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, new[] { 1 }, context);

        Assert.Single(result);
        Assert.Equal(2, result[0].BadgeId);
    }

    [Fact]
    public void NewlyEarned_SevenDayStreak_UnlocksBothStreakBadges()
    {
        var context = new BadgeContext(1, 7, 0.5m, 10);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, new[] { 1 }, context);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.BadgeId == 2);
        Assert.Contains(result, b => b.BadgeId == 3);
    }

    [Fact]
    public void NewlyEarned_PointsThreshold_UnlocksPointsBadge()
    {
        var context = new BadgeContext(0, 0, 0m, 100);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, Array.Empty<int>(), context);

        Assert.Single(result);
        Assert.Equal(4, result[0].BadgeId);
    }

    [Fact]
    public void NewlyEarned_Co2Threshold_UnlocksCo2Badge()
    {
        var context = new BadgeContext(0, 0, 10m, 0);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, Array.Empty<int>(), context);

        Assert.Single(result);
        Assert.Equal(5, result[0].BadgeId);
    }

    [Fact]
    public void NewlyEarned_NoCriteriaMet_ReturnsEmpty()
    {
        var context = new BadgeContext(0, 0, 0m, 0);

        var result = BadgeEvaluator.NewlyEarned(
            Badges, Array.Empty<int>(), context);

        Assert.Empty(result);
    }
}
