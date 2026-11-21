using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>LeaderboardViewModel (S-09, UIR-71, TC-46): load, retry, empty, own-row highlight, gap row outside the top 20, number format, fake scenarios.</summary>
public class LeaderboardViewModelTests
{
    private static readonly Dictionary<string, string> Strings = new()
    {
        ["Leaderboard.NoLevel"] = "–",
        ["Leaderboard.Auto"] = "Peringkat {0}, {1}, {2}, level {3}",
        ["Leaderboard.Auto.You"] = "Peringkat {0}, {1} (Kamu), {2}, level {3}",
    };

    private readonly ILeaderboardService _service = Substitute.For<ILeaderboardService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero));

    public LeaderboardViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
    }

    private LeaderboardViewModel Create(params LeaderboardEntryDto[] entries)
    {
        _service.GetLeaderboardAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LeaderboardEntryDto>>.Success(entries));
        return new LeaderboardViewModel(_service, _clock, _text);
    }

    private static LeaderboardEntryDto Entry(long rank, string name, decimal kg, bool me = false, string? level = "Eco Explorer") =>
        new(rank, name, kg, level, me);

    private static List<LeaderboardEntryDto> Top20(bool meAt3)
    {
        var list = Enumerable.Range(1, 20).Select(i => Entry(i, $"user{i}", 100 - i, me: meAt3 && i == 3)).ToList();
        return list;
    }

    [Fact]
    public async Task Loading_succeeds_and_lists_rows_in_rank_order()
    {
        var vm = Create(Entry(2, "bagas", 30m), Entry(1, "rani", 41.2m));

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.IsEmpty);
        Assert.Equal(["rani", "bagas"], vm.Rows.OfType<LeaderboardRowViewModel>().Select(r => r.UserName));
        await _service.Received(1).GetLeaderboardAsync(20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_load_shows_the_error_and_retry_succeeds()
    {
        _service.GetLeaderboardAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<LeaderboardEntryDto>>.Failure(ErrorCode.NetworkError, "Tidak dapat terhubung ke server"),
            Result<IReadOnlyList<LeaderboardEntryDto>>.Success([Entry(1, "dinda", 5m, me: true)]));
        var vm = new LeaderboardViewModel(_service, _clock, _text);

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("Tidak dapat terhubung ke server", vm.ErrorMessage);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task No_entries_means_the_empty_state()
    {
        var vm = Create();

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task The_own_row_inside_the_top_20_is_highlighted_without_a_gap()
    {
        var vm = Create(Top20(meAt3: true).ToArray());

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(20, vm.Rows.Count);
        Assert.DoesNotContain(vm.Rows, r => r is LeaderboardGapRowViewModel);
        var own = Assert.Single(vm.Rows.OfType<LeaderboardRowViewModel>(), r => r.IsCurrentUser);
        Assert.Equal(3, own.Rank);
        Assert.Contains("(Kamu)", own.AutomationName);
    }

    [Fact]
    public async Task The_own_row_outside_the_top_20_follows_a_gap_row()
    {
        var entries = Top20(meAt3: false);
        entries.Add(Entry(57, "dinda", 1.5m, me: true));
        var vm = Create(entries.ToArray());

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(22, vm.Rows.Count);
        Assert.IsType<LeaderboardGapRowViewModel>(vm.Rows[20]);
        var own = Assert.IsType<LeaderboardRowViewModel>(vm.Rows[21]);
        Assert.True(own.IsCurrentUser);
        Assert.Equal("57", own.RankText);
    }

    [Fact]
    public async Task The_own_row_at_rank_21_directly_after_the_list_has_no_gap()
    {
        var entries = Top20(meAt3: false);
        entries.Add(Entry(21, "dinda", 1.5m, me: true));
        var vm = Create(entries.ToArray());

        await vm.OnNavigatedToAsync(null);

        Assert.DoesNotContain(vm.Rows, r => r is LeaderboardGapRowViewModel);
    }

    [Fact]
    public async Task Without_an_own_entry_nothing_is_highlighted()
    {
        var vm = Create(Entry(1, "rani", 41.2m, me: false), Entry(2, "bagas", 30m, me: false));

        await vm.OnNavigatedToAsync(null);

        Assert.DoesNotContain(vm.Rows.OfType<LeaderboardRowViewModel>(), r => r.IsCurrentUser);
        Assert.DoesNotContain(vm.Rows, r => r is LeaderboardGapRowViewModel);
    }

    [Fact]
    public async Task Co2_uses_a_decimal_comma_and_a_missing_level_gets_a_dash()
    {
        var vm = Create(Entry(1, "rani", 12.5m), Entry(2, "bagas", 0.033m, level: null));

        await vm.OnNavigatedToAsync(null);

        var rows = vm.Rows.OfType<LeaderboardRowViewModel>().ToList();
        Assert.Equal("12,50 kg CO₂", rows[0].Co2Text);
        Assert.Equal("0,033 kg CO₂", rows[1].Co2Text);
        Assert.Equal("–", rows[1].LevelText);
        Assert.Equal("Peringkat 1, rani, 12,50 kg CO₂, level Eco Explorer", rows[0].AutomationName);
    }

    [Fact]
    public async Task Fake_normal_lists_the_signed_in_user_among_the_top_20()
    {
        var w = new FakeWorld(FakeScenario.Normal);
        w.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var vm = new LeaderboardViewModel(w.Leaderboard, w.Clock, _text);

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Single(vm.Rows.OfType<LeaderboardRowViewModel>(), r => r.IsCurrentUser);
    }

    [Fact]
    public async Task Fake_errors_fails_once_then_retry_succeeds()
    {
        var w = new FakeWorld(FakeScenario.Errors);
        var vm = new LeaderboardViewModel(w.Leaderboard, w.Clock, _text);

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    [Fact]
    public async Task Fake_slow_waits_three_seconds_and_fake_empty_still_shows_the_new_user_last()
    {
        var slow = new FakeWorld(FakeScenario.Slow);
        await new LeaderboardViewModel(slow.Leaderboard, slow.Clock, _text).OnNavigatedToAsync(null);
        Assert.Contains(TimeSpan.FromSeconds(3), slow.Delays);

        var empty = new FakeWorld(FakeScenario.Empty);
        var vm = new LeaderboardViewModel(empty.Leaderboard, empty.Clock, _text);
        await vm.OnNavigatedToAsync(null);
        var last = Assert.IsType<LeaderboardRowViewModel>(vm.Rows[^1]);
        Assert.True(last.IsCurrentUser);
        Assert.Equal("0 kg CO₂", last.Co2Text);
    }
}
