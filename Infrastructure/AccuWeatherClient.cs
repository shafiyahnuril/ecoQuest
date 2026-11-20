using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EcoQuest.Infrastructure;

public sealed class AccuWeatherClient(HttpClient http, AppConfig config) : IAccuWeatherClient
{
    // HttpClient didaftarkan singleton: BaseAddress = https://dataservice.accuweather.com/, Timeout = 5 s
    public async Task<string?> ResolveLocationKeyAsync(string city, CancellationToken ct)
    {
        var url = $"locations/v1/cities/ID/search?apikey={config.AccuWeatherApiKey}&q={Uri.EscapeDataString(city)}&language=id-id";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;                          // 401 key salah, 503 kuota habis → fallback
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
            ? doc.RootElement[0].GetProperty("Key").GetString() : null;
    }

    public async Task<AccuWeatherDailyForecast?> GetForecastForAsync(string key, DateOnly date, CancellationToken ct)
    {
        var url = $"forecasts/v1/daily/5day/{key}?apikey={config.AccuWeatherApiKey}&language=id-id&metric=true";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return ParseForecast(await resp.Content.ReadAsStringAsync(ct), date);
    }

    internal static AccuWeatherDailyForecast? ParseForecast(string rawJson, DateOnly date)
    {
        using var doc = JsonDocument.Parse(rawJson);
        foreach (var d in doc.RootElement.GetProperty("DailyForecasts").EnumerateArray())
        {
            var day = DateOnly.FromDateTime(DateTimeOffset.Parse(d.GetProperty("Date").GetString()!, CultureInfo.InvariantCulture).DateTime);
            if (day != date) continue;
            var t = d.GetProperty("Temperature"); var dayPart = d.GetProperty("Day");
            return new AccuWeatherDailyForecast(day,
                t.GetProperty("Minimum").GetProperty("Value").GetDecimal(), t.GetProperty("Maximum").GetProperty("Value").GetDecimal(),
                dayPart.GetProperty("IconPhrase").GetString() ?? "", dayPart.GetProperty("Icon").GetInt32(),
                dayPart.GetProperty("HasPrecipitation").GetBoolean(),
                dayPart.TryGetProperty("PrecipitationType", out var p) ? p.GetString() : null, d.GetRawText());
        }
        return null;
    }
}
