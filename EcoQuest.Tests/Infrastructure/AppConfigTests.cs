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
}
