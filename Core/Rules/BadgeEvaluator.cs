
using System;
using System.Collections.Generic;
using System.Linq;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Rules;

public static class BadgeEvaluator
{
    public static IReadOnlyList<Badge> NewlyEarned(
        IReadOnlyList<Badge> badges,
        IReadOnlyCollection<int> earnedBadgeIds,
        BadgeContext context)
    {
        ArgumentNullException.ThrowIfNull(badges);
        ArgumentNullException.ThrowIfNull(earnedBadgeIds);
        ArgumentNullException.ThrowIfNull(context);

        var earned = earnedBadgeIds.ToHashSet();

        return badges
            .Where(badge =>
                !earned.Contains(badge.BadgeId) &&
                badge.IsSatisfiedBy(context))
            .GroupBy(badge => badge.BadgeId)
            .Select(group => group.First())
            .ToList();
    }
}
