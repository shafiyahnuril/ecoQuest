using System;
using System.Collections.Generic;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcoQuest.Tests.Data;

public class ReviewMapperTests
{
    [Fact]
    public void BuildSubmitItems_uses_lowercase_keys_without_p_prefix_and_formats_times()
    {
        var userId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var log = ActivityLog.Rehydrate(
            logId,
            userId,
            null,
            3,
            new DateOnly(2026, 11, 19),
            2.5m,
            new TimeOnly(21, 0),
            new TimeOnly(23, 30),
            "Matikan lampu kamar",
            $"photos/{userId}/{logId}.png",
            0.0325m,
            1,
            0.013m,
            0.8m);

        var items = ReviewMapper.BuildSubmitItems([log]);

        Assert.Single(items);
        var item = items[0];
        Assert.Equal(logId, item["logid"]);
        Assert.Equal(3, item["activitytypeid"]);
        Assert.Equal(2.5m, item["quantity"]);
        Assert.Equal("21:00:00", item["starttime"]);
        Assert.Equal("23:30:00", item["endtime"]);
        Assert.Equal("Matikan lampu kamar", item["note"]);
        Assert.Equal($"photos/{userId}/{logId}.png", item["photopath"]);
        Assert.Equal(0.0325m, item["co2savedkg"]);
        Assert.Equal(1, item["pointsearned"]);
        Assert.Equal(0.013m, item["co2coefficientused"]);
        Assert.Equal(0.8m, item["pointfactorused"]);
        Assert.DoesNotContain(item.Keys, k => k.StartsWith("p_", StringComparison.Ordinal));
    }

    [Fact]
    public void ToDomain_rehydrates_DailyReview_with_logs_and_snapshot_fields()
    {
        var userId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var logId1 = Guid.NewGuid();
        var logId2 = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 11, 19, 13, 45, 0, TimeSpan.Zero);

        var json = new ReviewJson
        {
            ReviewId = reviewId,
            ReviewDate = "2026-11-19",
            TotalCo2SavedKg = 0.4825m,
            TotalPointsEarned = 10,
            SubmittedAt = submittedAt,
            Logs =
            [
                new ReviewLogJson
                {
                    LogId = logId1,
                    ActivityTypeId = 1,
                    Quantity = 3m,
                    StartTime = null,
                    EndTime = null,
                    Note = "Jalan sore",
                    PhotoPath = "photos/u/1.jpg",
                    Co2SavedKg = 0.45m,
                    PointsEarned = 9,
                    Co2CoefficientUsed = 0.15m,
                    PointFactorUsed = 1.0m
                },
                new ReviewLogJson
                {
                    LogId = logId2,
                    ActivityTypeId = 3,
                    Quantity = 2.5m,
                    StartTime = "21:00:00",
                    EndTime = "23:30:00",
                    Note = null,
                    PhotoPath = null,
                    Co2SavedKg = 0.0325m,
                    PointsEarned = 1,
                    Co2CoefficientUsed = 0.013m,
                    PointFactorUsed = 0.8m
                }
            ]
        };

        var review = ReviewMapper.ToDomain(json, userId);

        Assert.Equal(reviewId, review.ReviewId);
        Assert.Equal(userId, review.UserId);
        Assert.Equal(new DateOnly(2026, 11, 19), review.ReviewDate);
        Assert.Equal(submittedAt, review.SubmittedAt);
        Assert.Equal(2, review.Logs.Count);
        Assert.Equal(0.4825m, review.TotalCo2SavedKg);
        Assert.Equal(10, review.TotalPointsEarned);

        var first = review.Logs[0];
        Assert.Equal(logId1, first.LogId);
        Assert.Equal(reviewId, first.ReviewId);
        Assert.Equal(new DateOnly(2026, 11, 19), first.LogDate);
        Assert.Null(first.StartTime);
        Assert.Equal("Jalan sore", first.Note);
        Assert.Equal("photos/u/1.jpg", first.PhotoPath);
        Assert.Equal(0.15m, first.Co2CoefficientUsed);
        Assert.Equal(1.0m, first.PointFactorUsed);

