using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Services.Fakes;
using Xunit;

namespace EcoQuest.Tests.Fakes;

public class FakeScenarioTests
{
    private static readonly LoginRequest Good = new(FakeData.DemoUserName, FakeData.DemoPassword);
    private static SubmitReviewRequest WalkingOnly(bool ack = false) =>
        new([ActivityInput.Empty(1) with { Quantity = 3m }], ack);

    // ---- Normal -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Normal_login_works_with_username_or_email_and_rejects_bad_password()
    {
        var w = new FakeWorld(FakeScenario.Normal);

        var bad = await w.Auth.LoginAsync(new LoginRequest("dinda", "salah"));
        var byEmail = await w.Auth.LoginAsync(new LoginRequest(FakeData.DemoEmail, FakeData.DemoPassword));

        Assert.Equal(ErrorCode.InvalidCredentials, bad.Error);
        Assert.True(byEmail.IsSuccess);
        Assert.True(w.Session.IsAuthenticated);
    }

    [Fact]
    public async Task Normal_has_30_days_of_history_with_a_streak_and_unsubmitted_today()
    {
        var w = new FakeWorld(FakeScenario.Normal);

        var history = (await w.History.GetHistoryAsync(new HistoryFilter(null, null, []))).Value!;
        var today = (await w.Review.GetTodayAsync()).Value!;

        Assert.InRange(history.Count, 15, 30);
        Assert.All(history, d => Assert.InRange(d.ReviewDate, w.Clock.Today.AddDays(-30), w.Clock.Today.AddDays(-1)));
        Assert.Equal(4, w.Store.CurrentStreak);
        Assert.Equal(TodayReviewState.NotSubmitted, today.State);
    }

    [Fact]
    public async Task Normal_dashboard_has_four_categories_and_matching_trend_length()
    {
        var w = new FakeWorld(FakeScenario.Normal);

        var weekly = (await w.Dashboard.GetDashboardAsync(TrendRange.Weekly)).Value!;
        var monthly = (await w.Dashboard.GetDashboardAsync(TrendRange.Monthly)).Value!;

        Assert.Equal(7, weekly.Trend.Count);
        Assert.Equal(30, monthly.Trend.Count);
        Assert.Equal(4, weekly.Breakdown.Count);
        Assert.InRange(weekly.Insights.Count, 1, 3);
        Assert.True(weekly.TotalCo2Kg > 0);
    }

    [Fact]
    public async Task Normal_weather_is_live_style_not_fallback()
    {
        var w = new FakeWorld(FakeScenario.Normal);

        var weather = await w.Weather.GetTomorrowSuggestionAsync();

        Assert.False(weather.IsFallback);
        Assert.NotNull(weather.ForecastSummary);
        Assert.Equal(w.Clock.Today.AddDays(1), weather.ForDate);
    }

    [Fact]
    public async Task Normal_latency_is_between_300_and_800_ms()
    {
        var w = new FakeWorld(FakeScenario.Normal);

        await w.Reference.GetActivityTypesAsync();
        await w.Dashboard.GetDashboardAsync(TrendRange.Weekly);
        await w.Weather.GetTomorrowSuggestionAsync();

        Assert.Equal(3, w.Delays.Count);
        Assert.All(w.Delays, d => Assert.InRange(d.TotalMilliseconds, 300, 800));
    }

    [Fact]
    public async Task Submitting_a_review_updates_dashboard_and_locks_the_day()
    {
        var w = new FakeWorld(FakeScenario.Normal);
        var pointsBefore = w.Store.TotalPoints;

        var result = await w.Review.SubmitAsync(WalkingOnly());
        var again = await w.Review.SubmitAsync(WalkingOnly());
        var today = (await w.Review.GetTodayAsync()).Value!;

        Assert.True(result.IsSuccess);
        Assert.Equal(0.45m, result.Value!.TotalCo2SavedKg);
        Assert.Equal(pointsBefore + 9, w.Store.TotalPoints);
        Assert.Equal(ErrorCode.ReviewAlreadySubmitted, again.Error);
        Assert.Equal(TodayReviewState.Submitted, today.State);
    }

