using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>One ranked row of the leaderboard table (S-09). Only username, CO₂ and level are shown (FR-7.3).</summary>
public sealed class LeaderboardRowViewModel
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public LeaderboardRowViewModel(LeaderboardEntryDto entry, IUiText text)
    {
        Rank = entry.Rank;
        RankText = entry.Rank.ToString("N0", Id);
        UserName = entry.UserName;
        Co2Text = UiFormat.Co2(entry.TotalCo2Kg);
        LevelText = string.IsNullOrWhiteSpace(entry.LevelName) ? text.Get("Leaderboard.NoLevel") : entry.LevelName;
        IsCurrentUser = entry.IsCurrentUser;
        AutomationName = string.Format(
            Id, text.Get(IsCurrentUser ? "Leaderboard.Auto.You" : "Leaderboard.Auto"), RankText, UserName, Co2Text, LevelText);
    }

    public long Rank { get; }
    public string RankText { get; }
    public string UserName { get; }
    public string Co2Text { get; }
    public string LevelText { get; }
    public bool IsCurrentUser { get; }
    public string AutomationName { get; }
}

/// <summary>The "…" row shown between the top 20 and the signed-in user's own row when their rank is outside the list.</summary>
public sealed class LeaderboardGapRowViewModel;
