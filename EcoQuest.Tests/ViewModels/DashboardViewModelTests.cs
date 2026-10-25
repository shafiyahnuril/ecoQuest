using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>DashboardViewModel (S-04, SDD-Frontend 3.4): banner/button per state, zero account, errors, silent reload, date change, navigation, formatting.</summary>
public class DashboardViewModelTests
{
    private static readonly DateTimeOffset Evening = new(2026, 10, 24, 13, 0, 0, TimeSpan.Zero);   // 20:00 WIB, Saturday 24 Oct 2026
    private static readonly DateOnly Today = new(2026, 10, 24);

    private static readonly Dictionary<string, string> Strings = new()
    {
        ["Dashboard.Greeting"] = "Selamat malam, {0}!",
        ["Dashboard.PendingBanner"] = "Belum ada refleksi malam ini. Luangkan 3 menit sebelum tidur.",
        ["Dashboard.SavedBanner"] = "Refleksi hari ini tersimpan. {0}",
        ["Review.Saved"] = "Refleksi hari ini tersimpan",
        ["Dashboard.HeroCta.Fill"] = "Isi jurnal hari ini",
        ["Dashboard.HeroCta.View"] = "Lihat jurnal hari ini",
        ["Weather.CityLine"] = "{0} · Saran berdasarkan kota domisili",
    };

    private readonly IDashboardService _dashboard = Substitute.For<IDashboardService>();
    private readonly IProfileService _profile = Substitute.For<IProfileService>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly FakeSessionService _session = new();
    private readonly FakeClock _clock = new(Evening);

