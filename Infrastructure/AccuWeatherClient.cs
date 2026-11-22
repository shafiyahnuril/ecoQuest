using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EcoQuest.Infrastructure;

public sealed class AccuWeatherClient(HttpClient http, AppConfig config) : IAccuWeatherClient
{
    // HttpClient didaftarkan singleton: BaseAddress = {SUPABASE_URL}/functions/v1/, Timeout = 5 s
    public Task<string?> ResolveLocationKeyAsync(string city, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(city)
            || string.IsNullOrWhiteSpace(config.SupabaseUrl)
            || string.IsNullOrWhiteSpace(config.SupabaseKey))
            return Task.FromResult<string?>(null);

        // Resolusi locationKey dilakukan di dalam Edge Function pada server.
        return Task.FromResult<string?>(city.Trim());
    }

    public async Task<AccuWeatherDailyForecast?> GetForecastForAsync(string key, DateOnly date, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)
            || string.IsNullOrWhiteSpace(config.SupabaseUrl)
            || string.IsNullOrWhiteSpace(config.SupabaseKey))
            return null;

        var endpoint = http.BaseAddress is null
            ? $"{config.SupabaseUrl.TrimEnd('/')}/functions/v1/weather-tomorrow"
            : "weather-tomorrow";

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Add("apikey", config.SupabaseKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.SupabaseKey);
        req.Content = new StringContent(JsonSerializer.Serialize(new { city = key.Trim() }), Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(req, ct);
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
                dayPart.TryGetProperty("PrecipitationType", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null,
                d.GetRawText());
        }
        return null;
    }
}
