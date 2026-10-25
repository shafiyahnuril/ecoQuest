using System;
using System.Globalization;

namespace EcoQuest.Core.Common;

/// <summary>
/// Indonesian (id-ID) number and date text for the UI (UI-UX-Flow-Frontend 2.7). Normative: the prototype's format() is not used.
/// Pure and clock-free: callers pass the value, never "now".
/// </summary>
public static class UiFormat
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    /// <summary>"0,45 kg CO₂", "0,033 kg CO₂", "0 kg CO₂".</summary>
    public static string Co2(decimal kg) => Kg(kg) + " CO₂";

    /// <summary>Same number as <see cref="Co2"/> without the gas name ("9,45 kg"), for cards whose label already says CO₂.</summary>
    public static string Kg(decimal kg)
    {
        if (kg == 0m)
        {
            return "0 kg";
        }

        var decimals = Math.Abs(kg) < 0.1m ? 3 : 2;
        var rounded = Math.Round(kg, decimals, MidpointRounding.AwayFromZero);
        return rounded.ToString("N" + decimals, Id) + " kg";
    }

    /// <summary>Whole number with id-ID grouping ("1.250").</summary>
    public static string Count(int value) => value.ToString("N0", Id);

    /// <summary>"17 Okt 2026".</summary>
    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", Id);

    /// <summary>"Sabtu, 17 Oktober 2026".</summary>
    public static string LongDate(DateOnly date) => date.ToString("dddd, d MMMM yyyy", Id);
}
