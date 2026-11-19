
using System;

namespace EcoQuest.Core.Rules;

public static class StreakCalculator
{
    public static int Next(
        int previousStreak,
        DateOnly? lastReviewDate,
        DateOnly today)
    {
        if (previousStreak < 0)
            throw new ArgumentOutOfRangeException(nameof(previousStreak));

        if (lastReviewDate is null)
            return 1;

        if (lastReviewDate.Value == today)
            return Math.Max(1, previousStreak);

        if (lastReviewDate.Value == today.AddDays(-1))
            return previousStreak + 1;

        return 1;
    }

    public static int Effective(
        int storedStreak,
        DateOnly? lastReviewDate,
        DateOnly today)
    {
        if (storedStreak < 0)
            throw new ArgumentOutOfRangeException(nameof(storedStreak));

        if (lastReviewDate is null)
            return 0;

        if (lastReviewDate.Value == today ||
            lastReviewDate.Value == today.AddDays(-1))
            return storedStreak;

        return 0;
    }
}
