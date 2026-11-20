using EcoQuest.Core.Calculation;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Calculation;

public class ImpactCalculationServiceTests
{
    private static readonly ImpactCalculationService Sut = new(new ImpactCalculatorFactory(new IImpactCalculator[]
    {
        new DistanceImpactCalculator(), new DurationImpactCalculator(),
        new CountImpactCalculator(), new ToggleImpactCalculator()
    }));

    // Tabel SRS 4.4: (no, tipe, input, qty, co2, poin)
    public static IEnumerable<object[]> SrsRows()
    {
        yield return new object[] { 1, Walk(), Qty(Walk(), 3m), 3m, 0.4500m, 9 };
        yield return new object[] { 2, Walk(), Qty(Walk(), 3.5m), 3.5m, 0.5250m, 11 };
        yield return new object[] { 3, Transit(), Qty(Transit(), 10m), 10m, 0.9000m, 18 };
        yield return new object[] { 4, Lamp(), Time(Lamp(), new TimeOnly(21, 0), new TimeOnly(23, 30)), 2.5m, 0.0325m, 1 };
        yield return new object[] { 5, Lamp(), Time(Lamp(), new TimeOnly(22, 0), new TimeOnly(6, 0)), 8m, 0.1040m, 2 };
        yield return new object[] { 6, Lamp(), Time(Lamp(), new TimeOnly(23, 50), new TimeOnly(0, 10)), 0.3333m, 0.0043m, 0 };
        yield return new object[] { 7, Sorting(), Qty(Sorting(), 2m), 2m, 0.6000m, 14 };
        yield return new object[] { 8, Tumbler(), Yes(Tumbler(), true), 1m, 0.1000m, 2 };
        yield return new object[] { 9, Bags(), Qty(Bags(), 1m), 1m, 0.1000m, 2 };
        yield return new object[] { 10, Bags(), Qty(Bags(), 3m), 3m, 0.3000m, 7 };
    }

    [Theory]
    [MemberData(nameof(SrsRows))]
    public void Matches_SRS_4_4_table(int no, ActivityType type, ActivityInput input, decimal qty, decimal co2, int points)
    {
        Assert.InRange(no, 1, int.MaxValue);
        var r = Sut.Calculate(input, type);
        Assert.Equal(qty, r.Quantity);
        Assert.Equal(co2, r.Co2SavedKg);
        Assert.Equal(points, r.PointsEarned);
        Assert.Equal(type.Co2Coefficient, r.Co2CoefficientUsed);
        Assert.Equal(type.PointFactor, r.PointFactorUsed);
    }

    [Fact]
    public void Sum_of_rows_1_4_7_8_is_1_1825_kg_and_26_points()
    {
        var results = new[]
        {
            Sut.Calculate(Qty(Walk(), 3m), Walk()),
            Sut.Calculate(Time(Lamp(), new TimeOnly(21, 0), new TimeOnly(23, 30)), Lamp()),
            Sut.Calculate(Qty(Sorting(), 2m), Sorting()),
            Sut.Calculate(Yes(Tumbler(), true), Tumbler())
        };
        Assert.Equal(1.1825m, results.Sum(r => r.Co2SavedKg));
        Assert.Equal(26, results.Sum(r => r.PointsEarned));
    }

    [Fact]
    public void Time_range_result_keeps_start_and_end_but_others_do_not()
    {
        var lamp = Sut.Calculate(Time(Lamp(), new TimeOnly(21, 0), new TimeOnly(23, 30)), Lamp());
        var walk = Sut.Calculate(Qty(Walk(), 3m), Walk());
        Assert.Equal(new TimeOnly(21, 0), lamp.StartTime);
        Assert.Equal(new TimeOnly(23, 30), lamp.EndTime);
        Assert.Null(walk.StartTime);
        Assert.Null(walk.EndTime);
    }

    [Fact]
    public void Not_done_is_zero_co2_and_zero_points()
    {
        var r = Sut.Calculate(Qty(Walk(), null), Walk());
        Assert.Equal(0m, r.Co2SavedKg);
        Assert.Equal(0, r.PointsEarned);
    }
}
