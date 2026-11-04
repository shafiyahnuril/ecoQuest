using System;
using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Records;

namespace EcoQuest.Data.Mapping;

public static class RecordMapper
{
    public static ActivityType ToDomain(ActivityTypeRecord r) => new(
        r.ActivityTypeId,
        r.TypeName,
        r.CategoryId,
        ParseControl(r.InputControlType),
        r.UnitLabel,
        r.Co2Coefficient,
        r.PointFactor,
        r.PlausibleMaxPerDay,
        r.HardMaxPerDay,
        r.Description,
        r.DisplayOrder);

    public static ActivityCategory ToDomain(ActivityCategoryRecord r) => new(
        r.CategoryId,
        r.CategoryName,
        r.Description);

    public static InputControlType ParseControl(string value) =>
        Enum.TryParse<InputControlType>(value, ignoreCase: false, out var t) && Enum.IsDefined(t)
            ? t
            : throw new DataAccessException(ErrorCode.Unknown, $"inputcontroltype '{value}' tidak dikenal");

    public static DateOnly ParseDate(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static TimeOnly? ParseTime(string? s) =>
        string.IsNullOrEmpty(s) ? null : TimeOnly.ParseExact(s, "HH:mm:ss", CultureInfo.InvariantCulture);
}