    [Fact]
    public async Task Warning_needs_acknowledgement_and_hard_limit_is_an_error()
    {
        var w = new FakeWorld(FakeScenario.Normal);
        var warning = new SubmitReviewRequest([ActivityInput.Empty(1) with { Quantity = 45m }], false);   // > 30 plausible
        var hard = new SubmitReviewRequest([ActivityInput.Empty(1) with { Quantity = 301m }], true);      // > 300 hard

        var notAcked = await w.Review.SubmitAsync(warning);
        var blocked = await w.Review.SubmitAsync(hard);
        var acked = await w.Review.SubmitAsync(warning with { WarningsAcknowledged = true });

        Assert.Equal(ErrorCode.WarningsNotAcknowledged, notAcked.Error);
        Assert.Equal(ErrorCode.ValidationFailed, blocked.Error);
        Assert.True(acked.IsSuccess);
    }

    [Fact]
    public void Zero_and_empty_inputs_are_valid_and_marked_empty()
    {
        var w = new FakeWorld(FakeScenario.Normal);
        var items = FakeData.ActivityTypes.Select(t => ActivityInput.Empty(t.ActivityTypeId)).ToList();

        var result = w.Review.Validate(items, FakeData.ActivityTypes);

        Assert.True(result.IsEmpty);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task Taken_username_is_rejected_in_every_scenario()
    {
        foreach (var scenario in Enum.GetValues<FakeScenario>())
        {
            var w = new FakeWorld(scenario);
            var request = new RegisterRequest(FakeData.TakenUserName, "x@mail.com", "Rahasia123", "Rahasia123", "Yogyakarta");

            var result = await w.Auth.RegisterAsync(request);

            Assert.Equal(ErrorCode.UsernameTaken, result.Error);
            Assert.Equal("Username sudah digunakan", result.Message);
        }
    }

    [Fact]
    public async Task Register_succeeds_and_signs_in()
    {
        var w = new FakeWorld(FakeScenario.Normal);

        var result = await w.Auth.RegisterAsync(new RegisterRequest("budi_01", "Budi@Mail.com", "Rahasia123", "Rahasia123", "Bandung"));

        Assert.True(result.IsSuccess);
        Assert.Equal("budi@mail.com", result.Value!.Email);
        Assert.Equal("budi_01", w.Session.Current!.UserName);
    }

    // ---- Empty --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Empty_is_a_brand_new_account()
    {
        var w = new FakeWorld(FakeScenario.Empty);

        var dashboard = (await w.Dashboard.GetDashboardAsync(TrendRange.Weekly)).Value!;
        var history = (await w.History.GetHistoryAsync(new HistoryFilter(null, null, []))).Value!;
        var badges = (await w.Gamification.GetBadgesAsync()).Value!;
        var level = (await w.Gamification.GetLevelProgressAsync()).Value!;

        Assert.Equal(0m, dashboard.TotalCo2Kg);
        Assert.All(dashboard.Trend, p => Assert.False(p.HasReview));
        Assert.Equal(InsightKind.EmptyState, Assert.Single(dashboard.Insights).Kind);
        Assert.Empty(history);
        Assert.DoesNotContain(badges, b => b.IsEarned);
        Assert.Equal("Eco Starter", level.LevelName);
        Assert.Equal(0, w.Store.CurrentStreak);
    }

    [Fact]
    public async Task Empty_leaderboard_places_the_new_user_last_with_zero()
    {
        var w = new FakeWorld(FakeScenario.Empty);

        var board = (await w.Leaderboard.GetLeaderboardAsync()).Value!;

        var me = Assert.Single(board, e => e.IsCurrentUser);
        Assert.Equal(0m, me.TotalCo2Kg);
        Assert.Equal(board.Count, me.Rank);
    }

    // ---- Errors -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Errors_first_load_fails_with_network_error_then_retry_succeeds()
    {
        var w = new FakeWorld(FakeScenario.Errors);

        var first = await w.Dashboard.GetDashboardAsync(TrendRange.Weekly);
        var retry = await w.Dashboard.GetDashboardAsync(TrendRange.Weekly);
        var typesFirst = await w.Reference.GetActivityTypesAsync();
        var typesRetry = await w.Reference.GetActivityTypesAsync();

        Assert.Equal(ErrorCode.NetworkError, first.Error);
        Assert.Equal("Tidak dapat terhubung ke server. Periksa koneksi internet lalu coba lagi", first.Message);
        Assert.True(retry.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, typesFirst.Error);
        Assert.True(typesRetry.IsSuccess);
    }

    [Fact]
    public async Task Errors_login_fails_once_then_works_and_wrong_password_is_still_invalid()
    {
        var w = new FakeWorld(FakeScenario.Errors);

        var first = await w.Auth.LoginAsync(Good);
        var second = await w.Auth.LoginAsync(Good);
        var wrong = await w.Auth.LoginAsync(new LoginRequest("dinda", "salah"));

        Assert.Equal(ErrorCode.NetworkError, first.Error);
        Assert.True(second.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, wrong.Error);
    }

    [Fact]
    public async Task Errors_submit_is_network_error_then_review_already_submitted_and_today_is_locked()
    {
        var w = new FakeWorld(FakeScenario.Errors);
        await w.Review.GetTodayAsync();   // consume the one-time "today" failure

        var first = await w.Review.SubmitAsync(WalkingOnly());
        var second = await w.Review.SubmitAsync(WalkingOnly());
        var today = (await w.Review.GetTodayAsync()).Value!;

        Assert.Equal(ErrorCode.NetworkError, first.Error);
        Assert.Equal(ErrorCode.ReviewAlreadySubmitted, second.Error);
        Assert.Equal("Refleksi hari ini sudah tersimpan dan tidak dapat diubah", second.Message);
        Assert.Equal(TodayReviewState.Submitted, today.State);
        Assert.NotNull(today.Summary);
    }

    [Fact]
    public async Task Errors_weather_is_the_generic_fallback()
    {
        var w = new FakeWorld(FakeScenario.Errors);

        var weather = await w.Weather.GetTomorrowSuggestionAsync();

        Assert.True(weather.IsFallback);
        Assert.Null(weather.ForecastSummary);
        Assert.Null(weather.AccuWeatherIconCode);
        Assert.False(string.IsNullOrWhiteSpace(weather.SuggestionText));
    }

    // ---- Slow ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Slow_delays_every_call_by_three_seconds_but_returns_normal_data()
    {
        var w = new FakeWorld(FakeScenario.Slow);

        var dashboard = await w.Dashboard.GetDashboardAsync(TrendRange.Weekly);
        var types = await w.Reference.GetActivityTypesAsync();

        Assert.True(dashboard.IsSuccess);
        Assert.True(types.IsSuccess);
        Assert.All(w.Delays, d => Assert.Equal(TimeSpan.FromSeconds(3), d));
        Assert.Equal(2, w.Delays.Count);
    }

    // ---- Determinism --------------------------------------------------------------------------------------------

    [Fact]
    public void History_is_deterministic_and_derived_from_the_injected_clock()
    {
        var a = new FakeWorld(FakeScenario.Normal).Store;
        var b = new FakeWorld(FakeScenario.Normal).Store;

        Assert.Equal(a.Days.Select(d => (d.ReviewId, d.ReviewDate, d.TotalCo2Kg, d.TotalPoints)),
                     b.Days.Select(d => (d.ReviewId, d.ReviewDate, d.TotalCo2Kg, d.TotalPoints)));
        Assert.Equal(new FakeWorld(FakeScenario.Normal).Clock.Today.AddDays(-1), a.Days.Max(d => d.ReviewDate));
    }

    [Fact]
    public void Seed_history_only_contains_dates_from_the_clock()
    {
        var clock = new TestDoubles.FakeClock(new DateTimeOffset(2030, 1, 15, 3, 0, 0, TimeSpan.Zero));   // 10:00 WIB
        var ctx = new FakeContext(FakeScenario.Normal, clock, (_, _) => Task.CompletedTask);

        var store = new FakeStore(ctx);

        Assert.Equal(new DateOnly(2030, 1, 14), store.Days.Max(d => d.ReviewDate));
        Assert.True(store.Days.Min(d => d.ReviewDate) >= new DateOnly(2029, 12, 16));
    }
}
