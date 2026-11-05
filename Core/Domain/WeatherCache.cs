using System;

namespace EcoQuest.Core.Domain;

public sealed class WeatherCache
{
    public string City { get; }
    public DateOnly ForDate { get; }
    public string ForecastSummary { get; }
    public string? RawResponse { get; }
    public DateTimeOffset RetrievedAt { get; }

    private WeatherCache(string city, DateOnly forDate, string forecastSummary,
                         string? rawResponse, DateTimeOffset retrievedAt)
    {
        City = city;
        ForDate = forDate;
        ForecastSummary = forecastSummary;
        RawResponse = rawResponse;
        RetrievedAt = retrievedAt;
    }

    public static WeatherCache Create(string city, DateOnly date, string summary,
                                      string? raw, DateTimeOffset now)
        => new(city, date, summary, raw, now);

    public bool IsExpired(DateTimeOffset asOf) => asOf - RetrievedAt >= TimeSpan.FromHours(24);
}
