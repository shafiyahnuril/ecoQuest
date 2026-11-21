using System;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Rules;

public class WeatherSuggestionRulesTests
{
    private static readonly DateOnly Date = new(2026, 11, 10);

    private static WeatherSuggestionRules NewRules() =>
        new(new ImpactCalculationService(new ImpactCalculatorFactory(new IImpactCalculator[]
        {
            new DistanceImpactCalculator(), new DurationImpactCalculator(),
            new CountImpactCalculator(), new ToggleImpactCalculator()
        })));

    private static ActivityType[] Types(ActivityType? transit = null) =>
        new[] { Walk(), transit ?? Transit(), Lamp(), Sorting(), Tumbler(), Bags() };

    [Fact]
    public void Rain_suggests_transit_with_db_coefficient()
    {
        var rules = NewRules();

        var seed = rules.FromForecast("Yogyakarta", Date, "Hujan", 23m, 29m, true, 18, Types());
        Assert.False(seed.IsFallback);
        Assert.Equal(2, seed.SuggestedActivityTypeId);
        Assert.Equal(0.90m, seed.EstimatedCo2Kg);
        Assert.Contains("±0,90 kg CO₂", seed.SuggestionText);

        var changed = new ActivityType(2, "Transportasi Umum", 1, InputControlType.Numeric, "km",
            0.20m, 1.0m, 150m, 1000m, null, 2);
        var other = rules.FromForecast("Yogyakarta", Date, "Hujan", 23m, 29m, true, 18, Types(changed));
        Assert.Equal(2.00m, other.EstimatedCo2Kg);
        Assert.Contains("±2,00 kg CO₂", other.SuggestionText);
    }

    [Fact]
    public void Hot_suggests_tumbler()
    {
        var r = NewRules().FromForecast("Yogyakarta", Date, "Cerah", 25m, 34m, false, 1, Types());

        Assert.False(r.IsFallback);
        Assert.Equal(5, r.SuggestedActivityTypeId);
        Assert.Equal(0.10m, r.EstimatedCo2Kg);
        Assert.Contains("34°C", r.SuggestionText);
    }

    [Fact]
    public void Mild_suggests_walking()
    {
        var r = NewRules().FromForecast("Yogyakarta", Date, "Cerah berawan", 24m, 31m, false, 4, Types());

        Assert.False(r.IsFallback);
        Assert.Equal(1, r.SuggestedActivityTypeId);
        Assert.Equal(0.30m, r.EstimatedCo2Kg);
        Assert.Equal("Cerah berawan, 24–31°C", r.ForecastSummary);
    }

    [Fact]
    public void Fallback_rotates_by_day_of_year()
    {
        var rules = NewRules();

        var d0 = rules.Fallback("Yogyakarta", new DateOnly(2026, 1, 3), Types());   // DayOfYear 3 → 3 % 3 = 0
        var d1 = rules.Fallback("Yogyakarta", new DateOnly(2026, 1, 1), Types());   // 1 % 3 = 1
        var d2 = rules.Fallback("Yogyakarta", new DateOnly(2026, 1, 2), Types());   // 2 % 3 = 2

        Assert.All(new[] { d0, d1, d2 }, r =>
        {
            Assert.True(r.IsFallback);
            Assert.Null(r.ForecastSummary);
        });
        Assert.Equal(4, d0.SuggestedActivityTypeId);
        Assert.Equal(0.60m, d0.EstimatedCo2Kg);
        Assert.Equal(6, d1.SuggestedActivityTypeId);
        Assert.Equal(0.10m, d1.EstimatedCo2Kg);
        Assert.Equal(3, d2.SuggestedActivityTypeId);
        Assert.Equal(0.039m, d2.EstimatedCo2Kg);
    }
}
