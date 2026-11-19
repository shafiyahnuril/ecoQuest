using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public sealed class DurationImpactCalculator : ImpactCalculatorBase
{
    public override InputControlType SupportedType => InputControlType.TimeRange;

    public static decimal Hours(TimeOnly start, TimeOnly end)
    {
        var minutes = (decimal)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;
        if (minutes < 0) minutes += 24 * 60;                      // lintas tengah malam (Skema v2 §9.1)
        return Math.Round(minutes / 60m, 4, MidpointRounding.AwayFromZero);
    }

    public override decimal ResolveQuantity(ActivityInput i) =>
        i.StartTime is { } s && i.EndTime is { } e && s != e ? Hours(s, e) : 0m;

    protected override string FormatQuantity(decimal hours, ActivityType t)
    {
        var totalMinutes = (int)Math.Round(hours * 60m, MidpointRounding.AwayFromZero);
        var h = totalMinutes / 60; var m = totalMinutes % 60;
        return m == 0 ? $"{h} j" : h == 0 ? $"{m} m" : $"{h} j {m} m";
    }
    protected override string UnitSingular(ActivityType t) => "jam";

    public override IReadOnlyList<ValidationIssue> ValidateHard(ActivityInput i, ActivityType t)
    {
        var list = new List<ValidationIssue>();
        if (i.StartTime is null && i.EndTime is null) return list;                         // tidak dilakukan
        if (i.StartTime is null) list.Add(Error(t.ActivityTypeId, "StartTime", "Lengkapi jam mulai"));
        else if (i.EndTime is null) list.Add(Error(t.ActivityTypeId, "EndTime", "Lengkapi jam selesai"));
        else if (i.StartTime == i.EndTime) list.Add(Error(t.ActivityTypeId, "TimeRange", "Rentang jam tidak boleh 0"));
        else if (Hours(i.StartTime.Value, i.EndTime.Value) > t.HardMaxPerDay)
            list.Add(Error(t.ActivityTypeId, "TimeRange", "Rentang jam tidak boleh melebihi 24 jam"));
        return list;
    }
}
