using System;
using EcoQuest.Infrastructure;
using Xunit;

namespace EcoQuest.Tests.Infrastructure;

public class AppConfigTests
{
    [Fact]
    public void Missing_url_and_key_are_reported_when_not_using_fakes()
    {
        var cfg = new AppConfig(null, "  ", null, UseFakes: false);
        Assert.Equal(new[] { "SUPABASE_URL", "SUPABASE_KEY" }, cfg.MissingRequiredKeys());
    }

    [Fact]
    public void Non_https_url_is_reported_as_missing_when_not_using_fakes()
    {
        var cfg = new AppConfig("http://example.supabase.co", "sb_publishable_123", null, UseFakes: false);
        Assert.Equal(new[] { "SUPABASE_URL" }, cfg.MissingRequiredKeys());
    }

    [Fact]
    public void Nothing_missing_when_using_fakes()
    {
        var cfg = new AppConfig(null, null, null, UseFakes: true);
        Assert.Empty(cfg.MissingRequiredKeys());
    }

    [Fact]
    public void AccuWeather_key_is_optional()
    {
        var cfg = new AppConfig("https://x.supabase.co", "anon", null, UseFakes: false);
        Assert.Empty(cfg.MissingRequiredKeys());
    }

    [Fact]
    public void FromEnvironment_ignores_accuweather_api_key()
    {
        var prev = Environment.GetEnvironmentVariable("ACCUWEATHER_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ACCUWEATHER_API_KEY", "must-not-be-loaded");
            var cfg = AppConfig.FromEnvironment();
            Assert.Null(cfg.AccuWeatherApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACCUWEATHER_API_KEY", prev);
        }
    }
}
