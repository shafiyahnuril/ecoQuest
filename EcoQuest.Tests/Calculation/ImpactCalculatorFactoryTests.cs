using EcoQuest.Core.Calculation;
using EcoQuest.Core.Domain;
using Xunit;

namespace EcoQuest.Tests.Calculation;

public class ImpactCalculatorFactoryTests
{
    private static ImpactCalculatorFactory Full() => new(new IImpactCalculator[]
    {
        new DistanceImpactCalculator(), new DurationImpactCalculator(),
        new CountImpactCalculator(), new ToggleImpactCalculator()
    });

    [Theory]
    [InlineData(InputControlType.Numeric)]
    [InlineData(InputControlType.TimeRange)]
    [InlineData(InputControlType.Count)]
    [InlineData(InputControlType.Toggle)]
    public void Returns_calculator_for_each_InputControlType(InputControlType type) =>
        Assert.Equal(type, Full().For(type).SupportedType);

    [Fact]
    public void Throws_for_unregistered_type()
    {
        var factory = new ImpactCalculatorFactory(new IImpactCalculator[] { new DistanceImpactCalculator() });
        Assert.Throws<NotSupportedException>(() => factory.For(InputControlType.Toggle));
    }
}
