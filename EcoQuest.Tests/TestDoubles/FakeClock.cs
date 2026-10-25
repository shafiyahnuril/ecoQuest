using System;
using EcoQuest.Core.Common;

namespace EcoQuest.Tests.TestDoubles;

/// <summary>Jam palsu untuk unit test: atur sendiri "sekarang"-nya.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow, TimeZoneInfo? timeZone = null)
    {
        UtcNow = utcNow;
        TimeZone = timeZone ?? TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    }

    public DateTimeOffset UtcNow { get; set; }
    public TimeZoneInfo TimeZone { get; }
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime);
}
