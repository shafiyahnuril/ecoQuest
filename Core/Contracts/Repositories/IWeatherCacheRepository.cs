using System;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

public interface IWeatherCacheRepository
{
    Task<WeatherCache?> GetAsync(string city, DateOnly forDate, CancellationToken ct);
    Task UpsertAsync(WeatherCache entry, CancellationToken ct);
}
