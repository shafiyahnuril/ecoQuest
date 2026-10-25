using System;
using EcoQuest.Core.Common;

namespace EcoQuest.Infrastructure;

/// <summary>
/// Implementasi nyata IClock. Satu-satunya tempat yang boleh membaca jam sistem (ADR-013).
/// Tanggal jurnal selalu memakai zona waktu Asia/Jakarta (WIB).
/// </summary>
public sealed class SystemClock : IClock
{
    private readonly Func<DateTimeOffset> _utcNow;

    public SystemClock() : this(TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"), null) { }

    // Konstruktor kedua dipakai unit test agar jam bisa diatur.
    public SystemClock(TimeZoneInfo timeZone, Func<DateTimeOffset>? utcNowProvider)
    {
        TimeZone = timeZone;
        _utcNow = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public TimeZoneInfo TimeZone { get; }

    public DateTimeOffset UtcNow => _utcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime);
}
