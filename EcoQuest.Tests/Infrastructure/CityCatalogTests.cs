using EcoQuest.Infrastructure;
using Xunit;

namespace EcoQuest.Tests.Infrastructure;

public class CityCatalogTests
{
    [Fact]
    public void Cities_contains_43_canonical_cities()
    {
        var catalog = new CityCatalog();

        Assert.Equal(43, catalog.Cities.Count);
        Assert.Equal(43, catalog.Cities.Distinct(StringComparer.Ordinal).Count());
        Assert.All(catalog.Cities, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        Assert.Contains("Yogyakarta", catalog.Cities);
        Assert.Contains("Sleman", catalog.Cities);
        Assert.Contains("Bantul", catalog.Cities);
        Assert.Contains("Jakarta", catalog.Cities);
        Assert.Contains("Tanjung Pinang", catalog.Cities);
    }

    [Theory]
    [InlineData("Yogyakarta")]
    [InlineData("Jakarta")]
    [InlineData("Bandung")]
    [InlineData("Gunungkidul")]
    [InlineData("Kulon Progo")]
    public void IsSupported_returns_true_for_valid_city(string city)
    {
        var catalog = new CityCatalog();
        Assert.True(catalog.IsSupported(city));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Atlantis")]
    [InlineData("yogyakarta")]
    public void IsSupported_returns_false_for_invalid_city(string? city)
    {
        var catalog = new CityCatalog();
        Assert.False(catalog.IsSupported(city));
    }
}
