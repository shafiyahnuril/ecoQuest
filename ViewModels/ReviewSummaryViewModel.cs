using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;
using EcoQuest.ViewModels.Items;

namespace EcoQuest.ViewModels;

/// <summary>Read-only summary of a submitted review (S-06). Both entry paths (just saved, reopened) build it from the same <see cref="ReviewSummaryDto"/>.</summary>
public sealed class ReviewSummaryViewModel
{
    public ReviewSummaryViewModel(
        ReviewSummaryDto summary, IClock clock, IUiText text, WeatherSuggestionViewModel weather, IAsyncRelayCommand backHomeCommand)
    {
        var submittedAt = TimeZoneInfo.ConvertTime(summary.SubmittedAt, clock.TimeZone);
        LockedText = string.Format(CultureInfo.CurrentCulture, text.Get("Summary.LockedAt"), submittedAt.ToString("HH:mm", CultureInfo.InvariantCulture));
        TotalText = UiFormat.Kg(summary.TotalCo2SavedKg);
        PointsText = string.Format(CultureInfo.CurrentCulture, text.Get("Summary.Points"), UiFormat.Count(summary.TotalPointsEarned));
        StreakText = string.Format(CultureInfo.CurrentCulture, text.Get("Summary.Streak"), summary.CurrentStreak);
        Insight = summary.Insight;
        Items = summary.Items.Select(i => new SummaryActivityItem(i, text)).ToList();
        EmptyText = text.Get("Summary.NoActivities");
        Weather = weather;
        BackHomeCommand = backHomeCommand;
    }

    public string LockedText { get; }

    public string TotalText { get; }

    public string PointsText { get; }

    public string StreakText { get; }

    public string Insight { get; }

    public IReadOnlyList<SummaryActivityItem> Items { get; }

    public bool HasItems => Items.Count > 0;

    public string EmptyText { get; }

    public WeatherSuggestionViewModel Weather { get; }

    public IAsyncRelayCommand BackHomeCommand { get; }
}
