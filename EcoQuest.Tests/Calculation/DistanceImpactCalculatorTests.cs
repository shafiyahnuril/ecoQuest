using EcoQuest.Core.Calculation;
using EcoQuest.Core.Domain;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Calculation;

public class DistanceImpactCalculatorTests
{
    private readonly DistanceImpactCalculator _sut = new();

    [Fact]
    public void Co2_3km_walk_is_0_45() =>
        Assert.Equal(0.4500m, _sut.CalculateCo2(3m, Walk().Co2Coefficient));

    [Fact]
    public void Points_3_5km_rounds_away_from_zero_to_11()
    {
        var co2 = _sut.CalculateCo2(3.5m, Walk().Co2Coefficient);
        Assert.Equal(0.5250m, co2);
        Assert.Equal(11, _sut.CalculatePoints(co2, Walk().PointFactor));
    }

    [Fact]
    public void Transit_10km_is_0_90_and_18_points()
    {
        var t = Transit();
        var co2 = _sut.CalculateCo2(10m, t.Co2Coefficient);
        Assert.Equal(0.9000m, co2);
        Assert.Equal(18, _sut.CalculatePoints(co2, t.PointFactor));
    }

    [Fact]
    public void Negative_is_error() =>
        Assert.Single(_sut.ValidateHard(Qty(Walk(), -1m), Walk()), i => i.Severity == ValidationSeverity.Error);

    [Fact]
    public void Three_decimals_is_error() =>
        Assert.Single(_sut.ValidateHard(Qty(Walk(), 1.234m), Walk()), i => i.Severity == ValidationSeverity.Error);

    [Fact]
    public void Above_hard_max_is_error() =>
        Assert.Single(_sut.ValidateHard(Qty(Walk(), 301m), Walk()), i => i.Severity == ValidationSeverity.Error);

    [Fact]
    public void Within_limits_has_no_issue() =>
        Assert.Empty(_sut.ValidateHard(Qty(Walk(), 3.5m), Walk()));
}
