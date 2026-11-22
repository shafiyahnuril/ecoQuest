using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoQuest.Infrastructure;

public sealed record AppConfig(string? SupabaseUrl, string? SupabaseKey, string? AccuWeatherApiKey = null, bool UseFakes = false)
{
    public static AppConfig FromEnvironment()
    {
        var rawFakes = Environment.GetEnvironmentVariable("ECOQUEST_USE_FAKES");
        var useFakes = string.Equals(rawFakes, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rawFakes, "1", StringComparison.OrdinalIgnoreCase);

        return new AppConfig(
            Environment.GetEnvironmentVariable("SUPABASE_URL"),
            Environment.GetEnvironmentVariable("SUPABASE_KEY"),
            null,
            useFakes);
    }

    /// <summary>Key wajib yang kosong atau tidak valid (untuk layar error konfigurasi).</summary>
    public IReadOnlyList<string> MissingRequiredKeys()
    {
        if (UseFakes) return [];

        var missing = new List<string>(2);
        if (!IsValidHttpsUrl(SupabaseUrl))
            missing.Add("SUPABASE_URL");
        if (string.IsNullOrWhiteSpace(SupabaseKey))
            missing.Add("SUPABASE_KEY");
        return missing;
    }

    private static bool IsValidHttpsUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
