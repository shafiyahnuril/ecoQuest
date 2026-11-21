using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.ViewModels.Items;

namespace EcoQuest.ViewModels;

public enum AchievementFilter { All, Earned, Locked }

/// <summary>
/// Pencapaian (S-08, UIR-70/73): level card, streak card and the badge grid. Level and badges come from IGamificationService;
/// the effective streak comes from IProfileService because the gamification contract does not carry it. The three calls run together
/// so one "Coba lagi" is enough after a failure. The filter chips only reshape what is already loaded (UIR-73).
/// </summary>
public sealed partial class AchievementsViewModel : ViewModelBase
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    private readonly IGamificationService _gamification;
    private readonly IProfileService _profile;
    private readonly IClock _clock;
    private readonly IUiText _text;
    private IReadOnlyList<BadgeItemViewModel> _allBadges = [];

    public AchievementsViewModel(IGamificationService gamification, IProfileService profile, IClock clock, IUiText text)
    {
        _gamification = gamification;
        _profile = profile;
        _clock = clock;
        _text = text;
        TodayText = UiFormat.LongDate(clock.Today);
    }

    public string TodayText { get; }

    public ObservableCollection<BadgeItemViewModel> VisibleBadges { get; } = [];

    [ObservableProperty] private string _levelName = string.Empty;
    [ObservableProperty] private string _levelProgressText = string.Empty;
    [ObservableProperty] private double _levelProgressPercent;
    [ObservableProperty] private bool _hasNextLevel;
    [ObservableProperty] private int _streakDays;
    [ObservableProperty] private AchievementFilter _filter = AchievementFilter.All;
    [ObservableProperty] private bool _hasNoVisibleBadges;

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var snapshot = await RunAsync(FetchAsync);
        if (snapshot is null)
        {
            return;
        }

        Apply(snapshot);
    }

    [RelayCommand]
    private void SetFilter(AchievementFilter filter) => Filter = filter;

    partial void OnFilterChanged(AchievementFilter value) => ApplyFilter();

    private async Task<Result<Snapshot>> FetchAsync(CancellationToken ct)
    {
        var levelTask = _gamification.GetLevelProgressAsync(ct);
        var badgesTask = _gamification.GetBadgesAsync(ct);
        var profileTask = _profile.GetProfileAsync(ct);
        await Task.WhenAll(levelTask, badgesTask, profileTask);

        var level = await levelTask;
        var badges = await badgesTask;
        var profile = await profileTask;

        foreach (var failed in new Result[] { level, badges, profile })
        {
            if (!failed.IsSuccess)
            {
                return Result<Snapshot>.Failure(failed.Error, failed.Message ?? string.Empty);
            }
        }

        return Result<Snapshot>.Success(new Snapshot(level.Value!, badges.Value!, profile.Value!.CurrentStreak));
    }

    private void Apply(Snapshot snapshot)
    {
        var level = snapshot.Level;
        LevelName = level.LevelName;
        HasNextLevel = level.NextLevelName is not null && level.PointsToNext is not null;
        LevelProgressPercent = Math.Clamp(level.ProgressRatio, 0, 1) * 100;
        LevelProgressText = HasNextLevel
            ? string.Format(Id, _text.Get("Achievements.Level.ToNext"), UiFormat.Count(level.PointsToNext!.Value), level.NextLevelName)
            : _text.Get("Achievements.Level.Max");

        StreakDays = snapshot.Streak;
        _allBadges = snapshot.Badges.Select(b => new BadgeItemViewModel(b, _clock, _text)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        VisibleBadges.Clear();
        foreach (var badge in _allBadges.Where(b => Filter switch
        {
            AchievementFilter.Earned => b.IsEarned,
            AchievementFilter.Locked => !b.IsEarned,
            _ => true,
        }))
        {
            VisibleBadges.Add(badge);
        }

        HasNoVisibleBadges = VisibleBadges.Count == 0 && _allBadges.Count > 0;
    }

    private sealed record Snapshot(LevelProgressDto Level, IReadOnlyList<BadgeDto> Badges, int Streak);
}
