using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace EcoQuest.Data.Records;

// Catatan: atribut kolom disesuaikan dengan skema v1.2 & Core/Domain/WeatherCache;
// verifikasi ulang terhadap SDD-Backend.md §6.1 saat dokumen diterima.
[Table("weathercache")]
public sealed class WeatherCacheRecord : BaseModel
{
    [PrimaryKey("cacheid", false)] public int CacheId { get; set; }
    [Column("city")]               public string City { get; set; } = "";
    [Column("fordate")]            public string ForDate { get; set; } = "";
    [Column("forecastsummary")]    public string ForecastSummary { get; set; } = "";
    [Column("rawresponse")]        public string? RawResponse { get; set; }
    [Column("retrievedat")]        public DateTimeOffset RetrievedAt { get; set; }
}
