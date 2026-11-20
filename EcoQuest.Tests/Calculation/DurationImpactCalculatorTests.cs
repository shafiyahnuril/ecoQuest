using EcoQuest.Core.Calculation;
using EcoQuest.Core.Domain;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Calculation;

public class DurationImpactCalculatorTests
{
    private readonly DurationImpactCalculator _sut = new();

    [Fact]
    public void Hours_21_00_to_23_30_is_2_5() =>
        Assert.Equal(2.5m, DurationImpactCalculator.Hours(new TimeOnly(21, 0), new TimeOnly(23, 30)));

    [Fact]
    public void Crosses_midnight_22_to_06_is_8() =>
        Assert.Equal(8m, DurationImpactCalculator.Hours(new TimeOnly(22, 0), new TimeOnly(6, 0)));

    [Fact]
    public void Crosses_midnight_23_50_to_00_10_is_0_3333() =>
        Assert.Equal(0.3333m, DurationImpactCalculator.Hours(new TimeOnly(23, 50), new TimeOnly(0, 10)));

    [Fact]
    public void Equal_start_end_is_error()
    {
        var input = Time(Lamp(), new TimeOnly(20, 0), new TimeOnly(20, 0));
        Assert.Single(_sut.ValidateHard(input, Lamp()), i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Only_start_filled_is_error()
    {
        var input = Time(Lamp(), new TimeOnly(20, 0), null);
        Assert.Single(_sut.ValidateHard(input, Lamp()), i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Nothing_filled_is_valid_not_done() =>
        Assert.Empty(_sut.ValidateHard(Time(Lamp(), null, null), Lamp()));

    [Fact]
    public void Co2_lamp_2_5h_is_0_0325_and_1_point()
    {
        var lamp = Lamp();
        var co2 = _sut.CalculateCo2(2.5m, lamp.Co2Coefficient);
        Assert.Equal(0.0325m, co2);
        Assert.Equal(1, _sut.CalculatePoints(co2, lamp.PointFactor));
    }
}