    public DashboardViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.TryGetValue(call.Arg<string>(), out var v) ? v : call.Arg<string>());
        _session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        ReturnsDashboard(Dto());
        ReturnsProfile(reviewCount: 7);
    }

    private DashboardViewModel Create() => new(_dashboard, _profile, _session, _clock, _nav, _text);

    private static LevelProgressDto Level(string name = "Eco Explorer") => new(2, name, 100, 499, 212, "Eco Warrior", 288, 0.28);

    private static IReadOnlyList<CategoryBreakdownDto> Breakdown() =>
    [
        new(1, "Transportasi", 0m, 0m, ""), new(2, "Energi", 0m, 0m, ""),
        new(3, "Sampah/Daur Ulang", 0m, 0m, ""), new(4, "Konsumsi", 0m, 0m, ""),
    ];

    private static DashboardDto Dto(
        decimal total = 9.45m, TodayReviewState state = TodayReviewState.NotSubmitted, int streak = 4,
        IReadOnlyList<TrendPointDto>? trend = null, string explanation = "Total dari 7 refleksi.") =>
        new(total, explanation, 212, streak, Level(), state, TrendRange.Weekly, trend ?? [], Breakdown(), []);

    private static UserProfileDto Profile(int reviewCount) =>
        new(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null, 212, 9.45m, 4, Level(), Today, reviewCount, 20, Evening);

    private void ReturnsDashboard(DashboardDto dto) =>
        _dashboard.GetDashboardAsync(Arg.Any<TrendRange>(), Arg.Any<CancellationToken>()).Returns(Result<DashboardDto>.Success(dto));

    private void ReturnsProfile(int reviewCount) =>
        _profile.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(Result<UserProfileDto>.Success(Profile(reviewCount)));

    private async Task<DashboardViewModel> LoadedAsync()
    {
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        return vm;
    }

    // ---------- banner and hero button per state (UIR-51, D-02) ----------

    [Fact]
    public async Task NotSubmitted_shows_the_pending_banner_and_the_fill_button()
    {
        var vm = await LoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.IsSubmitted);
        Assert.Equal("Belum ada refleksi malam ini. Luangkan 3 menit sebelum tidur.", vm.BannerText);
        Assert.Equal("Isi jurnal hari ini", vm.HeroCtaText);
    }

    [Fact]
    public async Task Submitted_shows_the_saved_banner_with_todays_co2_and_the_view_button()
    {
        ReturnsDashboard(Dto(state: TodayReviewState.Submitted, trend: [new TrendPointDto(Today.AddDays(-1), 0.5m, true), new TrendPointDto(Today, 0.9m, true)]));

        var vm = await LoadedAsync();

        Assert.True(vm.IsSubmitted);
        Assert.Equal("Refleksi hari ini tersimpan. 0,90 kg CO₂", vm.BannerText);
        Assert.Equal("Lihat jurnal hari ini", vm.HeroCtaText);
    }

    [Fact]
    public async Task Submitted_without_a_trend_point_for_today_shows_the_sentence_without_a_number()
    {
        ReturnsDashboard(Dto(state: TodayReviewState.Submitted, trend: [new TrendPointDto(Today.AddDays(-1), 0.5m, true), new TrendPointDto(Today, 0m, false)]));

        var vm = await LoadedAsync();

        Assert.Equal("Refleksi hari ini tersimpan.", vm.BannerText);
    }

    // ---------- account without reviews (UIR-57) ----------

    [Fact]
    public async Task Account_without_reviews_is_a_loaded_screen_with_zeros_not_an_empty_state()
    {
        ReturnsDashboard(Dto(total: 0m, streak: 0, explanation: "Belum ada data."));
        ReturnsProfile(reviewCount: 0);

        var vm = await LoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.IsEmpty);
        Assert.Equal("0 kg", vm.TotalCo2Text);
        Assert.Equal("0", vm.ReflectionDaysText);
        Assert.Equal("0", vm.StreakText);
        Assert.Equal(4, vm.BreakdownLegend.Count);
    }

    // ---------- formatting and data mapping ----------

    [Fact]
    public async Task Texts_are_formatted_in_indonesian_and_come_from_the_services()
    {
        var vm = await LoadedAsync();

        Assert.Equal("Selamat malam, dinda!", vm.Greeting);
        Assert.Equal("Sabtu, 24 Oktober 2026", vm.TodayText);
        Assert.Equal("9,45 kg", vm.TotalCo2Text);
        Assert.Equal("Total dari 7 refleksi.", vm.TotalCo2Explanation);
        Assert.Equal("7", vm.ReflectionDaysText);
        Assert.Equal("4", vm.StreakText);
        Assert.Equal("Eco Explorer", vm.LevelName);
        Assert.Equal("Yogyakarta · Saran berdasarkan kota domisili", vm.CityLineText);
    }

    [Fact]
    public async Task A_failing_profile_call_shows_a_dash_and_does_not_fail_the_dashboard()
    {
        _profile.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(Result<UserProfileDto>.Failure(ErrorCode.NetworkError, "x"));

        var vm = await LoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(DashboardViewModel.UnknownValue, vm.ReflectionDaysText);
        Assert.Equal("9,45 kg", vm.TotalCo2Text);
    }

    // ---------- loading and errors (UIR-05) ----------

    [Fact]
    public async Task Navigating_to_the_screen_starts_with_the_skeleton()
    {
        var gate = new TaskCompletionSource<Result<DashboardDto>>();
        _dashboard.GetDashboardAsync(Arg.Any<TrendRange>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = Create();

        var load = vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Loading, vm.State);

        gate.SetResult(Result<DashboardDto>.Success(Dto()));
        await load;
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    [Fact]
    public async Task A_service_error_shows_the_error_state_and_retry_loads_again()
    {
        _dashboard.GetDashboardAsync(Arg.Any<TrendRange>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto>.Failure(ErrorCode.NetworkError, "Tidak dapat terhubung ke server."),
                     Result<DashboardDto>.Success(Dto()));
        var vm = Create();

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("Tidak dapat terhubung ke server.", vm.ErrorMessage);

        await vm.ReloadCommand.ExecuteAsync(null);   // "Coba lagi"
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal("9,45 kg", vm.TotalCo2Text);
    }

    // ---------- reload on the same instance (UIR-56) ----------

    [Fact]
    public async Task Silent_reload_keeps_the_old_content_visible_until_the_new_data_arrives()
    {
        var vm = await LoadedAsync();
        var gate = new TaskCompletionSource<Result<DashboardDto>>();
        _dashboard.GetDashboardAsync(Arg.Any<TrendRange>(), Arg.Any<CancellationToken>()).Returns(gate.Task);

        var reload = vm.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);        // no skeleton flash
        Assert.Equal("9,45 kg", vm.TotalCo2Text);        // old content still there

        gate.SetResult(Result<DashboardDto>.Success(Dto(total: 10.5m)));
        await reload;
        Assert.Equal("10,50 kg", vm.TotalCo2Text);
    }

    [Fact]
    public async Task A_new_day_triggers_a_reload_and_the_same_day_does_not()
    {
        var vm = await LoadedAsync();
        _dashboard.ClearReceivedCalls();

        await vm.RefreshIfDateChangedCommand.ExecuteAsync(null);
        await _dashboard.DidNotReceiveWithAnyArgs().GetDashboardAsync(default, default);

        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        await vm.RefreshIfDateChangedCommand.ExecuteAsync(null);

        await _dashboard.Received(1).GetDashboardAsync(TrendRange.Weekly, Arg.Any<CancellationToken>());
        Assert.Equal("Minggu, 25 Oktober 2026", vm.TodayText);
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    // ---------- navigation ----------

    [Fact]
    public async Task Start_and_view_summary_navigate_to_the_evening_review()
    {
        var vm = await LoadedAsync();

        await vm.StartReviewCommand.ExecuteAsync(null);
        await vm.ViewSummaryCommand.ExecuteAsync(null);

        await _nav.Received(2).NavigateTo<EveningReviewViewModel>(Arg.Any<object?>());
    }
}
