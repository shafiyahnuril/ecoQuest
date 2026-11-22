
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Rules;

/// <summary>
/// Menghasilkan insight edukatif berdasarkan aktivitas pengguna.
/// Seluruh perhitungan bersifat deterministik.
/// </summary>
public static class InsightGenerator
{
    /// <summary>
    /// Insight ketika belum ada refleksi dalam periode.
    /// </summary>
    public static InsightDto EmptyPeriod()
    {
        return new InsightDto(
            InsightKind.EmptyState,
            "Belum ada refleksi di periode ini. Isi Evening Review malam ini untuk melihat dampakmu."
        );
    }

    /// <summary>
    /// Insight kategori dengan kontribusi CO2 terbesar.
    /// </summary>
    public static InsightDto? TopCategory(
        IReadOnlyList<CategoryBreakdownDto> breakdown,
        string periodLabel)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(periodLabel);

        var top = breakdown
            .Where(category => category.Co2Kg > 0m)
            .OrderByDescending(category => category.Co2Kg)
            .ThenBy(category => category.CategoryId)
            .FirstOrDefault();

        if (top is null)
            return null;

        var co2 = top.Co2Kg.ToString(
            "0.###",
            CultureInfo.InvariantCulture);

        var percentage = top.Percentage.ToString(
            "0.#",
            CultureInfo.InvariantCulture);

        return new InsightDto(
            InsightKind.TopCategory,
            $"Kontribusi terbesarmu {periodLabel} berasal dari " +
            $"{top.CategoryName}: {co2} kg CO₂ ({percentage}%)."
        );
    }

    /// <summary>
    /// Insight kategori yang belum memiliki kontribusi CO2.
    /// Saran aktivitas dan estimasi dampak berasal dari
    /// data aktivitas serta koefisien yang valid.
    /// </summary>
    public static InsightDto? WeakestCategory(
        IReadOnlyList<CategoryBreakdownDto> breakdown,
        string activitySuggestion,
        decimal estimatedCo2Kg)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(activitySuggestion);

        if (estimatedCo2Kg < 0m)
            throw new ArgumentOutOfRangeException(nameof(estimatedCo2Kg));

        if (breakdown.Sum(category => category.Co2Kg) <= 0m)
            return null;

        var weakest = breakdown
            .Where(category => category.Co2Kg == 0m)
            .OrderBy(category => category.CategoryId)
            .FirstOrDefault();

        if (weakest is null)
            return null;

        var co2 = estimatedCo2Kg.ToString(
            "0.###",
            CultureInfo.InvariantCulture);

        return new InsightDto(
            InsightKind.WeakestCategory,
            $"Kategori {weakest.CategoryName} belum tercatat. " +
            $"Coba {activitySuggestion} — estimasi pengurangan {co2} kg CO₂."
        );
    }

    /// <summary>
    /// Membandingkan pengurangan CO2 minggu ini
    /// dengan minggu sebelumnya.
    /// </summary>
    public static InsightDto? WeekOverWeek(
        decimal currentWeekCo2Kg,
        decimal previousWeekCo2Kg)
    {
        if (currentWeekCo2Kg < 0m)
            throw new ArgumentOutOfRangeException(nameof(currentWeekCo2Kg));

        if (previousWeekCo2Kg < 0m)
            throw new ArgumentOutOfRangeException(nameof(previousWeekCo2Kg));

        if (previousWeekCo2Kg == 0m)
            return null;

        var direction = currentWeekCo2Kg > previousWeekCo2Kg
            ? "naik"
            : currentWeekCo2Kg < previousWeekCo2Kg
                ? "turun"
                : "tetap";

        if (direction == "tetap")
        {
            return new InsightDto(
                InsightKind.WeekOverWeek,
                "Dibanding 7 hari sebelumnya, CO₂ yang kamu hindari tetap sama."
            );
        }

        decimal changePercentage;

        try
        {
            changePercentage = currentWeekCo2Kg > previousWeekCo2Kg
                ? (currentWeekCo2Kg / previousWeekCo2Kg - 1m) * 100m
                : (1m - currentWeekCo2Kg / previousWeekCo2Kg) * 100m;
        }
        catch (OverflowException)
        {
            return new InsightDto(
                InsightKind.WeekOverWeek,
                "Dibanding 7 hari sebelumnya, CO₂ yang kamu hindari naik sangat besar."
            );
        }

        var percentage = changePercentage.ToString(
            "0.#",
            CultureInfo.InvariantCulture);

        return new InsightDto(
            InsightKind.WeekOverWeek,
            $"Dibanding 7 hari sebelumnya, CO₂ yang kamu hindari {direction} {percentage}%."
        );
    }

    /// <summary>
    /// Membandingkan pengurangan CO2 dengan estimasi kasar
    /// penyerapan satu pohon dewasa.
    /// </summary>
    public static InsightDto? Equivalence(decimal totalCo2Kg)
    {
        if (totalCo2Kg < 0m)
            throw new ArgumentOutOfRangeException(nameof(totalCo2Kg));

        if (totalCo2Kg < 0.5m)
            return null;

        var days = totalCo2Kg / InsightConstants.TreeKgPerDay;

        var formattedDays = Math.Round(
            days,
            1,
            MidpointRounding.AwayFromZero
        ).ToString("0.#", CultureInfo.InvariantCulture);

        return new InsightDto(
            InsightKind.Equivalence,
            $"Itu kira-kira setara serapan CO₂ satu pohon dewasa " +
            $"selama {formattedDays} hari (estimasi kasar)."
        );
    }

    /// <summary>
    /// Menghasilkan insight streak apabila pengguna
    /// memiliki streak efektif minimal 3 hari.
    /// </summary>
    public static InsightDto? Streak(int effectiveStreak)
    {
        if (effectiveStreak < 0)
            throw new ArgumentOutOfRangeException(nameof(effectiveStreak));

        if (effectiveStreak < 3)
            return null;

        return new InsightDto(
            InsightKind.Streak,
            $"Kamu sudah {effectiveStreak} hari berturut-turut berefleksi. " +
            "Konsistensi adalah kunci kebiasaan baru."
        );
    }
}
