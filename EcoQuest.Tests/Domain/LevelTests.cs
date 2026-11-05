using EcoQuest.Core.Domain;
using Xunit;

namespace EcoQuest.Tests.Domain;

public class LevelTests
{
    [Fact]
    public void IsPointInRange_null_max()
    {
        var champion = new Level(4, "Eco Champion", 1500, null);
        Assert.True(champion.IsPointInRange(999999));
        Assert.False(champion.IsPointInRange(1499));
    }

    [Fact]
    public void IsPointInRange_bounded_level()
    {
        var starter = new Level(1, "Eco Starter", 0, 99);
        Assert.True(starter.IsPointInRange(0));
        Assert.True(starter.IsPointInRange(99));
        Assert.False(starter.IsPointInRange(100));
    }
}