        var second = review.Logs[1];
        Assert.Equal(new TimeOnly(21, 0), second.StartTime);
        Assert.Equal(new TimeOnly(23, 30), second.EndTime);
    }

    [Fact]
    public void ToDomain_handles_review_with_zero_logs()
    {
        var userId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var json = new ReviewJson
        {
            ReviewId = reviewId,
            ReviewDate = "2026-11-19",
            TotalCo2SavedKg = 0m,
            TotalPointsEarned = 0,
            SubmittedAt = new DateTimeOffset(2026, 11, 19, 14, 0, 0, TimeSpan.Zero),
            Logs = []
        };

        var review = ReviewMapper.ToDomain(json, userId);

        Assert.Empty(review.Logs);
        Assert.Equal(0m, review.TotalCo2SavedKg);
        Assert.Equal(0, review.TotalPointsEarned);
    }

    [Fact]
    public void ToDailyTotalRow_and_ToCategoryTotalRow_map_expected_fields()
    {
        var daily = ReviewMapper.ToDailyTotalRow(new DailyTotalJson
        {
            ReviewDate = "2026-11-18",
            TotalCo2SavedKg = 1.1825m,
            TotalPointsEarned = 26
        });
        var category = ReviewMapper.ToCategoryTotalRow(new CategoryTotalJson
        {
            CategoryId = 2,
            CategoryName = "Energi",
            TotalCo2SavedKg = 0.1040m
        });

        Assert.Equal(new DateOnly(2026, 11, 18), daily.ReviewDate);
        Assert.Equal(1.1825m, daily.TotalCo2SavedKg);
        Assert.Equal(26, daily.TotalPointsEarned);
        Assert.Equal(2, category.CategoryId);
        Assert.Equal("Energi", category.CategoryName);
        Assert.Equal(0.1040m, category.TotalCo2SavedKg);
    }

    [Fact]
    public void ToHistoryRow_maps_all_18_columns_and_rejects_invalid_control_type()
    {
        var logId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var valid = new HistoryJson
        {
            LogId = logId,
            ReviewId = reviewId,
            LogDate = "2026-11-19",
            ActivityTypeId = 3,
            TypeName = "Mematikan Lampu Saat Tidak Dipakai",
            CategoryId = 2,
            CategoryName = "Energi",
            InputControlType = "TimeRange",
            UnitLabel = "jam",
            Quantity = 8m,
            StartTime = "22:00:00",
            EndTime = "06:00:00",
            Co2SavedKg = 0.104m,
            PointsEarned = 2,
            Co2CoefficientUsed = 0.013m,
            PointFactorUsed = 0.8m,
            Note = "Tidur malam",
            PhotoPath = "photos/a/b.png"
        };

        var row = ReviewMapper.ToHistoryRow(valid);

        Assert.Equal(logId, row.LogId);
        Assert.Equal(reviewId, row.ReviewId);
        Assert.Equal(new DateOnly(2026, 11, 19), row.LogDate);
        Assert.Equal(3, row.ActivityTypeId);
        Assert.Equal(InputControlType.TimeRange, row.InputControlType);
        Assert.Equal(new TimeOnly(22, 0), row.StartTime);
        Assert.Equal(new TimeOnly(6, 0), row.EndTime);
        Assert.Equal(0.104m, row.Co2SavedKg);
        Assert.Equal(2, row.PointsEarned);
        Assert.Equal("Tidur malam", row.Note);

        var invalid = new HistoryJson
        {
            LogId = logId,
            ReviewId = reviewId,
            LogDate = "2026-11-19",
            InputControlType = "timerange" // case-sensitive
        };

        var ex = Assert.Throws<DataAccessException>(() => ReviewMapper.ToHistoryRow(invalid));
        Assert.Equal(ErrorCode.Unknown, ex.Code);
    }

    [Fact]
    public void BackendRegistration_resolves_IDailyReviewRepository()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        Assert.IsType<SupabaseDailyReviewRepository>(sp.GetRequiredService<IDailyReviewRepository>());
    }
}
