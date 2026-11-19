
using System;
using System.Collections.Generic;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using Xunit;

namespace EcoQuest.Tests.Rules;

public class LevelResolverTests
{
    private static readonly IReadOnlyList<Level> Levels =
        new List<Level>
        {
            new(1, "Pemula", 0, 99),
            new(2, "Aktif", 100, 299),
            new(3, "Juara", 300, null)
        };

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    [InlineData(500, 3)]
    public void Resolve_ReturnsCorrectLevel(
        int points, int expectedLevelId)
    {
        var result = LevelResolver.Resolve(points, Levels);

        Assert.Equal(expectedLevelId, result.LevelId);
    }

    [Fact]
    public void Progress_CalculatesCorrectRatio()
    {
        var result = LevelResolver.Progress(150, Levels);

        Assert.Equal(2, result.LevelId);
        Assert.Equal("Juara", result.NextLevelName);
        Assert.Equal(150, result.PointsToNext);
        Assert.Equal(0.25, result.ProgressRatio, 5);
    }

    [Fact]
    public void Progress_HighestLevel_ReturnsFullProgress()
    {
        var result = LevelResolver.Progress(350, Levels);

        Assert.Equal(3, result.LevelId);
        Assert.Null(result.NextLevelName);
        Assert.Null(result.PointsToNext);
        Assert.Equal(1.0, result.ProgressRatio);
    }

    [Fact]
    public void Resolve_EmptyLevels_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            LevelResolver.Resolve(100, Array.Empty<Level>()));
    }

    [Fact]
    public void Resolve_NegativePoints_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelResolver.Resolve(-1, Levels));
    }
}
