using System;

namespace EcoQuest.Models
{
    public class WeatherCache
    {
        private int _cacheId; 
        private string _city;
        private DateTime _forDate;
        private string _forecastSummary;
        private string _rawResponse;
        private DateTime _retrievedAt;

        public int CacheId { get => _cacheId; set => _cacheId = value; }
        public string City { get => _city; set => _city = value; }
        public DateTime ForDate { get => _forDate; set => _forDate = value; }
        public string ForecastSummary { get => _forecastSummary; set => _forecastSummary = value; }
        public string RawResponse { get => _rawResponse; set => _rawResponse = value; }
        public DateTime RetrievedAt { get => _retrievedAt; set => _retrievedAt = value; }

        public WeatherCache() { }

        public bool IsExpired(DateTime asOf)
        {
            return asOf > RetrievedAt.AddHours(24);
        }
    }
}
