using System;
using System.Globalization;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;

namespace EcoQuest.Services;

/// <summary>
/// Memetakan entitas domain, hasil kalkulasi, dan baris riwayat menjadi DTO kontrak UI (ADR-010 snapshot).
/// </summary>
public sealed class DtoFactory
{
    private static readonly CultureInfo IdCulture = new("id-ID");

    private readonly IImpactCalculatorFactory _calculators;

    public DtoFactory(IImpactCalculatorFactory calculators)
    {
        _calculators = calculators ?? throw new ArgumentNullException(nameof(calculators));
    }

    public ActivityType ToDomain(ActivityTypeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ActivityType(
            dto.ActivityTypeId,
            dto.TypeName,
            dto.CategoryId,
            dto.InputControlType,
            dto.UnitLabel,
            dto.Co2Coefficient,
            dto.PointFactor,
            dto.PlausibleMaxPerDay,
            dto.HardMaxPerDay,
            dto.Description,
            dto.DisplayOrder);
    }

    public ActivityImpactDto Impact(ActivityLog log, ActivityType currentType, string categoryName)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(currentType);

        // ADR-010: penjelasan rumus untuk log tersimpan wajib memakai koefisien snapshot historis.
        var snapshotType = CreateSnapshotType(currentType, log.Co2CoefficientUsed, log.PointFactorUsed);
        var (formula, points) = BuildExplanations(snapshotType, log.Quantity, log.Co2SavedKg, log.PointsEarned);

        return new ActivityImpactDto(
            log.LogId,
            currentType.ActivityTypeId,
            currentType.TypeName,
            currentType.CategoryId,
            categoryName ?? string.Empty,
            currentType.InputControlType,
            log.Quantity,
            currentType.UnitLabel,
            log.StartTime,
            log.EndTime,
            log.Co2SavedKg,
            log.PointsEarned,
            log.Co2CoefficientUsed,
            log.PointFactorUsed,
            formula,
            points,
            log.Note,
            log.PhotoPath);
    }

    public ActivityImpactDto Impact(ImpactResult result, ActivityType type, string categoryName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(type);

        var snapshotType = CreateSnapshotType(type, result.Co2CoefficientUsed, result.PointFactorUsed);
        var (formula, points) = BuildExplanations(snapshotType, result.Quantity, result.Co2SavedKg, result.PointsEarned);

        return new ActivityImpactDto(
            null,
            type.ActivityTypeId,
            type.TypeName,
            type.CategoryId,
            categoryName ?? string.Empty,
            type.InputControlType,
            result.Quantity,
            type.UnitLabel,
            result.StartTime,
            result.EndTime,
            result.Co2SavedKg,
            result.PointsEarned,
            result.Co2CoefficientUsed,
            result.PointFactorUsed,
            formula,
            points,
            null,
            null);
    }

    public ActivityImpactDto Impact(HistoryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var snapshotType = new ActivityType(
            row.ActivityTypeId,
            row.TypeName,
            row.CategoryId,
            row.InputControlType,
            row.UnitLabel,
            row.Co2CoefficientUsed,
            row.PointFactorUsed,
            null,
            Math.Max(1m, row.Quantity),
            null,
            0);

        var (formula, points) = BuildExplanations(snapshotType, row.Quantity, row.Co2SavedKg, row.PointsEarned);

        return new ActivityImpactDto(
            row.LogId,
            row.ActivityTypeId,
            row.TypeName,
            row.CategoryId,
            row.CategoryName,
            row.InputControlType,
            row.Quantity,
            row.UnitLabel,
            row.StartTime,
            row.EndTime,
            row.Co2SavedKg,
            row.PointsEarned,
            row.Co2CoefficientUsed,
            row.PointFactorUsed,
            formula,
            points,
            row.Note,
            row.PhotoPath);
    }

    public BadgeDto Badge(Badge badge, bool earned, DateTimeOffset? earnedAt, BadgeContext ctx)
    {
        ArgumentNullException.ThrowIfNull(badge);
        ArgumentNullException.ThrowIfNull(ctx);

        var currentValue = badge.CriteriaType switch
        {
            CriteriaType.ActivityCount => ctx.LogCount,
            CriteriaType.StreakDays => ctx.Streak,
            CriteriaType.CO2SavedThreshold => ctx.TotalCo2,
            CriteriaType.PointsThreshold => ctx.TotalPoints,
            _ => 0m
        };

        return new BadgeDto(
            badge.BadgeId,
            badge.Name,
            badge.Description,
            badge.CriteriaType,
            badge.CriteriaValue,
            earned,
            earned ? earnedAt : null,
            currentValue);
    }

    private (string Formula, string Points) BuildExplanations(
        ActivityType snapshotType,
        decimal quantity,
        decimal co2SavedKg,
        int pointsEarned)
    {
        var calc = _calculators.For(snapshotType.InputControlType);
        var formula = calc.DescribeFormula(quantity, snapshotType);
        var points = calc is ImpactCalculatorBase baseCalc
            ? baseCalc.DescribePoints(co2SavedKg, snapshotType)
            : FormatPointsFallback(co2SavedKg, snapshotType.PointFactor, pointsEarned);
        return (formula, points);
    }

    private static ActivityType CreateSnapshotType(ActivityType t, decimal co2Coefficient, decimal pointFactor)
        => new(
            t.ActivityTypeId,
            t.TypeName,
            t.CategoryId,
            t.InputControlType,
            t.UnitLabel,
            co2Coefficient,
            pointFactor,
            t.PlausibleMaxPerDay,
            t.HardMaxPerDay,
            t.Description,
            t.DisplayOrder);

    private static string FormatPointsFallback(decimal co2SavedKg, decimal pointFactor, int pointsEarned)
        => $"{co2SavedKg.ToString("0.####", IdCulture)} × {ImpactConstants.PointsPerKg.ToString("0", IdCulture)} × {pointFactor.ToString("0.0", IdCulture)} = {pointsEarned} poin";
}
