using EcoQuest.Core.Calculation;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Calculation;

public class ImpactCalculatorBaseTests
{
    private readonly DistanceImpactCalculator _sut = new();

    [Fact]
    public void Co2_rounded_to_4_decimals_away_from_zero() =>
        Assert.Equal(0.0001m, _sut.CalculateCo2(1m, 0.00005m));

    [Fact]
    public void Points_use_midpoint_away_from_zero() =>
        Assert.Equal(11, _sut.CalculatePoints(0.525m, 1.0m));

    [Fact]
    public void DescribeFormula_uses_id_ID_decimal_comma()
    {
        var text = _sut.DescribeFormula(3m, Walk());
        Assert.Equal("3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", text);
    }

    [Fact]
    public void DescribePoints_uses_id_ID_decimal_comma() =>
        Assert.Equal("0,45 × 20 × 1,0 = 9 poin", _sut.DescribePoints(0.45m, Walk()));

    [Fact]
    public void Negative_quantity_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.CalculateCo2(-1m, 0.15m));
}
