
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Rules;

/// <summary>
/// Menyusun insight dashboard secara deterministik.
/// Prioritas: I1 EmptyState, I2 TopCategory,
/// I3 WeakestCategory, I4 WeekOverWeek,
/// I5 Equivalence, I6 Streak.
/// Maksimal tiga insight.
/// </summary>
public static class DashboardInsightRules
{
    private static readonly CultureInfo Id = new("id-ID");

    public static IReadOnlyList<(InsightKind Kind, string Text)> ForDashboard(
        IReadOnlyList<(int CategoryId, string CategoryName, decimal Co2)> breakdown,
        decimal periodTotal,
        decimal? previousTotal,
        int effectiveStreak,
        TrendRange range,
        IReadOnlyList<ActivityType> types,
        Func<ActivityType, decimal>? estimateCo2 = null)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(types);

        if (periodTotal <= 0m)
        {
            return
            [
                (
                    InsightKind.EmptyState,
                    "Belum ada refleksi di periode ini. Isi Evening Review malam ini untuk melihat dampakmu."
                )
            ];
        }

        var result = new List<(InsightKind Kind, string Text)>();

        // I2: Kategori dengan kontribusi terbesar.
        var top = breakdown
            .Where(c => c.Co2 > 0m)
            .OrderByDescending(c => c.Co2)
            .ThenBy(c => c.CategoryId)
            .FirstOrDefault();

        if (top.Co2 > 0m)
        {
            var percentage = Math.Round(
                top.Co2 / periodTotal * 100m,
                1,
                MidpointRounding.AwayFromZero);

            var periodLabel = range == TrendRange.Weekly
                ? "7 hari terakhir"
                : "30 hari terakhir";

            result.Add((
                InsightKind.TopCategory,
                $"Kontribusi terbesarmu {periodLabel} berasal dari " +
                $"{top.CategoryName}: {top.Co2.ToString("0.###", Id)} kg CO₂ " +
                $"({percentage.ToString("0.#", Id)}%)."
            ));
        }

        // I3: Kategori tanpa kontribusi.
        var weakest = breakdown
            .Where(c => c.Co2 == 0m)
            .OrderBy(c => c.CategoryId)
            .FirstOrDefault();

        if (weakest.CategoryName is not null)
        {
            var suggestion = types
                .Where(t => t.CategoryId == weakest.CategoryId)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.ActivityTypeId)
                .FirstOrDefault();

            if (suggestion is not null)
            {
                var text =
                    $"Kategori {weakest.CategoryName} belum tercatat. " +
                    $"Coba {suggestion.TypeName}";

                if (estimateCo2 is not null)
                {
                    var estimated = estimateCo2(suggestion);

                    if (estimated > 0m)
                    {
                        text +=
                            $" — estimasi pengurangan " +
                            $"{estimated.ToString("0.####", Id)} kg CO₂";
                    }
                }

                result.Add((InsightKind.WeakestCategory, text + "."));
            }
        }

        // I4: Perbandingan dengan minggu sebelumnya.
        if (range == TrendRange.Weekly && previousTotal is > 0m)
        {
            var change = (periodTotal - previousTotal.Value)
                / previousTotal.Value * 100m;

            string text;

            if (change == 0m)
            {
                text = "Dibanding 7 hari sebelumnya, CO₂ yang kamu hindari tetap sama.";
            }
            else
            {
                var direction = change > 0m ? "naik" : "turun";
                var percentage = Math.Abs(change).ToString("0.#", Id);

                text =
                    $"Dibanding 7 hari sebelumnya, CO₂ yang kamu hindari " +
                    $"{direction} {percentage}%.";
            }

            result.Add((InsightKind.WeekOverWeek, text));
        }

        // I5: Kesetaraan serapan pohon.
        if (periodTotal >= 0.5m)
        {
            var days = Math.Round(
                periodTotal / InsightConstants.TreeKgPerDay,
                1,
                MidpointRounding.AwayFromZero);

            result.Add((
                InsightKind.Equivalence,
                $"Itu kira-kira setara serapan CO₂ satu pohon dewasa " +
                $"selama {days.ToString("0.#", Id)} hari (estimasi kasar)."
            ));
        }

        // I6: Streak.
        if (effectiveStreak >= 3)
        {
            result.Add((
                InsightKind.Streak,
                $"Kamu sudah {effectiveStreak} hari berturut-turut berefleksi. " +
                "Konsistensi adalah kunci kebiasaan baru."
            ));
        }

        return result.Take(3).ToList();
    }
}
