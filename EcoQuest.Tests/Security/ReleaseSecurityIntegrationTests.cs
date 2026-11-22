using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DotNetEnv;
using EcoQuest.Infrastructure;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.Security;

[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public class ReleaseSecurityIntegrationTests
{
    private static AppConfig LoadLiveConfig()
    {
        Env.TraversePath().Load();
        var cfg = AppConfig.FromEnvironment();
        Assert.False(string.IsNullOrWhiteSpace(cfg.SupabaseUrl), "SUPABASE_URL wajib diisi untuk security integration test.");
        Assert.False(string.IsNullOrWhiteSpace(cfg.SupabaseKey), "SUPABASE_KEY wajib diisi untuk security integration test.");
        return cfg;
    }

    private static HttpClient CreateAnonClient(AppConfig cfg)
    {
        var client = new HttpClient { BaseAddress = new Uri(cfg.SupabaseUrl!.TrimEnd('/') + "/") };
        client.DefaultRequestHeaders.Add("apikey", cfg.SupabaseKey!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.SupabaseKey!);
        return client;
    }

    [Theory]
    [InlineData("users")]
    [InlineData("dailyreviews")]
    [InlineData("activitylogs")]
    [InlineData("userbadges")]
    internal void Private_table_names_list(string _) { } // guard untuk menjaga konsistensi nama tabel

    [IntegrationFact]
    public async Task S1_Direct_table_access_to_private_tables_is_denied_by_rls()
    {
        var cfg = LoadLiveConfig();
        using var http = CreateAnonClient(cfg);

        foreach (var table in new[] { "users", "dailyreviews", "activitylogs", "userbadges" })
        {
            using var getResp = await http.GetAsync($"rest/v1/{table}?select=*&limit=1");
            Assert.True(
                getResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"GET /rest/v1/{table} seharusnya ditolak RLS, tapi mendapat {(int)getResp.StatusCode}");

            using var postContent = new StringContent("{}", Encoding.UTF8, "application/json");
            using var postResp = await http.PostAsync($"rest/v1/{table}", postContent);
            Assert.True(
                postResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"POST /rest/v1/{table} seharusnya ditolak RLS, tapi mendapat {(int)postResp.StatusCode}");
        }
    }

    [IntegrationFact]
    public async Task S2_Anon_cannot_write_weathercache_or_read_weatherlocations()
    {
        var cfg = LoadLiveConfig();
        using var http = CreateAnonClient(cfg);

        using var fakeCache = new StringContent(
            "{\"city\":\"Yogyakarta\",\"fordate\":\"2099-01-01\",\"forecastsummary\":\"Palsu\"}",
            Encoding.UTF8,
            "application/json");
        using var postResp = await http.PostAsync("rest/v1/weathercache", fakeCache);
        Assert.True(
            postResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"POST /rest/v1/weathercache seharusnya ditolak, tapi mendapat {(int)postResp.StatusCode}");

        using var locResp = await http.GetAsync("rest/v1/weatherlocations?select=*&limit=1");
        Assert.True(
            locResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"GET /rest/v1/weatherlocations seharusnya ditolak, tapi mendapat {(int)locResp.StatusCode}");
    }

    [IntegrationFact]
    public async Task S3_Weather_edge_function_rejects_unauthenticated_invalid_city_and_never_leaks_key()
    {
        var cfg = LoadLiveConfig();
        using var anonClient = CreateAnonClient(cfg);
        using var bareClient = new HttpClient { BaseAddress = new Uri(cfg.SupabaseUrl!.TrimEnd('/') + "/") };

        // 1. Method GET ditolak (405)
        using var getResp = await anonClient.GetAsync("functions/v1/weather-tomorrow");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getResp.StatusCode);

        // 2. Tanpa apikey ditolak (401)
        using var noKeyBody = new StringContent("{\"city\":\"Yogyakarta\"}", Encoding.UTF8, "application/json");
        using var noKeyResp = await bareClient.PostAsync("functions/v1/weather-tomorrow", noKeyBody);
        Assert.Equal(HttpStatusCode.Unauthorized, noKeyResp.StatusCode);

        // 3. Kota di luar whitelist / payload injeksi ditolak (400) dan tidak bocor URL upstream
        using var badCityBody = new StringContent("{\"city\":\"KotaPalsu?inject=1\"}", Encoding.UTF8, "application/json");
        using var badCityResp = await anonClient.PostAsync("functions/v1/weather-tomorrow", badCityBody);
        Assert.Equal(HttpStatusCode.BadRequest, badCityResp.StatusCode);

        var bodyText = await badCityResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("dataservice.accuweather.com", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationFact]
    public async Task S4_Weather_edge_function_returns_cache_on_second_call()
    {
        var cfg = LoadLiveConfig();
        using var http = CreateAnonClient(cfg);

        using var body1 = new StringContent("{\"city\":\"Yogyakarta\"}", Encoding.UTF8, "application/json");
        using var resp1 = await http.PostAsync("functions/v1/weather-tomorrow", body1);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        using var body2 = new StringContent("{\"city\":\"Yogyakarta\"}", Encoding.UTF8, "application/json");
        using var resp2 = await http.PostAsync("functions/v1/weather-tomorrow", body2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var text2 = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("\"source\":\"cache\"", text2, StringComparison.Ordinal);
        Assert.DoesNotContain("dataservice.accuweather.com", text2, StringComparison.OrdinalIgnoreCase);
    }
}
