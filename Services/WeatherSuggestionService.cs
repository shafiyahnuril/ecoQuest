using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Infrastructure;

namespace EcoQuest.Services;

public sealed class WeatherSuggestionService(
    ISessionService session,
    IClock clock,
    IActivityTypeRepository typeRepo,
    IWeatherCacheRepository cache,
    IAccuWeatherClient accu,
    WeatherSuggestionRules rules,
    AppConfig config,
    AppLogger log) : IWeatherSuggestionService
{
    public async Task<WeatherSuggestionDto> GetTomorrowSuggestionAsync(CancellationToken ct = default)
    {
        var user = session.RequireUser();
        var date = clock.Today.AddDays(1);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(6));                                     // batas total (FR-6.3)
        try
        {
            var types = await typeRepo.GetAllAsync(cts.Token);
            var cached = await cache.GetAsync(user.City, date, cts.Token);
            if (cached is not null && !cached.IsExpired(clock.UtcNow))
                return rules.FromCache(user.City, date, cached, types);
            if (string.IsNullOrWhiteSpace(config.SupabaseUrl) || string.IsNullOrWhiteSpace(config.SupabaseKey))
                return rules.Fallback(user.City, date, types);

            var key = await accu.ResolveLocationKeyAsync(user.City, cts.Token);
            if (key is null) return rules.Fallback(user.City, date, types);
            var fc = await accu.GetForecastForAsync(key, date, cts.Token);
            if (fc is null) return rules.Fallback(user.City, date, types);

            // Edge Function weather-tomorrow sudah melakukan upsert weathercache di server (service_role).
            return rules.FromForecast(user.City, date, fc.DayPhrase, fc.MinTempC, fc.MaxTempC, fc.HasPrecipitation, fc.DayIcon, types);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warn("weather", "Fallback", ex.GetType().Name);
            return rules.Fallback(user.City, date, await SafeTypes());                  
        }
    }

    
    private async Task<IReadOnlyList<ActivityType>> SafeTypes()
    {
        try { return await typeRepo.GetAllAsync(); }
        catch { return Array.Empty<ActivityType>(); }
    }
}
