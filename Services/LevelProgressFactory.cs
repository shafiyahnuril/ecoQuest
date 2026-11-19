
using System;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Rules;

namespace EcoQuest.Services;

public static class LevelProgressFactory
{
    public static LevelProgressDto ToDto(LevelProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new LevelProgressDto(
            progress.LevelId,
            progress.LevelName,
            progress.MinPoints,
            progress.MaxPoints,
            progress.CurrentPoints,
            progress.NextLevelName,
            progress.PointsToNext,
            progress.ProgressRatio
        );
    }
}
