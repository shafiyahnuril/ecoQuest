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
    // String "yyyy-MM-dd": a DateTime property is subject to timezone conversion when serialized.
    [Column("fordate")]            public string ForDate { get; set; } = "";
    [Column("forecastsummary")]    public string ForecastSummary { get; set; } = "";
    [Column("rawresponse")]        public string? RawResponse { get; set; }
    [Column("retrievedat")]        public DateTimeOffset RetrievedAt { get; set; }
}

// Write-only: Upsert sends the primary key (cacheid = 0), which collides on weathercache_pkey; the DB assigns it.
[Table("weathercache")]
public sealed class WeatherCacheUpsertRecord : BaseModel
{
    [Column("city")]            public string City { get; set; } = "";
    [Column("fordate")]         public string ForDate { get; set; } = "";
    [Column("forecastsummary")] public string ForecastSummary { get; set; } = "";
    [Column("rawresponse")]     public string? RawResponse { get; set; }
    [Column("retrievedat")]     public DateTimeOffset RetrievedAt { get; set; }
}
