using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Tests.Calculation;

// Nilai koefisien = seed database v1.2; literal hanya boleh ada di test.
internal static class CalculationTestData
{
    public static ActivityType Walk() => new(1, "Jalan Kaki / Bersepeda", 1, InputControlType.Numeric, "km",
        0.15m, 1.0m, 30m, 300m, null, 1);

    public static ActivityType Transit() => new(2, "Transportasi Umum", 1, InputControlType.Numeric, "km",
        0.09m, 1.0m, 150m, 1000m, null, 2);

    public static ActivityType Lamp() => new(3, "Mematikan Lampu Saat Tidak Dipakai", 2, InputControlType.TimeRange, "jam",
        0.013m, 0.8m, 12m, 24m, null, 3);

    public static ActivityType Sorting() => new(4, "Memilah Sampah Organik/Anorganik", 3, InputControlType.Count, "kali",
        0.3m, 1.2m, 10m, 50m, null, 4);

    public static ActivityType Tumbler() => new(5, "Membawa Tumbler Sendiri", 4, InputControlType.Toggle, "kali",
        0.1m, 1.0m, null, 1m, null, 5);

    public static ActivityType Bags() => new(6, "Membawa Kantong Belanja Sendiri", 4, InputControlType.Count, "kali",
        0.1m, 1.2m, 5m, 20m, null, 6);

    public static ActivityInput Qty(ActivityType t, decimal? q) =>
        new(t.ActivityTypeId, q, null, null, null, null, null);

    public static ActivityInput Time(ActivityType t, TimeOnly? s, TimeOnly? e) =>
        new(t.ActivityTypeId, null, s, e, null, null, null);

    public static ActivityInput Yes(ActivityType t, bool? v) =>
        new(t.ActivityTypeId, null, null, null, v, null, null);
}
