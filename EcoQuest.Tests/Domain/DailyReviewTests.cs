using System;
using EcoQuest.Core.Domain;
using Xunit;

namespace EcoQuest.Tests.Domain;

public class DailyReviewTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 11, 10);

    private static ActivityLog Log(int typeId) =>
        ActivityLog.Rehydrate(Guid.NewGuid(), UserId, null, typeId, Day,
            3m, null, null, null, null, 0.45m, 9, 0.15m, 1.0m);

    [Fact]
    public void AddLog_rejects_duplicate_type()
    {
        var review = new DailyReview(Guid.NewGuid(), UserId, Day);
        review.AddLog(Log(1));
        Assert.Throws<InvalidOperationException>(() => review.AddLog(Log(1)));
    }

    [Fact]
    public void Totals_sum_all_logs()
    {
        var review = new DailyReview(Guid.NewGuid(), UserId, Day);
        review.AddLog(Log(1));
        review.AddLog(Log(2));
        Assert.Equal(0.90m, review.TotalCo2SavedKg);
        Assert.Equal(18, review.TotalPointsEarned);
    }
}
