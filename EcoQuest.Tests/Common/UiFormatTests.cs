using EcoQuest.Core.Common;
using Xunit;

namespace EcoQuest.Tests.Common;

/// <summary>UiFormat (UI-UX-Flow-Frontend 2.7): id-ID text for CO₂, counts and dates.</summary>
public class UiFormatTests
{
    [Theory]
    [InlineData("0.45", "0,45 kg CO₂")]
    [InlineData("9.45", "9,45 kg CO₂")]
    [InlineData("12", "12,00 kg CO₂")]
    [InlineData("0.1", "0,10 kg CO₂")]
    [InlineData("0.099", "0,099 kg CO₂")]
    [InlineData("0.033", "0,033 kg CO₂")]
    [InlineData("0", "0 kg CO₂")]
    [InlineData("1250.5", "1.250,50 kg CO₂")]
    public void Co2_uses_two_decimals_three_below_0_1_and_a_bare_zero(string kg, string expected) =>
        Assert.Equal(expected, UiFormat.Co2(decimal.Parse(kg, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Co2_rounds_half_away_from_zero()
    {
        Assert.Equal("0,13 kg CO₂", UiFormat.Co2(0.125m));      // banker's rounding would give 0,12
        Assert.Equal("0,045 kg CO₂", UiFormat.Co2(0.0445m));    // 0.0445 -> 3 decimals: 0.045 (away from zero)
    }

    [Fact]
    public void Kg_omits_the_gas_name_for_metric_cards()
    {
        Assert.Equal("9,45 kg", UiFormat.Kg(9.45m));
        Assert.Equal("0 kg", UiFormat.Kg(0m));
    }

    [Fact]
    public void Count_groups_thousands() => Assert.Equal("1.250", UiFormat.Count(1250));

    [Fact]
    public void Dates_use_indonesian_names()
    {
        var date = new DateOnly(2026, 10, 24);   // a Saturday
        Assert.Equal("Sabtu, 24 Oktober 2026", UiFormat.LongDate(date));
        Assert.Equal("24 Okt 2026", UiFormat.Date(date));
    }
}
