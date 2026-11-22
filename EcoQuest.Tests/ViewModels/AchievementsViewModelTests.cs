using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>AchievementsViewModel (S-08, UIR-70/73, TC-43..45): load, retry, level/streak/badge text, icon fallback, filter, and the fake scenarios.</summary>
public class AchievementsViewModelTests
{
    private static readonly Dictionary<string, string> Strings = new()
    {
        ["Achievements.Level.ToNext"] = "{0} poin lagi menuju {1}",
        ["Achievements.Level.Max"] = "Level tertinggi tercapai",
        ["Achievements.Badge.Earned"] = "Diraih {0}",
        ["Achievements.Badge.EarnedNoDate"] = "Sudah diraih",
        ["Achievements.Auto.Earned"] = "{0}, diraih {1}",
        ["Achievements.Auto.EarnedNoDate"] = "{0}, sudah diraih",
        ["Achievements.Auto.Locked"] = "{0}, belum diraih, progres {1}",
        ["Achievements.Progress.Days"] = "{0}/{1} hari",
        ["Achievements.Progress.Activities"] = "{0}/{1} aktivitas",
        ["Achievements.Progress.Points"] = "{0}/{1} poin",
        ["Achievements.Progress.Co2"] = "{0}/{1} kg CO₂",
        ["Achievements.Progress.Plain"] = "{0}/{1}",
    };

