using System.IO;
namespace EcoQuest.Services.Fakes;

/// <summary>Behaviour profile of the fake services, chosen with ECOQUEST_FAKE_SCENARIO (UIR-81).</summary>
public enum FakeScenario
{
    /// <summary>Realistic data, 300-800 ms latency.</summary>
    Normal,
    /// <summary>Brand-new account: no history, no points, no badges.</summary>
    Empty,
    /// <summary>First load of every data call fails with NetworkError; retry succeeds. Also covers login/submit/weather failures.</summary>
    Errors,
    /// <summary>Normal data with a 3 s latency so Loading states are visible.</summary>
    Slow
}

/// <summary>
/// Reads the two fake-mode switches. Process environment variables win; otherwise the nearest ".env" file is scanned
/// for EXACTLY these two keys. No other key (e.g. SUPABASE_*) is ever stored, returned or logged (DI is built before
/// <c>Env.Load()</c> runs in App.OnStartup, so a flag written only in .env would not be visible otherwise).
/// </summary>
public static class FakeSettings
{
    public const string UseFakesKey = "ECOQUEST_USE_FAKES";
    public const string ScenarioKey = "ECOQUEST_FAKE_SCENARIO";

    public static bool IsEnabled() => ParseBool(Read(UseFakesKey));

    public static FakeScenario ReadScenario() => ParseScenario(Read(ScenarioKey));

    public static bool ParseBool(string? value) =>
        value is not null && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1");

    public static FakeScenario ParseScenario(string? value) =>
        Enum.TryParse<FakeScenario>(value?.Trim(), ignoreCase: true, out var scenario) && Enum.IsDefined(scenario)
            ? scenario
            : FakeScenario.Normal;

    private static string? Read(string key)
    {
        var fromProcess = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(fromProcess)) return fromProcess;

        // Run from VS the working directory is bin/Debug/..., so also walk up from the executable folder.
        return ReadFromDotEnv(key, Directory.GetCurrentDirectory()) ?? ReadFromDotEnv(key, AppContext.BaseDirectory);
    }

    /// <summary>Finds the nearest ".env" at or above <paramref name="startDirectory"/> and returns the value of <paramref name="key"/>.</summary>
    /// <exception cref="ArgumentException">When <paramref name="key"/> is not one of the two fake-mode keys.</exception>
    public static string? ReadFromDotEnv(string key, string startDirectory)
    {
        if (key != UseFakesKey && key != ScenarioKey)
            throw new ArgumentException("Only the fake-mode keys may be read from .env.", nameof(key));

        try
        {
            for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
            {
                var file = Path.Combine(dir.FullName, ".env");
                if (!File.Exists(file)) continue;

                foreach (var raw in File.ReadLines(file))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

                    var eq = line.IndexOf('=');
                    if (eq <= 0 || line[..eq].Trim() != key) continue;   // every other key is skipped unread

                    var value = line[(eq + 1)..].Trim();
                    if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                        value = value[1..^1];
                    return value;
                }
                return null;   // nearest .env found, key absent
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable .env behaves like a missing one.
        }
        return null;
    }
}
