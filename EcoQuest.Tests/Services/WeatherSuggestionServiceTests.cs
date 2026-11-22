using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Services;

public class WeatherSuggestionServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly DateOnly Tomorrow = new(2026, 11, 10);          // ada di fixture
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 3, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<ActivityType> Types =
        new[] { Walk(), Transit(), Lamp(), Sorting(), Tumbler(), Bags() };

    private static string FixtureJson() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "accuweather_5day.json"));

    private sealed class Rig : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "ecoquest-ws-" + Guid.NewGuid().ToString("N"));

        public IActivityTypeRepository TypeRepo { get; } = Substitute.For<IActivityTypeRepository>();
        public IWeatherCacheRepository Cache { get; } = Substitute.For<IWeatherCacheRepository>();
        public IAccuWeatherClient Accu { get; } = Substitute.For<IAccuWeatherClient>();
        public WeatherSuggestionService Sut { get; }

        public Rig(string? supabaseKey = "sb_publishable_tes")
        {
            var session = Substitute.For<ISessionService>();
            session.RequireUser().Returns(new UserSession(Guid.NewGuid(), "alya", "alya@tes.local", "Yogyakarta", null));

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);
            clock.Today.Returns(Today);

            TypeRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Types);
            Cache.GetAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                 .Returns((WeatherCache?)null);

            var paths = Substitute.For<IAppPaths>();
            paths.DataRoot.Returns(_dir);
            paths.LocationKeysFile.Returns(Path.Combine(_dir, "locationkeys.json"));

            var rules = new WeatherSuggestionRules(new ImpactCalculationService(
                new ImpactCalculatorFactory(new IImpactCalculator[]
                {
                    new DistanceImpactCalculator(), new DurationImpactCalculator(),
                    new CountImpactCalculator(), new ToggleImpactCalculator()
                })));

            Sut = new WeatherSuggestionService(session, clock, TypeRepo, Cache, Accu,
                rules, new AppConfig("https://example.supabase.co", supabaseKey, null, false), new AppLogger(paths, clock));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }
    }

    private static void GivenForecastAvailable(Rig rig)
    {
        var forecast = AccuWeatherClient.ParseForecast(FixtureJson(), Tomorrow)!;
        rig.Accu.ResolveLocationKeyAsync("Yogyakarta", Arg.Any<CancellationToken>()).Returns("Yogyakarta");
        rig.Accu.GetForecastForAsync("Yogyakarta", Tomorrow, Arg.Any<CancellationToken>()).Returns(forecast);
    }

    [Fact]
    public async Task Api_throws_returns_fallback()
    {
        using var rig = new Rig();
        rig.Accu.ResolveLocationKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("putus"));

        var r = await rig.Sut.GetTomorrowSuggestionAsync();

        Assert.True(r.IsFallback);
        Assert.Null(r.ForecastSummary);
    }

    [Fact]
    public async Task Valid_cache_is_used_without_calling_api()
    {
        using var rig = new Rig();

        var rawDay = AccuWeatherClient.ParseForecast(FixtureJson(), Tomorrow)!.RawJson;
        rig.Cache.GetAsync("Yogyakarta", Tomorrow, Arg.Any<CancellationToken>())
                 .Returns(WeatherCache.Create("Yogyakarta", Tomorrow, "Cerah berawan, 24–31°C", rawDay, Now.AddHours(-1)));

        var r = await rig.Sut.GetTomorrowSuggestionAsync();

        Assert.False(r.IsFallback);
        Assert.Equal("Cerah berawan, 24–31°C", r.ForecastSummary);
        await rig.Accu.DidNotReceive().GetForecastForAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await rig.Accu.DidNotReceive().ResolveLocationKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_supabase_key_returns_fallback_without_calling_client()
    {
        using var rig = new Rig(supabaseKey: "");

        var r = await rig.Sut.GetTomorrowSuggestionAsync();

        Assert.True(r.IsFallback);
        await rig.Accu.DidNotReceive().ResolveLocationKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await rig.Accu.DidNotReceive().GetForecastForAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Live_edge_forecast_returns_suggestion_without_client_cache_upsert()
    {
        using var rig = new Rig();
        GivenForecastAvailable(rig);

        var r = await rig.Sut.GetTomorrowSuggestionAsync();

        Assert.False(r.IsFallback);
        Assert.Equal("Cerah berawan, 24–31°C", r.ForecastSummary);
        await rig.Cache.DidNotReceive().UpsertAsync(Arg.Any<WeatherCache>(), Arg.Any<CancellationToken>());
    }
}
