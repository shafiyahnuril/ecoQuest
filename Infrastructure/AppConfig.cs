using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoQuest.Infrastructure;

public sealed record AppConfig(string? SupabaseUrl, string? SupabaseKey, string? AccuWeatherApiKey, bool UseFakes)
{
    public static AppConfig FromEnvironment()
    {
        var rawFakes = Environment.GetEnvironmentVariable("ECOQUEST_USE_FAKES");
        var useFakes = string.Equals(rawFakes, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rawFakes, "1", StringComparison.OrdinalIgnoreCase);

        return new AppConfig(
            Environment.GetEnvironmentVariable("SUPABASE_URL"),
            Environment.GetEnvironmentVariable("SUPABASE_KEY"),
            Environment.GetEnvironmentVariable("ACCUWEATHER_API_KEY"),
            useFakes);
    }

    /// <summary>Key wajib yang kosong (untuk layar error konfigurasi). AccuWeather opsional (ada fallback).</summary>
    public IReadOnlyList<string> MissingRequiredKeys() => UseFakes ? [] :
        new[] { ("SUPABASE_URL", SupabaseUrl), ("SUPABASE_KEY", SupabaseKey) }
            .Where(k => string.IsNullOrWhiteSpace(k.Item2)).Select(k => k.Item1).ToList();
}
