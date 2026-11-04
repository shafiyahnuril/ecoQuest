using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Services.Fakes;
using Xunit;

namespace EcoQuest.Tests.Fakes;

/// <summary>Examples table of SRS section 4.4 (normative).</summary>
public class FakeImpactTests
{
    private static ActivityTypeDto Type(int id) => FakeData.ActivityTypes.Single(t => t.ActivityTypeId == id);

    private static ActivityImpactDto Run(int typeId, ActivityInput input) => FakeImpact.Build(input, Type(typeId));

    private static ActivityInput Qty(int id, decimal q) => ActivityInput.Empty(id) with { Quantity = q };

    private static ActivityInput Range(string start, string end) =>
        ActivityInput.Empty(3) with { StartTime = TimeOnly.Parse(start), EndTime = TimeOnly.Parse(end) };

    [Theory]
    [InlineData(1, 3.0, 0.4500, 9)]      // #1 walk 3 km
    [InlineData(1, 3.5, 0.5250, 11)]     // #2 10.5 rounds to 11, not 10 (AwayFromZero)
    [InlineData(2, 10.0, 0.9000, 18)]    // #3 public transport
    [InlineData(4, 2.0, 0.6000, 14)]     // #7 sorting waste
    [InlineData(6, 1.0, 0.1000, 2)]      // #9 shopping bag x1
    [InlineData(6, 3.0, 0.3000, 7)]      // #10 shopping bag x3
    public void Quantity_examples_match_srs(int typeId, double quantity, double co2, int points)
    {
        var impact = Run(typeId, Qty(typeId, (decimal)quantity));

        Assert.Equal((decimal)co2, impact.Co2SavedKg);
        Assert.Equal(points, impact.PointsEarned);
    }

    [Theory]
    [InlineData("21:00", "23:30", 2.5, 0.0325, 1)]       // #4 = 0.0325 kg (task example: 2 h 30 min)
    [InlineData("22:00", "06:00", 8.0, 0.1040, 2)]       // #5 crosses midnight
    [InlineData("23:50", "00:10", 0.3333, 0.0043, 0)]    // #6 20 minutes
    public void Time_range_examples_match_srs(string start, string end, double hours, double co2, int points)
    {
        var impact = Run(3, Range(start, end));

        Assert.Equal((decimal)hours, impact.Quantity);
        Assert.Equal((decimal)co2, impact.Co2SavedKg);
        Assert.Equal(points, impact.PointsEarned);
    }

    [Fact]
    public void Walking_3_km_is_0_45_kg_and_explains_the_formula()
    {
        var impact = Run(1, Qty(1, 3m));

        Assert.Equal(0.45m, impact.Co2SavedKg);
        Assert.Equal("3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", impact.FormulaExplanation);
        Assert.Equal("0,45 × 20 × 1,0 = 9 poin", impact.PointsExplanation);
    }

    [Fact]
    public void Toggle_yes_is_0_1_kg_and_2_points()   // #8
    {
        var impact = Run(5, ActivityInput.Empty(5) with { IsYes = true });

        Assert.Equal(0.1m, impact.Co2SavedKg);
        Assert.Equal(2, impact.PointsEarned);
    }

    [Fact]
    public void One_day_total_of_examples_1_4_7_8_is_1_1825_kg_and_26_points()   // sum row
    {
        var impacts = new[]
        {
            Run(1, Qty(1, 3m)), Run(3, Range("21:00", "23:30")), Run(4, Qty(4, 2m)),
            Run(5, ActivityInput.Empty(5) with { IsYes = true })
        };

        Assert.Equal(1.1825m, impacts.Sum(i => i.Co2SavedKg));
        Assert.Equal(26, impacts.Sum(i => i.PointsEarned));
    }

    [Fact]
    public void Reference_data_matches_the_sql_seed()
    {
        Assert.Equal(6, FakeData.ActivityTypes.Count);
        Assert.Equal(4, FakeData.Categories.Count);
        Assert.Equal(4, FakeData.Levels.Count);
        Assert.Equal(8, FakeData.Badges.Count);
        Assert.Equal([0.15m, 0.09m, 0.013m, 0.3m, 0.1m, 0.1m], FakeData.ActivityTypes.Select(t => t.Co2Coefficient));
        Assert.Equal([1.0m, 1.0m, 0.8m, 1.2m, 1.0m, 1.2m], FakeData.ActivityTypes.Select(t => t.PointFactor));
    }
}
