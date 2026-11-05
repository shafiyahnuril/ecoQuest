using System;
using EcoQuest.Core.Domain;
using Xunit;

namespace EcoQuest.Tests.Domain;

public class ActivityLogTests
{
    private static ActivityLog Lamp(TimeOnly start, TimeOnly end) =>
        ActivityLog.Rehydrate(Guid.NewGuid(), Guid.NewGuid(), null, 3, new DateOnly(2026, 11, 10),
            1m, start, end, null, null, 0m, 0, 0.013m, 0.8m);

    [Fact]
    public void GetDurationInHours_crosses_midnight()
    {
        Assert.Equal(8m, Lamp(new TimeOnly(22, 0), new TimeOnly(6, 0)).GetDurationInHours());
        Assert.Equal(0.3333m, Lamp(new TimeOnly(23, 50), new TimeOnly(0, 10)).GetDurationInHours());
    }

    [Fact]
    public void GetDurationInHours_same_day()
        => Assert.Equal(2.5m, Lamp(new TimeOnly(21, 0), new TimeOnly(23, 30)).GetDurationInHours());
}
