using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcoQuest.Infrastructure;

public interface IAccuWeatherClient
{
    Task<string?> ResolveLocationKeyAsync(string city, CancellationToken ct);                                      // Locations API
    Task<AccuWeatherDailyForecast?> GetForecastForAsync(string locationKey, DateOnly date, CancellationToken ct); // 5-day, ambil tanggal target
}

public sealed record AccuWeatherDailyForecast(DateOnly Date, decimal MinTempC, decimal MaxTempC, string DayPhrase,
                                              int DayIcon, bool HasPrecipitation, string? PrecipitationType, string RawJson);
