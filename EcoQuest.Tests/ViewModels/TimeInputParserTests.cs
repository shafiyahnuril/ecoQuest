using EcoQuest.Controls;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class TimeInputParserTests
{
    [Theory]
    [InlineData("2100", 21, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("9:30", 9, 30)]
    [InlineData("09:30", 9, 30)]
    [InlineData("21:00", 21, 0)]
    [InlineData(" 2100 ", 21, 0)]
    [InlineData("0000", 0, 0)]
    [InlineData("2103", 21, 5)]
    [InlineData("2102", 21, 0)]
    [InlineData("21:03", 21, 5)]
    [InlineData("1158", 12, 0)]
    [InlineData("2357", 23, 55)]
    [InlineData("2358", 0, 0)]
    [InlineData("2359", 0, 0)]
    public void Valid_text_snaps_to_five_minute_slots(string text, int hour, int minute)
    {
        Assert.True(TimeInputParser.TryParse(text, out var value));
        Assert.Equal(new TimeOnly(hour, minute), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_text_is_a_valid_empty_value(string? text)
    {
        Assert.True(TimeInputParser.TryParse(text, out var value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("2500")]
    [InlineData("23:60")]
    [InlineData("2360")]
    [InlineData("21:7")]
    [InlineData("12")]
    [InlineData("abc")]
    [InlineData("21:00:00")]
    [InlineData("-9:30")]
    public void Invalid_text_is_rejected(string text)
    {
        Assert.False(TimeInputParser.TryParse(text, out var value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData(21, 0, 1, 21, 5)]
    [InlineData(21, 0, -1, 20, 55)]
    [InlineData(23, 55, 1, 0, 0)]
    [InlineData(0, 0, -1, 23, 55)]
    public void Step_moves_one_slot_and_wraps_past_midnight(int hour, int minute, int direction, int expectedHour, int expectedMinute)
    {
        Assert.Equal(new TimeOnly(expectedHour, expectedMinute), TimeInputParser.Step(new TimeOnly(hour, minute), direction));
    }

    [Fact]
    public void Step_keeps_an_empty_value_empty()
    {
        Assert.Null(TimeInputParser.Step(null, 1));
    }

    [Theory]
    [InlineData(9, 30, "09:30")]
    [InlineData(0, 5, "00:05")]
    public void Format_is_24_hour_with_leading_zeros(int hour, int minute, string expected)
    {
        Assert.Equal(expected, TimeInputParser.Format(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void Format_of_an_empty_value_is_empty_text()
    {
        Assert.Equal(string.Empty, TimeInputParser.Format(null));
    }
}