    private readonly IGamificationService _gamification = Substitute.For<IGamificationService>();
    private readonly IProfileService _profile = Substitute.For<IProfileService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero));

    public AchievementsViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
    }

    private static LevelProgressDto Level(string? next = "Eco Warrior", int? toNext = 288, double ratio = 0.4) =>
        new(2, "Eco Explorer", 100, 499, 211, next, toNext, ratio);

    private static BadgeDto Badge(int id, string name, CriteriaType type, decimal criteria, decimal current, DateTimeOffset? earnedAt = null) =>
        new(id, name, $"Deskripsi {name}", type, criteria, earnedAt is not null, earnedAt, current);

    private static UserProfileDto Profile(int streak) =>
        new(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null, 211, 5m, streak, Level(), null, 0, 0, DateTimeOffset.UtcNow);

    private AchievementsViewModel Create(LevelProgressDto level, IReadOnlyList<BadgeDto> badges, int streak = 4)
    {
        _gamification.GetLevelProgressAsync(Arg.Any<CancellationToken>()).Returns(Result<LevelProgressDto>.Success(level));
        _gamification.GetBadgesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<BadgeDto>>.Success(badges));
        _profile.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(Result<UserProfileDto>.Success(Profile(streak)));
        return new AchievementsViewModel(_gamification, _profile, _clock, _text);
    }

    private static readonly IReadOnlyList<BadgeDto> Mixed =
    [
        Badge(1, "Langkah Pertama", CriteriaType.ActivityCount, 1, 5, new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero)),
        Badge(4, "7 Hari Refleksi Berturut-turut", CriteriaType.StreakDays, 7, 4),
        Badge(7, "10 kg CO2 Pertama", CriteriaType.CO2SavedThreshold, 10, 2.5m),
    ];

    [Fact]
    public async Task Loading_succeeds_and_fills_level_streak_and_badges()
    {
        var vm = Create(Level(), Mixed);

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal("Eco Explorer", vm.LevelName);
        Assert.True(vm.HasNextLevel);
        Assert.Equal(40, vm.LevelProgressPercent, 3);
        Assert.Equal("288 poin lagi menuju Eco Warrior", vm.LevelProgressText);
        Assert.Equal(4, vm.StreakDays);
        Assert.Equal(3, vm.VisibleBadges.Count);
    }

    [Fact]
    public async Task A_failure_shows_the_error_and_one_retry_recovers()
    {
        var vm = Create(Level(), Mixed);
        _gamification.GetBadgesAsync(Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<BadgeDto>>.Failure(ErrorCode.NetworkError, "Tidak dapat terhubung"),
            Result<IReadOnlyList<BadgeDto>>.Success(Mixed));

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("Tidak dapat terhubung", vm.ErrorMessage);
        Assert.Empty(vm.VisibleBadges);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(3, vm.VisibleBadges.Count);
    }

    [Fact]
    public async Task The_highest_level_has_no_progress_bar_and_says_so()
    {
        var vm = Create(new LevelProgressDto(4, "Eco Champion", 1500, null, 1800, null, null, 1.0), Mixed);

        await vm.OnNavigatedToAsync(null);

        Assert.False(vm.HasNextLevel);
        Assert.Equal("Level tertinggi tercapai", vm.LevelProgressText);
    }

    [Fact]
    public async Task A_zero_streak_is_shown_as_zero()
    {
        var vm = Create(Level(), Mixed, streak: 0);

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(0, vm.StreakDays);
    }

    [Fact]
    public async Task An_earned_badge_shows_the_date_in_Jakarta_and_a_locked_one_shows_progress()
    {
        var vm = Create(Level(), Mixed);

        await vm.OnNavigatedToAsync(null);

        var earned = vm.VisibleBadges[0];
        Assert.True(earned.IsEarned);
        Assert.Equal("Diraih 20 Nov 2026", earned.StatusText);
        Assert.Equal("Langkah Pertama, diraih 20 Nov 2026", earned.AutomationName);
        Assert.Equal("Deskripsi Langkah Pertama", earned.Description);

        var streak = vm.VisibleBadges[1];
        Assert.False(streak.IsEarned);
        Assert.Equal("4/7 hari", streak.StatusText);
        Assert.Equal("7 Hari Refleksi Berturut-turut, belum diraih, progres 4/7 hari", streak.AutomationName);

        Assert.Equal("2,5/10 kg CO₂", vm.VisibleBadges[2].StatusText);
    }

    [Fact]
    public void An_earned_time_after_17_00_UTC_is_the_next_day_in_Jakarta()
    {
        var badge = Badge(1, "A", CriteriaType.ActivityCount, 1, 1, new DateTimeOffset(2026, 11, 20, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal("Diraih 21 Nov 2026", new BadgeItemViewModel(badge, _clock, _text).StatusText);
    }

    [Fact]
    public void Progress_never_shows_more_than_the_criterion()
    {
        var badge = Badge(2, "B", CriteriaType.StreakDays, 7, 12);

        Assert.Equal("7/7 hari", new BadgeItemViewModel(badge, _clock, _text).StatusText);
    }

    [Theory]
    [InlineData(CriteriaType.ActivityCount, "Icon.Leaf")]
    [InlineData(CriteriaType.StreakDays, "Icon.Flame")]
    [InlineData(CriteriaType.PointsThreshold, "Icon.Star")]
    [InlineData(CriteriaType.CO2SavedThreshold, "Icon.Cloud")]
    [InlineData((CriteriaType)99, BadgeItemViewModel.DefaultIconKey)]
    public void The_icon_follows_the_criteria_type_and_unknown_types_use_the_default(CriteriaType type, string expected)
    {
        var item = new BadgeItemViewModel(Badge(9, "X", type, 5, 1), _clock, _text);

        Assert.Equal(expected, item.IconKey);
    }

    [Fact]
    public async Task Filter_chips_only_reshape_the_loaded_list()
    {
        var vm = Create(Level(), Mixed);
        await vm.OnNavigatedToAsync(null);

        vm.SetFilterCommand.Execute(AchievementFilter.Earned);
        Assert.Single(vm.VisibleBadges);
        Assert.False(vm.HasNoVisibleBadges);

        vm.SetFilterCommand.Execute(AchievementFilter.Locked);
        Assert.Equal(2, vm.VisibleBadges.Count);

        vm.SetFilterCommand.Execute(AchievementFilter.All);
        Assert.Equal(3, vm.VisibleBadges.Count);
        await _gamification.Received(1).GetBadgesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_filter_with_no_match_flags_the_empty_hint()
    {
        var vm = Create(Level(), [Badge(4, "Kunci", CriteriaType.StreakDays, 7, 0)]);
        await vm.OnNavigatedToAsync(null);

        vm.SetFilterCommand.Execute(AchievementFilter.Earned);

        Assert.Empty(vm.VisibleBadges);
        Assert.True(vm.HasNoVisibleBadges);
    }

    private static AchievementsViewModel FromWorld(FakeWorld world, IUiText text)
    {
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        return new AchievementsViewModel(world.Gamification, world.Profile, world.Clock, text);
    }

    [Fact]
    public async Task Fake_normal_has_a_middle_level_and_both_earned_and_locked_badges()
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var vm = FromWorld(world, _text);

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.True(vm.HasNextLevel);
        Assert.InRange(vm.LevelProgressPercent, 1, 99);
        Assert.Equal(8, vm.VisibleBadges.Count);
        Assert.Contains(vm.VisibleBadges, b => b.IsEarned);
        Assert.Contains(vm.VisibleBadges, b => !b.IsEarned);
    }

    [Fact]
    public async Task Fake_empty_is_level_one_with_streak_zero_and_every_badge_locked()
    {
        var world = new FakeWorld(FakeScenario.Empty);
        var vm = FromWorld(world, _text);

        await vm.OnNavigatedToAsync(null);

        Assert.Equal("Eco Starter", vm.LevelName);
        Assert.Equal(0, vm.StreakDays);
        Assert.Equal(8, vm.VisibleBadges.Count);
        Assert.All(vm.VisibleBadges, b => Assert.False(b.IsEarned));
    }

    [Fact]
    public async Task Fake_errors_fails_once_and_a_single_retry_succeeds()
    {
        var world = new FakeWorld(FakeScenario.Errors);
        var vm = FromWorld(world, _text);

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(8, vm.VisibleBadges.Count);
    }
}
