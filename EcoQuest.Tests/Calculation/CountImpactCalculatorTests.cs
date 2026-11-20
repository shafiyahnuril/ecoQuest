using EcoQuest.Core.Calculation;
using EcoQuest.Core.Domain;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Calculation;

public class CountImpactCalculatorTests
{
    private readonly CountImpactCalculator _sut = new();

    [Fact]
    public void Sorting_2_is_0_6_and_14_points()
    {
        var t = Sorting();
        var co2 = _sut.CalculateCo2(2m, t.Co2Coefficient);
        Assert.Equal(0.6000m, co2);
        Assert.Equal(14, _sut.CalculatePoints(co2, t.PointFactor));
    }

    [Fact]
    public void Bags_3_is_7_points()
    {
        var t = Bags();
        var co2 = _sut.CalculateCo2(3m, t.Co2Coefficient);
        Assert.Equal(7, _sut.CalculatePoints(co2, t.PointFactor));
    }

    [Fact]
    public void Decimal_count_is_error() =>
        Assert.Single(_sut.ValidateHard(Qty(Bags(), 1.5m), Bags()), i => i.Severity == ValidationSeverity.Error);

    [Fact]
    public void Above_hard_is_error() =>
        Assert.Single(_sut.ValidateHard(Qty(Sorting(), 51m), Sorting()), i => i.Severity == ValidationSeverity.Error);
}
