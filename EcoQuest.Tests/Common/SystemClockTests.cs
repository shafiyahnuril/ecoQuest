using System;
using EcoQuest.Infrastructure;
using Xunit;

namespace EcoQuest.Tests.Common;

public class SystemClockTests
{
    private static SystemClock At(DateTimeOffset utc) =>
        new(TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"), () => utc);

    [Fact]
    public void Today_is_jakarta_date_not_utc_date()
    {
        // 17:30 UTC tanggal 24 = 00:30 WIB tanggal 25
        var clock = At(new DateTimeOffset(2026, 10, 24, 17, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 25), clock.Today);
    }

    [Fact]
    public void Today_stays_same_day_before_midnight_wib()
    {
        // 16:59 UTC tanggal 24 = 23:59 WIB tanggal 24
        var clock = At(new DateTimeOffset(2026, 10, 24, 16, 59, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 24), clock.Today);
    }

    [Fact]
    public void Timezone_is_utc_plus_7()
    {
        var clock = At(DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.FromHours(7), clock.TimeZone.BaseUtcOffset);
    }
}
