
using System;
using EcoQuest.Core.Rules;
using Xunit;

namespace EcoQuest.Tests.Rules;

public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    [Fact]
    public void Next_FirstReview_ReturnsOne()
    {
        var result = StreakCalculator.Next(0, null, Today);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Next_ReviewedYesterday_IncrementsStreak()
    {
        var yesterday = Today.AddDays(-1);

        var result = StreakCalculator.Next(3, yesterday, Today);

        Assert.Equal(4, result);
    }

    [Fact]
    public void Next_MissedSeveralDays_ResetsToOne()
    {
        var threeDaysAgo = Today.AddDays(-3);

        var result = StreakCalculator.Next(5, threeDaysAgo, Today);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Next_AlreadyReviewedToday_DoesNotIncrement()
    {
        var result = StreakCalculator.Next(5, Today, Today);

        Assert.Equal(5, result);
    }

    [Fact]
    public void Effective_ReviewedThreeDaysAgo_ReturnsZero()
    {
        var threeDaysAgo = Today.AddDays(-3);

        var result = StreakCalculator.Effective(5, threeDaysAgo, Today);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Effective_ReviewedYesterday_KeepsStreak()
    {
        var yesterday = Today.AddDays(-1);

        var result = StreakCalculator.Effective(5, yesterday, Today);

        Assert.Equal(5, result);
    }

    [Fact]
    public void Effective_NoPreviousReview_ReturnsZero()
    {
        var result = StreakCalculator.Effective(0, null, Today);

        Assert.Equal(0, result);
    }
}
