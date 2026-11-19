
namespace EcoQuest.Core.Rules;

public sealed record LevelProgress(
    int LevelId,
    string LevelName,
    int MinPoints,
    int? MaxPoints,
    int CurrentPoints,
    string? NextLevelName,
    int? PointsToNext,
    double ProgressRatio
);
