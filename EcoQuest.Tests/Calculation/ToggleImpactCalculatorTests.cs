using EcoQuest.Core.Calculation;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Calculation;

public class ToggleImpactCalculatorTests
{
    private readonly ToggleImpactCalculator _sut = new();

    [Fact]
    public void Yes_is_quantity_1_and_0_1kg_2_points()
    {
        var t = Tumbler();
        var qty = _sut.ResolveQuantity(Yes(t, true));
        var co2 = _sut.CalculateCo2(qty, t.Co2Coefficient);
        Assert.Equal(1m, qty);
        Assert.Equal(0.1000m, co2);
        Assert.Equal(2, _sut.CalculatePoints(co2, t.PointFactor));
    }

    [Fact]
    public void No_or_null_is_quantity_0()
    {
        var t = Tumbler();
        Assert.Equal(0m, _sut.ResolveQuantity(Yes(t, false)));
        Assert.Equal(0m, _sut.ResolveQuantity(Yes(t, null)));
    }
}
