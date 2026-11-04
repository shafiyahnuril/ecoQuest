using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

/// <summary>Never fails (FR-6.3): the Errors scenario returns the generic fallback suggestion instead.</summary>
public sealed class FakeWeatherSuggestionService : IWeatherSuggestionService
{
    private readonly FakeContext _ctx;
    private readonly ISessionService _session;

    public FakeWeatherSuggestionService(FakeContext ctx, ISessionService session)
    {
        _ctx = ctx; _session = session;
    }

    public async Task<WeatherSuggestionDto> GetTomorrowSuggestionAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);

        var city = _session.Current?.City ?? FakeData.DemoCity;
        var tomorrow = _ctx.Clock.Today.AddDays(1);
        var walking = FakeData.ActivityTypes[0];
        var tumbler = FakeData.ActivityTypes[4];

        if (_ctx.Scenario == FakeScenario.Errors)
            return new WeatherSuggestionDto(city, tomorrow, true, null, null,
                "Coba bawa tumbler dan kantong belanja sendiri besok.",
                tumbler.ActivityTypeId, FakeImpact.CalculateCo2(1m, tumbler.Co2Coefficient));

        return new WeatherSuggestionDto(city, tomorrow, false, "Cerah berawan, 24–31°C", 3,
            "Cuaca besok cerah berawan. Cocok untuk berjalan kaki atau bersepeda ke kampus.",
            walking.ActivityTypeId, FakeImpact.CalculateCo2(3m, walking.Co2Coefficient));
    }
}
