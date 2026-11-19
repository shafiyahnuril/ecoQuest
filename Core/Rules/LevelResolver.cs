
using System;
using System.Collections.Generic;
using System.Linq;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Rules;

public static class LevelResolver
{
    public static Level Resolve(
        int totalPoints,
        IReadOnlyList<Level> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (totalPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(totalPoints));

        if (levels.Count == 0)
            throw new ArgumentException(
                "Daftar level tidak boleh kosong.", nameof(levels));

        return levels
            .OrderByDescending(l => l.MinPoints)
            .FirstOrDefault(l => l.IsPointInRange(totalPoints))
            ?? throw new InvalidOperationException(
                "Tidak ditemukan level untuk total poin.");
    }

    public static LevelProgress Progress(
        int totalPoints,
        IReadOnlyList<Level> levels)
    {
        var current = Resolve(totalPoints, levels);

        var next = levels
            .Where(l => l.MinPoints > current.MinPoints)
            .OrderBy(l => l.MinPoints)
            .FirstOrDefault();

        if (next is null)
        {
            return new LevelProgress(
                current.LevelId,
                current.LevelName,
                current.MinPoints,
                current.MaxPoints,
                totalPoints,
                null,
                null,
                1.0
            );
        }

        var pointsToNext = Math.Max(0, next.MinPoints - totalPoints);

        var progressRatio = Math.Clamp(
            (double)(totalPoints - current.MinPoints) /
            (next.MinPoints - current.MinPoints),
            0.0,
            1.0
        );

        return new LevelProgress(
            current.LevelId,
            current.LevelName,
            current.MinPoints,
            current.MaxPoints,
            totalPoints,
            next.LevelName,
            pointsToNext,
            progressRatio
        );
    }
}
