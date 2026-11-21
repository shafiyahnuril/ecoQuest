using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>
/// One cell of the badge grid (S-08). Carries theme resource keys, never brushes or geometry, so it stays free of System.Windows.
/// BadgeDto has no icon key, so the icon follows the criteria type; an unknown type falls back to <see cref="DefaultIconKey"/>.
/// </summary>
public sealed class BadgeItemViewModel
{
    public const string DefaultIconKey = "Icon.Award";

    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public BadgeItemViewModel(BadgeDto badge, IClock clock, IUiText text)
    {
        Name = badge.Name;
        Description = badge.Description;
        IsEarned = badge.IsEarned;
        IconKey = IconKeyFor(badge.CriteriaType);
        (BubbleBackgroundKey, BubbleForegroundKey) = IsEarned ? ToneFor(badge.CriteriaType) : ("Bg.Stepper", "Line.Input");

        if (IsEarned)
        {
            if (badge.EarnedAt is { } at)
            {
                var date = UiFormat.Date(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, clock.TimeZone).DateTime));
                StatusText = string.Format(Id, text.Get("Achievements.Badge.Earned"), date);
                AutomationName = string.Format(Id, text.Get("Achievements.Auto.Earned"), Name, date);
            }
            else
            {
                StatusText = text.Get("Achievements.Badge.EarnedNoDate");
                AutomationName = string.Format(Id, text.Get("Achievements.Auto.EarnedNoDate"), Name);
            }
        }
        else
        {
            StatusText = ProgressText(badge, text);
            AutomationName = string.Format(Id, text.Get("Achievements.Auto.Locked"), Name, StatusText);
        }
    }

    public string Name { get; }

    /// <summary>Criteria text, shown as the tooltip and as the automation help text.</summary>
    public string Description { get; }

    public bool IsEarned { get; }

    /// <summary>"Diraih 12 Okt 2026" or the progress "4/7 hari".</summary>
    public string StatusText { get; }

    public string AutomationName { get; }

    public string IconKey { get; }

    public string BubbleBackgroundKey { get; }

    public string BubbleForegroundKey { get; }

    public static string IconKeyFor(CriteriaType type) => type switch
    {
        CriteriaType.ActivityCount => "Icon.Leaf",
        CriteriaType.StreakDays => "Icon.Flame",
        CriteriaType.PointsThreshold => "Icon.Star",
        CriteriaType.CO2SavedThreshold => "Icon.Cloud",
        _ => DefaultIconKey,
    };

    private static (string Background, string Foreground) ToneFor(CriteriaType type) => type switch
    {
        CriteriaType.ActivityCount => ("Bubble.Green.Bg", "Bubble.Green.Fg"),
        CriteriaType.StreakDays => ("Bubble.Orange.Bg", "Bubble.Orange.Fg"),
        CriteriaType.PointsThreshold => ("Bubble.Amber.Bg", "Bubble.Amber.Fg"),
        CriteriaType.CO2SavedThreshold => ("Bubble.Blue.Bg", "Bubble.Blue.Fg"),
        _ => ("Bubble.Default.Bg", "Bubble.Default.Fg"),
    };

    private static string ProgressText(BadgeDto badge, IUiText text)
    {
        var current = Math.Min(badge.CurrentValue, badge.CriteriaValue);
        var (key, format) = badge.CriteriaType switch
        {
            CriteriaType.StreakDays => ("Achievements.Progress.Days", "N0"),
            CriteriaType.ActivityCount => ("Achievements.Progress.Activities", "N0"),
            CriteriaType.PointsThreshold => ("Achievements.Progress.Points", "N0"),
            CriteriaType.CO2SavedThreshold => ("Achievements.Progress.Co2", "0.##"),
            _ => ("Achievements.Progress.Plain", "0.##"),
        };

        var whole = format == "N0";
        var now = whole ? Math.Floor(current) : current;
        return string.Format(Id, text.Get(key), now.ToString(format, Id), badge.CriteriaValue.ToString(format, Id));
    }
}
