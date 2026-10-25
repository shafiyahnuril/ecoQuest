using System.Globalization;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Services.Fakes;

/// <summary>
/// CO2/points arithmetic of SRS section 4.4 for the fakes.
/// TEMP until BE-09: Core/Calculation does not exist yet; replace the body of this class with calls to
/// IImpactCalculationService once it lands (do not keep two implementations).
/// </summary>
public static class FakeImpact
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    /// <summary>round(quantity x coefficient, 4, AwayFromZero).</summary>
    public static decimal CalculateCo2(decimal quantity, decimal co2Coefficient) =>
        Math.Round(quantity * co2Coefficient, 4, MidpointRounding.AwayFromZero);

    /// <summary>round(CO2 x 20 x pointFactor, 0, AwayFromZero).</summary>
    public static int CalculatePoints(decimal co2SavedKg, decimal pointFactor) =>
        (int)Math.Round(co2SavedKg * 20m * pointFactor, 0, MidpointRounding.AwayFromZero);

    /// <summary>Hours between two clock times; an end before the start crosses midnight. 4 decimals.</summary>
    public static decimal DurationHours(TimeOnly start, TimeOnly end)
    {
        var minutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        if (minutes < 0) minutes += 24 * 60;
        return Math.Round(minutes / 60m, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Canonical quantity (km / hours / times); 0 means the activity was not done.</summary>
    public static decimal ResolveQuantity(ActivityInput input, ActivityTypeDto type) => type.InputControlType switch
    {
        InputControlType.Toggle => input.IsYes == true ? 1m : 0m,
        InputControlType.TimeRange => input.StartTime is { } s && input.EndTime is { } e ? DurationHours(s, e) : 0m,
        _ => input.Quantity ?? 0m
    };

    /// <summary>True when the user touched the row (a negative or half-filled value still counts, so validation can flag it).</summary>
    public static bool IsFilled(ActivityInput input, ActivityTypeDto type) => type.InputControlType switch
    {
        InputControlType.Toggle => input.IsYes == true,
        InputControlType.TimeRange => input.StartTime is not null || input.EndTime is not null,
        _ => input.Quantity is { } q && q != 0m
    };

    public static ActivityImpactDto Build(ActivityInput input, ActivityTypeDto type, Guid? logId = null)
    {
        var quantity = ResolveQuantity(input, type);
        var co2 = CalculateCo2(quantity, type.Co2Coefficient);
        var points = CalculatePoints(co2, type.PointFactor);

        var quantityFormat = type.InputControlType switch
        {
            InputControlType.Numeric => "0.00",
            InputControlType.TimeRange => "0.00##",
            _ => "0.##"
        };
        var formula = $"{Num(quantity, quantityFormat)} {type.UnitLabel} × {Num(type.Co2Coefficient)} kg CO₂/{type.UnitLabel} = {Num(co2)} kg CO₂";
        var pointsText = $"{Num(co2)} × 20 × {Num(type.PointFactor, "0.0#")} = {points} poin";

        return new ActivityImpactDto(
            logId, type.ActivityTypeId, type.TypeName, type.CategoryId, type.CategoryName,
            type.InputControlType, quantity, type.UnitLabel,
            input.StartTime, input.EndTime,
            co2, points, type.Co2Coefficient, type.PointFactor,
            formula, pointsText,
            string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(), null);
    }

    /// <summary>id-ID number text (decimal comma), e.g. 0.0325 -> "0,0325".</summary>
    public static string Num(decimal value, string format = "0.00##") => value.ToString(format, Id);
}
