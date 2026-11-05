using System.Globalization;
using EcoQuest.Controls;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class NumericInputParserTests
{
    private static decimal? Dec(string? text) => text is null ? null : decimal.Parse(text, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("3,5", "3.5")]
    [InlineData("3.5", "3.5")]
    [InlineData(" 3,5 ", "3.5")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("-1", "-1")]
    [InlineData("1.000", "1")]
    [InlineData("3.500", "3.5")]
    [InlineData("3,", "3")]
    [InlineData(",5", "0.5")]
    [InlineData("3,456", "3.46")]
    [InlineData("3,455", "3.46")]
    [InlineData("2,345", "2.35")]
    public void Decimal_mode_parses_valid_text(string? text, string? expected)
    {
        Assert.True(NumericInputParser.TryParse(text, isInteger: false, out var value));
        Assert.Equal(Dec(expected), value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData("1.000.5")]
    [InlineData("1,2,3")]
    [InlineData("3,5kg")]
    [InlineData("٣")]
    public void Invalid_text_is_rejected(string text)
    {
        Assert.False(NumericInputParser.TryParse(text, isInteger: false, out var value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("7", "7")]
    [InlineData("3,5", "3.5")]
    [InlineData("-2", "-2")]
    [InlineData("", null)]
    public void Integer_mode_passes_the_value_through_for_the_service_to_judge(string text, string? expected)
    {
        Assert.True(NumericInputParser.TryParse(text, isInteger: true, out var value));
        Assert.Equal(Dec(expected), value);
    }

    [Theory]
    [InlineData(null, false, "")]
    [InlineData("3.5", false, "3,50")]
    [InlineData("3", false, "3,00")]
    [InlineData("3.455", false, "3,46")]
    [InlineData("0", false, "0,00")]
    [InlineData("3", true, "3")]
    [InlineData("3.5", true, "3,5")]
    [InlineData(null, true, "")]
    public void Format_uses_id_ID_text(string? value, bool isInteger, string expected)
    {
        Assert.Equal(expected, NumericInputParser.Format(Dec(value), isInteger));
    }

    [Theory]
    [InlineData(null, 1, false, "0.5")]
    [InlineData("2", 1, false, "2.5")]
    [InlineData("2", -1, false, "1.5")]
    [InlineData("0.5", -1, false, "0")]
    [InlineData("0.3", -1, false, "0")]
    [InlineData("0", -1, false, "0")]
    [InlineData(null, -1, false, "0")]
    [InlineData("-1", -1, false, "-1")]
    [InlineData("-1", 1, false, "-0.5")]
    [InlineData("2", 1, true, "3")]
    [InlineData("0", -1, true, "0")]
    public void Step_moves_by_the_mode_step_and_never_goes_below_zero(string? current, int direction, bool isInteger, string expected)
    {
        Assert.Equal(Dec(expected), NumericInputParser.Step(Dec(current), direction, isInteger));
    }
}
