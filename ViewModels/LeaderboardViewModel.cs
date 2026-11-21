using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.ViewModels.Items;

namespace EcoQuest.ViewModels;

/// <summary>
/// Leaderboard (S-09, UIR-71, FR-8.5): top entries by CO₂ avoided plus the signed-in user's own row. When the service returns the own
/// entry with a rank that does not follow the list (outside the top 20), a gap row is inserted before it. Rows are display-only.
/// </summary>
public sealed partial class LeaderboardViewModel : ViewModelBase
{
    private const int Top = 20;

    private readonly ILeaderboardService _leaderboard;
    private readonly IUiText _text;

    public LeaderboardViewModel(ILeaderboardService leaderboard, IClock clock, IUiText text)
    {
        _leaderboard = leaderboard;
        _text = text;
        TodayText = UiFormat.LongDate(clock.Today);
    }

    public string TodayText { get; }

    /// <summary><see cref="LeaderboardRowViewModel"/> items, with a <see cref="LeaderboardGapRowViewModel"/> before an own row outside the list.</summary>
    public ObservableCollection<object> Rows { get; } = [];

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var entries = await RunAsync(ct => _leaderboard.GetLeaderboardAsync(Top, ct));
        if (entries is null)
        {
            return;
        }

        Rows.Clear();
        LeaderboardRowViewModel? previous = null;
        foreach (var row in entries.OrderBy(e => e.Rank).Select(e => new LeaderboardRowViewModel(e, _text)))
        {
            if (row.IsCurrentUser && previous is not null && row.Rank > previous.Rank + 1)
            {
                Rows.Add(new LeaderboardGapRowViewModel());
            }

            Rows.Add(row);
            previous = row;
        }

        IsEmpty = Rows.Count == 0;
    }
}
