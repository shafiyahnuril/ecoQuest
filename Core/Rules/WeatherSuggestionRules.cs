using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Rules;

public sealed class WeatherSuggestionRules(IImpactCalculationService impact)
{
    private static readonly CultureInfo Id = new("id-ID");

    private const decimal HotThresholdC = 33m;                                   

    
    private const int WalkId = 1, TransitId = 2, LampId = 3, SortingId = 4, TumblerId = 5, BagId = 6;

    
    public string Summary(string phrase, decimal minTempC, decimal maxTempC) =>
        $"{phrase}, {Num(minTempC)}–{Num(maxTempC)}°C";

    public WeatherSuggestionDto FromForecast(string city, DateOnly date, string phrase, decimal minTempC,
        decimal maxTempC, bool hasPrecipitation, int iconCode, IReadOnlyList<ActivityType> types)
    {
        var summary = Summary(phrase, minTempC, maxTempC);

        if (hasPrecipitation)                                                    
        {
            var co2 = Estimate(types, new ActivityInput(TransitId, 10m, null, null, null, null, null));
            return new WeatherSuggestionDto(city, date, false, summary, iconCode,
                $"Besok diprakirakan {phrase}. Naik transportasi umum 10 km bisa menghindari {Kg(co2)} dibanding kendaraan pribadi. Jangan lupa tumbler!",
                TransitId, co2);
        }

        if (maxTempC >= HotThresholdC)                                           
        {
            var co2 = Estimate(types, new ActivityInput(TumblerId, null, null, null, true, null, null));
            return new WeatherSuggestionDto(city, date, false, summary, iconCode,
                $"Besok cukup panas ({Num(maxTempC)}°C). Bawa tumbler sendiri: hemat {Kg(co2)} dan tetap terhidrasi.",
                TumblerId, co2);
        }

        var walk = Estimate(types, new ActivityInput(WalkId, 2m, null, null, null, null, null));   
        return new WeatherSuggestionDto(city, date, false, summary, iconCode,
            $"Besok {phrase}, {Num(minTempC)}–{Num(maxTempC)}°C — cocok jalan kaki/bersepeda. 2 km bisa menghindari {Kg(walk)}.",
            WalkId, walk);
    }

    
    public WeatherSuggestionDto FromCache(string city, DateOnly date, WeatherCache cached, IReadOnlyList<ActivityType> types)
    {
        using var doc = JsonDocument.Parse(cached.RawResponse!);
        var d = doc.RootElement;
        var t = d.GetProperty("Temperature");
        var day = d.GetProperty("Day");
        return FromForecast(city, date,
            day.GetProperty("IconPhrase").GetString() ?? "",
            t.GetProperty("Minimum").GetProperty("Value").GetDecimal(),
            t.GetProperty("Maximum").GetProperty("Value").GetDecimal(),
            day.GetProperty("HasPrecipitation").GetBoolean(),
            day.GetProperty("Icon").GetInt32(),
            types);
    }

    public WeatherSuggestionDto Fallback(string city, DateOnly date, IReadOnlyList<ActivityType> types)
    {
        switch (date.DayOfYear % 3)                                              
        {
            case 0:
            {
                var co2 = Estimate(types, new ActivityInput(SortingId, 2m, null, null, null, null, null));
                return new WeatherSuggestionDto(city, date, true, null, null,
                    $"Pilah sampah 2 kali: bisa menghindari {Kg(co2)}.", SortingId, co2);
            }
            case 1:
            {
                var co2 = Estimate(types, new ActivityInput(BagId, 1m, null, null, null, null, null));
                return new WeatherSuggestionDto(city, date, true, null, null,
                    $"Bawa kantong belanja sendiri: bisa menghindari {Kg(co2)} per kunjungan.", BagId, co2);
            }
            default:
            {
                var co2 = Estimate(types, new ActivityInput(LampId, null, new TimeOnly(9, 0), new TimeOnly(12, 0), null, null, null));
                return new WeatherSuggestionDto(city, date, true, null, null,
                    $"Matikan lampu saat keluar ruangan 3 jam: bisa menghindari {Kg(co2)}.", LampId, co2);
            }
        }
    }

    
    private decimal? Estimate(IReadOnlyList<ActivityType> types, ActivityInput input)
    {
        var type = types.FirstOrDefault(t => t.ActivityTypeId == input.ActivityTypeId);
        return type is null ? null : impact.Calculate(input, type).Co2SavedKg;
    }

    private static string Kg(decimal? v) => v is { } x ? $"±{x.ToString("0.00", Id)} kg CO₂" : "sejumlah CO₂";

    private static string Num(decimal v) => v.ToString("0.#", Id);
}
