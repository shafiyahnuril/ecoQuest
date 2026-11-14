using System;
using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Records;
using EcoQuest.Data.Rpc;

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

    public static Level ToDomain(LevelRecord r) => new(
        r.LevelId,
        r.LevelName,
        r.MinPoints,
        r.MaxPoints);

    public static Badge ToDomain(BadgeRecord r) => new(
        r.BadgeId,
        r.Name,
        r.Description ?? string.Empty,
        ParseCriteria(r.CriteriaType),
        r.CriteriaValue);

    public static WeatherCache ToDomain(WeatherCacheRecord r) => WeatherCache.Create(
        r.City,
        DateOnly.FromDateTime(r.ForDate),
        r.ForecastSummary,
        r.RawResponse,
        r.RetrievedAt);

    public static UserBadge ToDomain(UserBadgeJson j, Guid userId) => new(
        userId,
        j.BadgeId,
        j.EarnedAt);

    public static InputControlType ParseControl(string value)
        => ParseEnum<InputControlType>(value, "inputcontroltype");

    public static CriteriaType ParseCriteria(string value)
        => ParseEnum<CriteriaType>(value, "criteriatype");

    private static TEnum ParseEnum<TEnum>(string value, string columnName) where TEnum : struct, Enum =>
        !string.IsNullOrEmpty(value)
        && Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed)
        && Enum.IsDefined(parsed)
        && string.Equals(parsed.ToString(), value, StringComparison.Ordinal)
            ? parsed
            : throw new DataAccessException(ErrorCode.Unknown, $"{columnName} '{value}' tidak dikenal");

    public static DateOnly ParseDate(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static TimeOnly? ParseTime(string? s) =>
        string.IsNullOrEmpty(s) ? null : TimeOnly.ParseExact(s, "HH:mm:ss", CultureInfo.InvariantCulture);
}
