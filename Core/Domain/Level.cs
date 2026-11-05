using System;

namespace EcoQuest.Core.Domain;

public sealed class Level
{
    public int LevelId { get; }
    public string LevelName { get; }
    public int MinPoints { get; }
    public int? MaxPoints { get; }          // null = level tertinggi (ADR-009)

    public Level(int levelId, string levelName, int minPoints, int? maxPoints)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            throw new ArgumentException("Nama level tidak boleh kosong.", nameof(levelName));
        if (minPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(minPoints));
        if (maxPoints is not null && maxPoints < minPoints)
            throw new ArgumentOutOfRangeException(nameof(maxPoints));

        LevelId = levelId;
        LevelName = levelName;
        MinPoints = minPoints;
        MaxPoints = maxPoints;
    }

    public bool IsPointInRange(int points)
        => points >= MinPoints && (MaxPoints is null || points <= MaxPoints);
}
