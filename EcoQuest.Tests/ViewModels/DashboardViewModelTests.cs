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
        ["Weather.TomorrowIn"] = "Besok di {0}",
        ["Weather.FallbackTitle"] = "Saran umum",
        ["Weather.FallbackAdvice"] = "Siapkan tumbler dan tas belanja.",
        ["Dashboard.InsightSubtitle"] = "Insight berdasarkan jurnalmu akan muncul di sini.",
        ["Dashboard.InsightSubtitleFilled"] = "Kamu telah mencatat {0} hari refleksi.",
        ["Dashboard.InsightSubtitleFilledNoCount"] = "Berikut hal yang bisa kamu pelajari.",
        ["Dashboard.InsightEmptyTitle"] = "Mulai refleksi pertamamu",
        ["Dashboard.InsightEmptyBody"] = "Catat aktivitas nyata hari ini untuk membuka insight personal.",
        ["Dashboard.InsightHabitTitle"] = "Bangun kebiasaan secara bertahap",
        ["Dashboard.InsightHabitBody"] = "Mulai dari kegiatan yang paling mudah.",
        ["Dashboard.Insight.TopCategory"] = "Kontribusi terbesar",
        ["Dashboard.Insight.WeakestCategory"] = "Peluang berikutnya",
        ["Dashboard.Insight.Streak"] = "Konsistensi refleksi",
        ["Dashboard.TrendNoReview"] = "tidak ada refleksi",
    };

    private readonly IDashboardService _dashboard = Substitute.For<IDashboardService>();
    private readonly IProfileService _profile = Substitute.For<IProfileService>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IWeatherSuggestionService _weather = Substitute.For<IWeatherSuggestionService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly FakeSessionService _session = new();
    private readonly FakeClock _clock = new(Evening);

    public DashboardViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.TryGetValue(call.Arg<string>(), out var v) ? v : call.Arg<string>());
        _session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        ReturnsDashboard(Dto());
        ReturnsProfile(reviewCount: 7);
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(LiveForecast("Yogyakarta"));
    }

    private DashboardViewModel Create() =>
        new(_dashboard, _profile, _session, _clock, _nav, _text, new WeatherSuggestionViewModel(_weather, _session, _clock, _text));

    private static WeatherSuggestionDto LiveForecast(string city) =>
        new(city, Today.AddDays(1), false, "Cerah berawan, 24–31°C", 3, "Cocok untuk jalan kaki.", 1, 0.30m);

    private static LevelProgressDto Level(string name = "Eco Explorer") => new(2, name, 100, 499, 212, "Eco Warrior", 288, 0.28);

    private static IReadOnlyList<CategoryBreakdownDto> Breakdown() =>
    [
        new(1, "Transportasi", 0m, 0m, ""), new(2, "Energi", 0m, 0m, ""),
        new(3, "Sampah/Daur Ulang", 0m, 0m, ""), new(4, "Konsumsi", 0m, 0m, ""),
    ];

    private static DashboardDto Dto(
        decimal total = 9.45m, TodayReviewState state = TodayReviewState.NotSubmitted, int streak = 4,
        IReadOnlyList<TrendPointDto>? trend = null, string explanation = "Total dari 7 refleksi.", IReadOnlyList<InsightDto>? insights = null) =>
        new(total, explanation, 212, streak, Level(), state, TrendRange.Weekly, trend ?? [], Breakdown(), insights ?? []);

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

    // ---------- insight panel (FR-4.4, UIR-54) ----------

    [Fact]
    public async Task Without_personal_insights_the_panel_invites_the_first_reflection_and_adds_the_habit_tip()
    {
        ReturnsDashboard(Dto(insights: [new InsightDto(InsightKind.EmptyState, "Catat aktivitas nyata hari ini.")]));

        var vm = await LoadedAsync();

        Assert.Equal(["Mulai refleksi pertamamu", "Bangun kebiasaan secara bertahap"], vm.InsightRows.Select(r => r.Title));
        Assert.Equal("Catat aktivitas nyata hari ini.", vm.InsightRows[0].Body);
        Assert.Equal("Insight berdasarkan jurnalmu akan muncul di sini.", vm.InsightSubtitle);
    }

    [Fact]
    public async Task Personal_insights_show_one_row_each_with_the_service_text_and_the_reflection_count()
    {
        ReturnsDashboard(Dto(insights:
        [
            new InsightDto(InsightKind.TopCategory, "Transportasi paling besar."),
            new InsightDto(InsightKind.Streak, "4 hari berturut-turut."),
        ]));

        var vm = await LoadedAsync();

        Assert.Equal(["Kontribusi terbesar", "Konsistensi refleksi"], vm.InsightRows.Select(r => r.Title));
        Assert.Equal(["Transportasi paling besar.", "4 hari berturut-turut."], vm.InsightRows.Select(r => r.Body));
        Assert.Equal("Kamu telah mencatat 7 hari refleksi.", vm.InsightSubtitle);
    }

    [Fact]
    public async Task The_panel_shows_at_most_three_insights()
    {
        ReturnsDashboard(Dto(insights: Enumerable.Range(1, 5).Select(i => new InsightDto(InsightKind.TopCategory, $"Butir {i}")).ToList()));

        var vm = await LoadedAsync();

        Assert.Equal(DashboardViewModel.MaxInsights, vm.InsightRows.Count);
    }

    [Fact]
    public async Task A_failing_profile_call_keeps_the_insights_without_the_reflection_count()
    {
        _profile.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(Result<UserProfileDto>.Failure(ErrorCode.NetworkError, "x"));
        ReturnsDashboard(Dto(insights: [new InsightDto(InsightKind.TopCategory, "Transportasi paling besar.")]));

        var vm = await LoadedAsync();

        Assert.Single(vm.InsightRows);
        Assert.Equal("Berikut hal yang bisa kamu pelajari.", vm.InsightSubtitle);
    }

    // ---------- weather card (FR-6, UIR-54) ----------

    [Fact]
    public async Task The_weather_card_shows_the_forecast_of_the_signed_in_city()
    {
        var vm = await LoadedAsync();
        await vm.Weather.LoadTask;

        Assert.False(vm.Weather.IsFallback);
        Assert.Equal("Besok di Yogyakarta", vm.Weather.ContentTitle);
    }

    [Fact]
    public async Task A_throwing_weather_service_falls_back_and_the_dashboard_still_loads()
    {
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns<Task<WeatherSuggestionDto>>(_ => throw new InvalidOperationException("boom"));

        var vm = await LoadedAsync();
        await vm.Weather.LoadTask;

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal("9,45 kg", vm.TotalCo2Text);
        Assert.True(vm.Weather.IsFallback);
        Assert.Equal("Saran umum", vm.Weather.ContentTitle);
    }

    [Fact]
    public async Task A_slow_weather_service_does_not_hold_back_the_dashboard()
    {
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<WeatherSuggestionDto>().Task);

        var vm = await LoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.True(vm.Weather.IsLoading);
    }

    [Fact]
    public async Task A_city_change_reloads_the_forecast_and_the_old_answer_does_not_overwrite_the_new_one()
    {
        var oldCity = new TaskCompletionSource<WeatherSuggestionDto>();
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(oldCity.Task, Task.FromResult(LiveForecast("Bandung")));
        var vm = await LoadedAsync();
        var firstLoad = vm.Weather.LoadTask;

        _session.Update(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Bandung", null));
        oldCity.SetResult(LiveForecast("Yogyakarta"));
        await firstLoad;
        await vm.Weather.LoadTask;

        Assert.Equal("Besok di Bandung", vm.Weather.ContentTitle);
        await _weather.Received(2).GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_session_change_without_a_new_city_does_not_reload_the_forecast()
    {
        var vm = await LoadedAsync();
        await vm.Weather.LoadTask;

        _session.Update(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", "avatar.png"));

        await _weather.Received(1).GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leaving_the_screen_stops_the_forecast_and_the_city_watch()
    {
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<WeatherSuggestionDto>().Task);
        var vm = await LoadedAsync();

        vm.OnNavigatedFrom();
        _session.Update(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Bandung", null));

        Assert.False(vm.Weather.IsLoading);
        await _weather.Received(1).GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>());
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

    // ---------- charts: trend range toggle, donut, legend (UIR-52, UIR-53, UIR-57) ----------

    private static IReadOnlyList<TrendPointDto> Days(int count, params int[] reviewedOffsets) =>
        Enumerable.Range(0, count)
            .Select(i => new TrendPointDto(Today.AddDays(i - (count - 1)), 0.5m, reviewedOffsets.Contains(count - 1 - i)))
            .ToList();

    private static IReadOnlyList<CategoryBreakdownDto> FilledBreakdown() =>
    [
        new(1, "Transportasi", 4.5m, 47.6m, ""), new(2, "Energi", 1m, 10.6m, ""),
        new(3, "Sampah/Daur Ulang", 2.75m, 29.1m, ""), new(4, "Konsumsi", 1.2m, 12.7m, ""),
    ];

    private static DashboardDto ChartDto(TrendRange range, IReadOnlyList<TrendPointDto> trend, IReadOnlyList<CategoryBreakdownDto>? breakdown = null) =>
        new(9.45m, "", 212, 4, Level(), TodayReviewState.NotSubmitted, range, trend, breakdown ?? FilledBreakdown(), []);

    [Fact]
    public async Task Charts_get_series_and_legend_percentages_with_one_decimal()
    {
        ReturnsDashboard(ChartDto(TrendRange.Weekly, Days(7, 0, 1, 3)));

        var vm = await LoadedAsync();

        Assert.True(vm.HasTrendData);
        Assert.Single(vm.TrendSeries);
        Assert.True(vm.HasBreakdownData);
        Assert.Equal(4, vm.BreakdownSeries.Length);
        Assert.Equal(["47,6%", "10,6%", "29,1%", "12,7%"], vm.BreakdownLegend.Select(l => l.PercentText));
        Assert.Contains("Transportasi 47,6%", vm.BreakdownAutomationText);
        Assert.Contains("tidak ada refleksi", vm.TrendAutomationText);
    }

    [Fact]
    public async Task Legend_rows_and_donut_help_carry_the_service_explanations()
    {
        var explained = FilledBreakdown().Select(b => b with { Explanation = $"{b.CategoryName}: dari faktor CO₂ per unit." }).ToList();
        ReturnsDashboard(ChartDto(TrendRange.Weekly, Days(7, 0, 1, 3), explained));

        var vm = await LoadedAsync();

        Assert.All(vm.BreakdownLegend, l => Assert.False(string.IsNullOrWhiteSpace(l.Explanation)));
        Assert.Equal("Transportasi: dari faktor CO₂ per unit.", vm.BreakdownLegend[0].Explanation);
        Assert.Contains("Konsumsi: dari faktor CO₂ per unit.", vm.BreakdownAutomationHelp);
        Assert.DoesNotContain("faktor", vm.BreakdownAutomationText);
    }

    [Fact]
    public async Task Without_breakdown_data_the_legend_has_no_explanation()
    {
        var explained = Breakdown().Select(b => b with { Explanation = "tidak dipakai" }).ToList();
        ReturnsDashboard(ChartDto(TrendRange.Weekly, Days(7), explained));

        var vm = await LoadedAsync();

        Assert.All(vm.BreakdownLegend, l => Assert.Equal("", l.Explanation));
    }

    [Fact]
    public async Task Without_reviews_both_charts_are_empty_and_the_legend_shows_names_only()
    {
        ReturnsDashboard(ChartDto(TrendRange.Weekly, Days(7), Breakdown()));

        var vm = await LoadedAsync();

        Assert.False(vm.HasTrendData);
        Assert.False(vm.HasBreakdownData);
        Assert.Empty(vm.BreakdownSeries);
        Assert.Equal(4, vm.BreakdownLegend.Count);
        Assert.All(vm.BreakdownLegend, l => Assert.Equal("", l.PercentText));
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Switching_the_range_reloads_only_the_trend_and_keeps_the_screen_visible()
    {
        ReturnsDashboard(ChartDto(TrendRange.Weekly, Days(7, 0)));
        var vm = await LoadedAsync();
        var donut = vm.BreakdownSeries;
        var gate = new TaskCompletionSource<Result<DashboardDto>>();
        _dashboard.GetDashboardAsync(TrendRange.Monthly, Arg.Any<CancellationToken>()).Returns(gate.Task);

        var switching = vm.SetTrendRangeCommand.ExecuteAsync(TrendRange.Monthly);

        Assert.Equal(LoadState.Loaded, vm.State);
        gate.SetResult(Result<DashboardDto>.Success(ChartDto(TrendRange.Monthly, Days(30, 0, 5), Breakdown())));
        await switching;

        Assert.Equal(TrendRange.Monthly, vm.SelectedRange);
        Assert.Equal(30, ((IEnumerable<double?>)((LiveChartsCore.SkiaSharpView.LineSeries<double?>)vm.TrendSeries[0]).Values!).Count());
        Assert.Same(donut, vm.BreakdownSeries);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.TrendErrorMessage);
    }

    [Fact]
    public async Task Choosing_the_current_range_again_does_not_call_the_service()
    {
        var vm = await LoadedAsync();
        _dashboard.ClearReceivedCalls();

        await vm.SetTrendRangeCommand.ExecuteAsync(TrendRange.Weekly);

        await _dashboard.DidNotReceiveWithAnyArgs().GetDashboardAsync(default, default);
    }

    [Fact]
    public async Task A_failing_range_switch_shows_a_banner_in_the_trend_card_and_leaves_the_rest_intact()
    {
        ReturnsDashboard(ChartDto(TrendRange.Weekly, Days(7, 0, 2)));
        var vm = await LoadedAsync();
        var trend = vm.TrendSeries;
        var rangeChanged = new List<string?>();
        vm.PropertyChanged += (_, e) => rangeChanged.Add(e.PropertyName);
        _dashboard.GetDashboardAsync(TrendRange.Monthly, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto>.Failure(ErrorCode.NetworkError, "Tidak dapat terhubung ke server."));

        await vm.SetTrendRangeCommand.ExecuteAsync(TrendRange.Monthly);

        Assert.Equal("Tidak dapat terhubung ke server.", vm.TrendErrorMessage);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(TrendRange.Weekly, vm.SelectedRange);
        Assert.Contains(nameof(DashboardViewModel.SelectedRange), rangeChanged);
        Assert.Same(trend, vm.TrendSeries);
        Assert.Equal("9,45 kg", vm.TotalCo2Text);
    }

    [Fact]
    public async Task A_later_successful_switch_clears_the_trend_banner()
    {
        var vm = await LoadedAsync();
        _dashboard.GetDashboardAsync(TrendRange.Monthly, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto>.Failure(ErrorCode.NetworkError, "Gagal."),
                     Result<DashboardDto>.Success(ChartDto(TrendRange.Monthly, Days(30, 0))));

        await vm.SetTrendRangeCommand.ExecuteAsync(TrendRange.Monthly);
        await vm.SetTrendRangeCommand.ExecuteAsync(TrendRange.Monthly);

        Assert.Null(vm.TrendErrorMessage);
        Assert.Equal(TrendRange.Monthly, vm.SelectedRange);
    }

    [Fact]
    public async Task A_full_reload_keeps_the_selected_range()
    {
        var vm = await LoadedAsync();
        _dashboard.GetDashboardAsync(TrendRange.Monthly, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto>.Success(ChartDto(TrendRange.Monthly, Days(30, 0))));
        await vm.SetTrendRangeCommand.ExecuteAsync(TrendRange.Monthly);
        _dashboard.ClearReceivedCalls();

        await vm.ReloadCommand.ExecuteAsync(null);

        await _dashboard.Received(1).GetDashboardAsync(TrendRange.Monthly, Arg.Any<CancellationToken>());
    }
}
