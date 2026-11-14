using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace EcoQuest.Data.Records;

// Selaras dengan SDD-Backend.md §6.1 dan 001_schema_v1_2.sql
[Table("weathercache")]
public sealed class WeatherCacheRecord : BaseModel
{
    [PrimaryKey("cacheid", false)] public int CacheId { get; set; }
    [Column("city")]               public string City { get; set; } = "";
    [Column("fordate")]            public DateTime ForDate { get; set; }
    [Column("forecastsummary")]    public string ForecastSummary { get; set; } = "";
    [Column("rawresponse")]        public string? RawResponse { get; set; }
    [Column("retrievedat")]        public DateTimeOffset RetrievedAt { get; set; }
}
