using System;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Records;
using Xunit;

namespace EcoQuest.Tests.Data;

public class RecordMapperTests
{
    [Theory]
    [InlineData("Numeric", InputControlType.Numeric)]
    [InlineData("TimeRange", InputControlType.TimeRange)]
    [InlineData("Count", InputControlType.Count)]
    [InlineData("Toggle", InputControlType.Toggle)]
    public void ParseControl_maps_exact_case_values(string raw, InputControlType expected)
        => Assert.Equal(expected, RecordMapper.ParseControl(raw));

    [Theory]
    [InlineData("numeric")]
    [InlineData("NUMERIC")]
    [InlineData("0")]
    [InlineData("InvalidControl")]
    public void ParseControl_rejects_wrong_case_or_unknown_values(string raw)
        => Assert.Throws<DataAccessException>(() => RecordMapper.ParseControl(raw));

    [Theory]
    [InlineData("ActivityCount", CriteriaType.ActivityCount)]
    [InlineData("PointsThreshold", CriteriaType.PointsThreshold)]
    [InlineData("StreakDays", CriteriaType.StreakDays)]
    [InlineData("CO2SavedThreshold", CriteriaType.CO2SavedThreshold)]
    public void ParseCriteria_maps_exact_case_values(string raw, CriteriaType expected)
        => Assert.Equal(expected, RecordMapper.ParseCriteria(raw));

    [Theory]
    [InlineData("activitycount")]
    [InlineData("0")]
    [InlineData("Unknown")]
    public void ParseCriteria_rejects_wrong_case_or_unknown_values(string raw)
        => Assert.Throws<DataAccessException>(() => RecordMapper.ParseCriteria(raw));

    [Fact]
    public void ParseDate_parses_iso_yyyy_MM_dd()
        => Assert.Equal(new DateOnly(2026, 11, 3), RecordMapper.ParseDate("2026-11-03"));

    [Fact]
    public void ParseTime_parses_hh_mm_ss_and_handles_null_or_empty()
    {
        Assert.Equal(new TimeOnly(21, 0, 0), RecordMapper.ParseTime("21:00:00"));
        Assert.Null(RecordMapper.ParseTime(null));
        Assert.Null(RecordMapper.ParseTime(""));
    }

    [Fact]
    public void ToDomain_maps_LevelRecord_including_unbounded_max_points()
    {
        var rec = new LevelRecord
        {
            LevelId = 4,
            LevelName = "Eco Champion",
            MinPoints = 1500,
            MaxPoints = null
        };

        var domain = RecordMapper.ToDomain(rec);

        Assert.Equal(4, domain.LevelId);
        Assert.Equal("Eco Champion", domain.LevelName);
        Assert.Equal(1500, domain.MinPoints);
        Assert.Null(domain.MaxPoints);
    }

    [Fact]
    public void ToDomain_maps_BadgeRecord_and_coalesces_null_description()
    {
        var rec = new BadgeRecord
        {
            BadgeId = 6,
            Name = "1 kg CO2 Pertama",
            Description = null,
            CriteriaType = "CO2SavedThreshold",
            CriteriaValue = 1m
        };

        var domain = RecordMapper.ToDomain(rec);

        Assert.Equal(6, domain.BadgeId);
        Assert.Equal("1 kg CO2 Pertama", domain.Name);
        Assert.Equal(string.Empty, domain.Description);
        Assert.Equal(CriteriaType.CO2SavedThreshold, domain.CriteriaType);
        Assert.Equal(1m, domain.CriteriaValue);
    }

    [Fact]
    public void ToDomain_maps_WeatherCacheRecord_with_parsed_date()
    {
        var retrievedAt = new DateTimeOffset(2026, 11, 13, 6, 30, 0, TimeSpan.FromHours(7));
        var rec = new WeatherCacheRecord
        {
            CacheId = 12,
            City = "Yogyakarta",
            ForDate = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Unspecified),
            ForecastSummary = "Cerah berawan, 24–31°C",
            RawResponse = "{\"Headline\":\"Sunny\"}",
            RetrievedAt = retrievedAt
        };

        var domain = RecordMapper.ToDomain(rec);

        Assert.Equal("Yogyakarta", domain.City);
        Assert.Equal(new DateOnly(2026, 11, 13), domain.ForDate);
        Assert.Equal("Cerah berawan, 24–31°C", domain.ForecastSummary);
        Assert.Equal("{\"Headline\":\"Sunny\"}", domain.RawResponse);
        Assert.Equal(retrievedAt, domain.RetrievedAt);
    }

    [Fact]
    public void ToDomain_maps_ActivityTypeRecord_and_ActivityCategoryRecord()
    {
        var catRec = new ActivityCategoryRecord { CategoryId = 1, CategoryName = "Transportasi", Description = "Mobilitas" };
        var cat = RecordMapper.ToDomain(catRec);
        Assert.Equal(1, cat.CategoryId);
        Assert.Equal("Transportasi", cat.CategoryName);
        Assert.Equal("Mobilitas", cat.Description);

        var typeRec = new ActivityTypeRecord
        {
            ActivityTypeId = 1,
            TypeName = "Jalan Kaki",
            CategoryId = 1,
            InputControlType = "Numeric",
            UnitLabel = "km",
            Co2Coefficient = 0.15m,
            PointFactor = 1.0m,
            PlausibleMaxPerDay = 30m,
            HardMaxPerDay = 300m,
            Description = "Harian",
            DisplayOrder = 1
        };

        var type = RecordMapper.ToDomain(typeRec);
        Assert.Equal(InputControlType.Numeric, type.InputControlType);
        Assert.Equal(0.15m, type.Co2Coefficient);
    }
}
