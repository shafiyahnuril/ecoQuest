using System;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Services;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Services;

public class DtoFactoryTests
{
    private static readonly DtoFactory Sut = new(new ImpactCalculatorFactory(new IImpactCalculator[]
    {
        new DistanceImpactCalculator(),
        new DurationImpactCalculator(),
        new CountImpactCalculator(),
        new ToggleImpactCalculator()
    }));

    [Fact]
    public void ToDomain_maps_all_activity_type_fields()
    {
        var dto = new ActivityTypeDto(
            1,
            "Jalan Kaki / Bersepeda",
            1,
            "Transportasi",
            InputControlType.Numeric,
            "km",
            0.15m,
            1.0m,
            30m,
            300m,
            "Deskripsi",
            1);

        var domain = Sut.ToDomain(dto);

        Assert.Equal(dto.ActivityTypeId, domain.ActivityTypeId);
        Assert.Equal(dto.TypeName, domain.TypeName);
        Assert.Equal(dto.CategoryId, domain.CategoryId);
        Assert.Equal(dto.InputControlType, domain.InputControlType);
        Assert.Equal(dto.UnitLabel, domain.UnitLabel);
        Assert.Equal(dto.Co2Coefficient, domain.Co2Coefficient);
        Assert.Equal(dto.PointFactor, domain.PointFactor);
        Assert.Equal(dto.PlausibleMaxPerDay, domain.PlausibleMaxPerDay);
        Assert.Equal(dto.HardMaxPerDay, domain.HardMaxPerDay);
        Assert.Equal(dto.Description, domain.Description);
        Assert.Equal(dto.DisplayOrder, domain.DisplayOrder);
    }

    [Fact]
    public void Impact_for_saved_log_uses_historical_snapshot_coefficients()
    {
        var userId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 20);

        // Log lama memakai koefisien 0.15m dan pointFactor 1.0m, sedangkan tipe saat ini berubah ke 0.25m & 1.2m.
        var log = ActivityLog.Rehydrate(
            logId, userId, reviewId, 1, date,
            3m, null, null, "Jalan pagi", "photos/a.png",
            0.45m, 9, 0.15m, 1.0m);

        var currentTypeWithChangedCoeff = new ActivityType(
            1, "Jalan Kaki / Bersepeda", 1, InputControlType.Numeric, "km",
            0.25m, 1.2m, 30m, 300m, null, 1);

        var dto = Sut.Impact(log, currentTypeWithChangedCoeff, "Transportasi");

        Assert.Equal(logId, dto.LogId);
        Assert.Equal("Transportasi", dto.CategoryName);
        Assert.Equal(0.15m, dto.Co2CoefficientUsed);
        Assert.Equal(1.0m, dto.PointFactorUsed);
        Assert.Equal("3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", dto.FormulaExplanation);
        Assert.Equal("0,45 × 20 × 1,0 = 9 poin", dto.PointsExplanation);
        Assert.Equal("Jalan pagi", dto.Note);
        Assert.Equal("photos/a.png", dto.PhotoPath);
    }

    [Fact]
    public void Impact_for_preview_returns_null_LogId_and_metadata()
    {
        var type = Walk();
        var result = new ImpactResult(1, 3m, null, null, 0.45m, 9, 0.15m, 1.0m);

        var dto = Sut.Impact(result, type, "Transportasi");

        Assert.Null(dto.LogId);
        Assert.Null(dto.Note);
        Assert.Null(dto.PhotoPath);
        Assert.Equal("Transportasi", dto.CategoryName);
        Assert.Equal("3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", dto.FormulaExplanation);
        Assert.Equal("0,45 × 20 × 1,0 = 9 poin", dto.PointsExplanation);
    }

    [Fact]
    public void Impact_for_history_row_uses_snapshot_coefficients_with_id_ID_format()
    {
        var logId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var row = new HistoryRow(
            logId,
            reviewId,
            new DateOnly(2026, 11, 18),
            1,
            "Jalan Kaki / Bersepeda",
            1,
            "Transportasi",
            InputControlType.Numeric,
            "km",
            3m,
            null,
            null,
            0.45m,
            9,
            0.15m,
            1.0m,
            "Catatan",
            "photos/x.jpg");

        var dto = Sut.Impact(row);

        Assert.Equal(logId, dto.LogId);
        Assert.Equal("Transportasi", dto.CategoryName);
        Assert.Equal("3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", dto.FormulaExplanation);
        Assert.Equal("0,45 × 20 × 1,0 = 9 poin", dto.PointsExplanation);
        Assert.Equal("Catatan", dto.Note);
        Assert.Equal("photos/x.jpg", dto.PhotoPath);
    }

    [Theory]
    [InlineData(CriteriaType.ActivityCount, 12)]
    [InlineData(CriteriaType.StreakDays, 7)]
    [InlineData(CriteriaType.CO2SavedThreshold, 14.5)]
    [InlineData(CriteriaType.PointsThreshold, 280)]
    public void Badge_maps_CurrentValue_for_all_criteria_types(CriteriaType criteriaType, decimal expectedCurrent)
    {
        var badge = new Badge(1, "Lencana", "Deskripsi", criteriaType, 10m);
        var ctx = new BadgeContext(LogCount: 12, Streak: 7, TotalCo2: 14.5m, TotalPoints: 280);
        var earnedAt = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        var dto = Sut.Badge(badge, earned: true, earnedAt, ctx);

        Assert.True(dto.IsEarned);
        Assert.Equal(earnedAt, dto.EarnedAt);
        Assert.Equal(expectedCurrent, dto.CurrentValue);
    }
}
