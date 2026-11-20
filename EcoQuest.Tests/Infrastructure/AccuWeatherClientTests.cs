using EcoQuest.Infrastructure;
using Xunit;
using System.IO;
using System.Text.Json;

namespace EcoQuest.Tests.Infrastructure;

public class AccuWeatherClientTests
{
    private static readonly string Fixture =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "accuweather_5day.json"));

    [Fact]
    public void ParseForecast_10_Nov_is_24_to_31_cerah_berawan_without_rain()
    {
        var f = AccuWeatherClient.ParseForecast(Fixture, new DateOnly(2026, 11, 10));
        Assert.NotNull(f);
        Assert.Equal(24m, f.MinTempC);
        Assert.Equal(31m, f.MaxTempC);
        Assert.Equal("Cerah berawan", f.DayPhrase);
        Assert.False(f.HasPrecipitation);
    }

    [Fact]
    public void ParseForecast_11_Nov_has_rain()
    {
        var f = AccuWeatherClient.ParseForecast(Fixture, new DateOnly(2026, 11, 11));
        Assert.NotNull(f);
        Assert.True(f.HasPrecipitation);
        Assert.Equal("Rain", f.PrecipitationType);
    }

    [Fact]
    public void ParseForecast_12_Nov_max_is_34()
    {
        var f = AccuWeatherClient.ParseForecast(Fixture, new DateOnly(2026, 11, 12));
        Assert.NotNull(f);
        Assert.Equal(34m, f.MaxTempC);
    }

    [Fact]
    public void ParseForecast_date_outside_fixture_is_null() =>
        Assert.Null(AccuWeatherClient.ParseForecast(Fixture, new DateOnly(2026, 11, 20)));
}
