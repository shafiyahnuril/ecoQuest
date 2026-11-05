using System;
using EcoQuest.Core.Domain;
using Xunit;

namespace EcoQuest.Tests.Domain;

public class ActivityTypeTests
{
    [Fact]
    public void PointFactor_out_of_range_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActivityType(1, "Jalan Kaki", 1, InputControlType.Numeric, "km",
                             0.15m, 2.0m, 30m, 300m, null, 1));
}
