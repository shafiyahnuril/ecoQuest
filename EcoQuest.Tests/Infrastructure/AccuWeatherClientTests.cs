using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Infrastructure;
using Xunit;

namespace EcoQuest.Tests.Infrastructure;

public class AccuWeatherClientTests
{
    private static readonly string Fixture =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "accuweather_5day.json"));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

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

    [Fact]
    public async Task GetForecastForAsync_posts_to_edge_function_with_headers_and_no_apikey_in_url()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Fixture, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.supabase.co/functions/v1/") };
        var cfg = new AppConfig("https://example.supabase.co", "sb_publishable_abc", null, UseFakes: false);
        var client = new AccuWeatherClient(http, cfg);

        var forecast = await client.GetForecastForAsync("Yogyakarta", new DateOnly(2026, 11, 10), CancellationToken.None);

        Assert.NotNull(forecast);
        Assert.Equal(24m, forecast.MinTempC);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://example.supabase.co/functions/v1/weather-tomorrow", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal(string.Empty, handler.LastRequest.RequestUri.Query);
        Assert.Equal("sb_publishable_abc", handler.LastRequest.Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("sb_publishable_abc", handler.LastRequest.Headers.Authorization?.Parameter);

        using var bodyDoc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("Yogyakarta", bodyDoc.RootElement.GetProperty("city").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetForecastForAsync_returns_null_on_error_status(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.supabase.co/functions/v1/") };
        var cfg = new AppConfig("https://example.supabase.co", "sb_publishable_abc", null, UseFakes: false);
        var client = new AccuWeatherClient(http, cfg);

        var forecast = await client.GetForecastForAsync("Yogyakarta", new DateOnly(2026, 11, 10), CancellationToken.None);

        Assert.Null(forecast);
    }
}
