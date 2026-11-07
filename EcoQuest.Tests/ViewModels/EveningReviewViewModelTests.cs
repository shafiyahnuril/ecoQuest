using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>EveningReviewViewModel (S-05, UIR-30..37): load modes, debounced validate/preview, totals, dialog order, result handling.</summary>
public class EveningReviewViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    private static readonly Dictionary<string, string> Strings = new()
    {
        ["Review.RunningTotal"] = "Total sementara ({0} aktivitas)",
        ["Review.Confirm"] = "Setelah disimpan, refleksi hari ini tidak bisa diubah.",
        ["Review.ConfirmTotal"] = "Total sementara: {0} dari {1} aktivitas.",
        ["Review.Title"] = "Evening Review",
        ["Review.Saved"] = "Refleksi hari ini tersimpan",
        ["Review.SavedAgain"] = "Refleksi hari ini sudah tersimpan",
        ["Dialog.LevelUp"] = "Kamu kini {0}",
        ["Dialog.NewBadge"] = "Milestone baru: {0}",
        ["Weather.FallbackTitle"] = "Saran umum",
        ["Weather.TomorrowIn"] = "Besok di {0}",
        ["Weather.CityLine"] = "{0} · Saran berdasarkan kota domisili",
    };

    private readonly IEveningReviewService _review = Substitute.For<IEveningReviewService>();
    private readonly IReferenceDataService _reference = Substitute.For<IReferenceDataService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly IWeatherSuggestionService _weather = Substitute.For<IWeatherSuggestionService>();
    private readonly FakeWorld _world = new(FakeScenario.Normal);
    private readonly List<string> _dialogTitles = [];

    public EveningReviewViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
        _reference.GetActivityTypesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ActivityTypeDto>>.Success(FakeData.ActivityTypes));
        _review.Validate(Arg.Any<IReadOnlyList<ActivityInput>>(), Arg.Any<IReadOnlyList<ActivityTypeDto>>())
            .Returns(call => _world.Review.Validate(call.ArgAt<IReadOnlyList<ActivityInput>>(0), call.ArgAt<IReadOnlyList<ActivityTypeDto>>(1)));
        _review.Preview(Arg.Any<ActivityInput>(), Arg.Any<ActivityTypeDto>())
            .Returns(call => _world.Review.Preview(call.ArgAt<ActivityInput>(0), call.ArgAt<ActivityTypeDto>(1)));
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<DialogIcon>())
            .Returns(call =>
            {
                _dialogTitles.Add(call.ArgAt<string>(0));
                return true;
            });
        _world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(_ => _world.Weather.GetTomorrowSuggestionAsync());
        GetTodayReturns(TodayReviewState.NotSubmitted);
    }

    private static ReviewSummaryDto Summary() =>
        new(Guid.NewGuid(), Today, DateTimeOffset.UtcNow, 0.45m, 9, [], "", 1, null, []);

    private void GetTodayReturns(TodayReviewState state) =>
        _review.GetTodayAsync(Arg.Any<CancellationToken>()).Returns(
            Result<TodayReviewDto>.Success(new TodayReviewDto(state, Today, state == TodayReviewState.Submitted ? Summary() : null)));

    private void SubmitReturns(Result<ReviewSummaryDto> result) =>
        _review.SubmitAsync(Arg.Any<SubmitReviewRequest>(), Arg.Any<CancellationToken>()).Returns(result);

    private WeatherSuggestionViewModel CreateWeather(TimeSpan? timeout = null) =>
        new(_weather, _world.Session, _world.Clock, _text, timeout);

    private ShellViewModel CreateShell() =>
        new(_nav, _dialogs, _text, new ToastViewModel(), _world.Session, Substitute.For<IAuthService>(), _review);

    private async Task<(EveningReviewViewModel Vm, ShellViewModel Shell)> StartAsync(TimeSpan? debounce = null, TimeSpan? weatherTimeout = null)
    {
        var shell = CreateShell();
        shell.HasPendingReview = true;
        var vm = new EveningReviewViewModel(
            _review, _reference, _dialogs, _text, _world.Clock, _nav, shell, CreateWeather(weatherTimeout), debounce ?? TimeSpan.Zero);
        await vm.OnNavigatedToAsync(null);
        return (vm, shell);
    }

    private static ActivityInputItemViewModel Item(EveningReviewViewModel vm, int activityTypeId) =>
        vm.Groups.SelectMany(g => g.Items).Single(i => i.ActivityTypeId == activityTypeId);

    private static void Fill(EveningReviewViewModel vm, int activityTypeId, decimal quantity) =>
        ((QuantityItemViewModel)Item(vm, activityTypeId)).Quantity = quantity;

    // ---------- loading (UIR-30, UIR-31) ----------

    [Fact]
    public async Task NotSubmitted_shows_the_empty_form_grouped_by_category()
    {
        var (vm, _) = await StartAsync();

        Assert.Equal(ReviewMode.Form, vm.Mode);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(["Transportasi", "Energi", "Sampah/Daur Ulang", "Konsumsi"], vm.Groups.Select(g => g.Name));
        Assert.Equal(6, vm.Groups.Sum(g => g.Items.Count));
        Assert.All(vm.Groups.SelectMany(g => g.Items), i => Assert.False(i.IsFilled));
        Assert.Equal(UiFormat.LongDate(Today), vm.TodayText);
        Assert.Equal(0, vm.FilledCount);
        Assert.Equal("0 kg CO₂", vm.TotalText);
        Assert.True(vm.SubmitCommand.CanExecute(null));
    }

    [Fact]
    public async Task Submitted_shows_the_summary_without_loading_activity_types()
    {
        GetTodayReturns(TodayReviewState.Submitted);

        var (vm, _) = await StartAsync();

        Assert.Equal(ReviewMode.Summary, vm.Mode);
        Assert.NotNull(vm.Summary);
        Assert.False(vm.SubmitCommand.CanExecute(null));
        _ = _reference.DidNotReceive().GetActivityTypesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_load_shows_the_error_state_with_the_service_message()
    {
        _review.GetTodayAsync(Arg.Any<CancellationToken>())
            .Returns(Result<TodayReviewDto>.Failure(ErrorCode.NetworkError, "Tidak ada koneksi"));

        var (vm, _) = await StartAsync();

        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("Tidak ada koneksi", vm.ErrorMessage);
    }

    // ---------- validate / preview (UIR-33) ----------

    [Fact]
    public async Task An_edit_validates_all_rows_and_previews_only_the_edited_row()
    {
        var (vm, _) = await StartAsync();
        _review.ClearReceivedCalls();

        Fill(vm, 1, 3m);

        _review.Received(1).Validate(Arg.Is<IReadOnlyList<ActivityInput>>(l => l.Count == 6), Arg.Any<IReadOnlyList<ActivityTypeDto>>());
        _review.Received(1).Preview(Arg.Is<ActivityInput>(i => i.ActivityTypeId == 1), Arg.Any<ActivityTypeDto>());
    }

    [Fact]
    public async Task A_burst_of_edits_is_validated_once_after_the_debounce()
    {
        var (vm, _) = await StartAsync(TimeSpan.FromMilliseconds(60));
        _review.ClearReceivedCalls();

        Fill(vm, 1, 1m);
        Fill(vm, 1, 2m);
        Fill(vm, 1, 3m);
        _review.DidNotReceive().Validate(Arg.Any<IReadOnlyList<ActivityInput>>(), Arg.Any<IReadOnlyList<ActivityTypeDto>>());

        await Task.Delay(400);

        _review.Received(1).Validate(Arg.Any<IReadOnlyList<ActivityInput>>(), Arg.Any<IReadOnlyList<ActivityTypeDto>>());
        _review.Received(1).Preview(Arg.Any<ActivityInput>(), Arg.Any<ActivityTypeDto>());
        Assert.Equal("≈ 0,45 kg CO₂", vm.TotalText);
    }

    [Fact]
    public async Task Total_is_the_sum_of_previews_and_the_count_is_the_filled_rows()
    {
        var (vm, _) = await StartAsync();

        Fill(vm, 1, 3m);
        Fill(vm, 4, 2m);

        Assert.Equal("≈ 1,05 kg CO₂", vm.TotalText);
        Assert.Equal(2, vm.FilledCount);
        Assert.Equal("Total sementara (2 aktivitas)", vm.RunningTotalText);

        Fill(vm, 1, 0m);

        Assert.Equal("≈ 0,60 kg CO₂", vm.TotalText);
        Assert.Equal(1, vm.FilledCount);
    }

    [Fact]
    public async Task An_error_blocks_saving_and_a_warning_does_not()
    {
        var (vm, _) = await StartAsync();

        Fill(vm, 1, -1m);
        Assert.True(vm.HasBlockingError);
        Assert.False(vm.SubmitCommand.CanExecute(null));

        Fill(vm, 1, 40m);   // above PlausibleMaxPerDay (30) but below HardMaxPerDay (300)
        Assert.False(vm.HasBlockingError);
        Assert.True(Item(vm, 1).HasWarning);
        Assert.True(vm.SubmitCommand.CanExecute(null));
    }

    // ---------- dialogs (UIR-35) ----------

    [Fact]
    public async Task A_plain_save_asks_only_the_confirmation_with_the_running_total()
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 3m);
        SubmitReturns(Result<ReviewSummaryDto>.Success(Summary()));

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(["Dialog.Save.Title"], _dialogTitles);
        _ = _dialogs.Received(1).ConfirmAsync(
            "Dialog.Save.Title",
            "Setelah disimpan, refleksi hari ini tidak bisa diubah." + Environment.NewLine + "Total sementara: 0,45 kg CO₂ dari 1 aktivitas.",
            "Dialog.Save.Confirm", Arg.Any<string?>(), DialogIcon.Leaf);
    }

    [Fact]
    public async Task Warnings_then_confirmation_are_asked_in_order_and_the_warnings_are_acknowledged()
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 40m);
        SubmitReturns(Result<ReviewSummaryDto>.Success(Summary()));

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(["Dialog.Warning.Title", "Dialog.Save.Title"], _dialogTitles);
        _ = _dialogs.Received(1).ConfirmAsync(
            "Dialog.Warning.Title", Arg.Is<string>(m => m.StartsWith("Jalan Kaki / Bersepeda: 40 km")), "Dialog.Warning.Confirm", "Dialog.Warning.Cancel", DialogIcon.Warning);
        _ = _review.Received(1).SubmitAsync(Arg.Is<SubmitReviewRequest>(r => r.WarningsAcknowledged), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_review_asks_the_empty_dialog_before_the_confirmation()
    {
        var (vm, _) = await StartAsync();
        SubmitReturns(Result<ReviewSummaryDto>.Success(Summary()));

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(["Dialog.Empty.Title", "Dialog.Save.Title"], _dialogTitles);
        _ = _dialogs.Received(1).ConfirmAsync("Dialog.Empty.Title", Arg.Any<string>(), "Dialog.Empty.Confirm", Arg.Any<string?>(), DialogIcon.Info);
        _ = _review.Received(1).SubmitAsync(Arg.Is<SubmitReviewRequest>(r => !r.WarningsAcknowledged), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Dialog.Warning.Title")]
    [InlineData("Dialog.Empty.Title")]
    [InlineData("Dialog.Save.Title")]
    public async Task Cancelling_any_dialog_returns_to_the_form_without_saving(string cancelledTitle)
    {
        var (vm, _) = await StartAsync();
        if (cancelledTitle != "Dialog.Empty.Title")
        {
            Fill(vm, 1, cancelledTitle == "Dialog.Warning.Title" ? 40m : 3m);
        }

        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<DialogIcon>())
            .Returns(call => call.ArgAt<string>(0) != cancelledTitle);

        await vm.SubmitCommand.ExecuteAsync(null);

        _ = _review.DidNotReceive().SubmitAsync(Arg.Any<SubmitReviewRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(ReviewMode.Form, vm.Mode);
        Assert.False(vm.IsBusy);
    }

    // ---------- results (UIR-37) ----------

    [Fact]
    public async Task Success_shows_the_summary_and_clears_the_pending_dot()
    {
        var (vm, shell) = await StartAsync();
        Fill(vm, 1, 3m);
        SubmitReturns(Result<ReviewSummaryDto>.Success(Summary()));
        GetTodayReturns(TodayReviewState.Submitted);

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(ReviewMode.Summary, vm.Mode);
        Assert.NotNull(vm.Summary);
        Assert.False(shell.HasPendingReview);
        Assert.Null(vm.BannerMessage);
    }

    [Theory]
    [InlineData(ErrorCode.ValidationFailed)]
    [InlineData(ErrorCode.WarningsNotAcknowledged)]
    public async Task A_rejected_save_shows_the_message_keeps_the_inputs_and_revalidates(ErrorCode code)
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 3m);
        SubmitReturns(Result<ReviewSummaryDto>.Failure(code, "Periksa isian."));
        _review.ClearReceivedCalls();
        var titlesBefore = _dialogTitles.Count;

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("Periksa isian.", vm.BannerMessage);
        Assert.Null(vm.BannerRetryCommand);
        Assert.Equal(ReviewMode.Form, vm.Mode);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(3m, ((QuantityItemViewModel)Item(vm, 1)).Quantity);
        _review.Received(2).Validate(Arg.Any<IReadOnlyList<ActivityInput>>(), Arg.Any<IReadOnlyList<ActivityTypeDto>>());
        Assert.Equal(titlesBefore + 1, _dialogTitles.Count);
    }

    [Fact]
    public async Task A_network_error_keeps_the_inputs_and_offers_a_retry_that_saves_without_asking_again()
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 40m);
        SubmitReturns(Result<ReviewSummaryDto>.Failure(ErrorCode.NetworkError, "Tidak ada koneksi"));

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("Tidak ada koneksi", vm.BannerMessage);
        Assert.NotNull(vm.BannerRetryCommand);
        Assert.Equal(40m, ((QuantityItemViewModel)Item(vm, 1)).Quantity);
        Assert.Equal(LoadState.Loaded, vm.State);

        SubmitReturns(Result<ReviewSummaryDto>.Success(Summary()));
        var titlesBefore = _dialogTitles.Count;
        await vm.BannerRetryCommand!.ExecuteAsync(null);

        Assert.Equal(ReviewMode.Summary, vm.Mode);
        Assert.Equal(titlesBefore, _dialogTitles.Count);
        _ = _review.Received(2).SubmitAsync(Arg.Is<SubmitReviewRequest>(r => r.WarningsAcknowledged), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewAlreadySubmitted_reloads_today_and_shows_the_summary()
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 3m);
        SubmitReturns(Result<ReviewSummaryDto>.Failure(ErrorCode.ReviewAlreadySubmitted, "Sudah tersimpan"));
        GetTodayReturns(TodayReviewState.Submitted);

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(ReviewMode.Summary, vm.Mode);
        Assert.Null(vm.BannerMessage);
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    [Fact]
    public async Task ReviewDateMismatch_shows_the_message_and_reloads_a_fresh_form_for_the_new_day()
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 3m);
        var tomorrow = Today.AddDays(1);
        SubmitReturns(Result<ReviewSummaryDto>.Failure(ErrorCode.ReviewDateMismatch, "Tanggal berganti"));
        _review.GetTodayAsync(Arg.Any<CancellationToken>())
            .Returns(Result<TodayReviewDto>.Success(new TodayReviewDto(TodayReviewState.NotSubmitted, tomorrow, null)));

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("Tanggal berganti", vm.BannerMessage);
        Assert.Equal(ReviewMode.Form, vm.Mode);
        Assert.Equal(UiFormat.LongDate(tomorrow), vm.TodayText);
        Assert.All(vm.Groups.SelectMany(g => g.Items), i => Assert.False(i.IsFilled));
        Assert.Equal(0, vm.FilledCount);
    }

    [Fact]
    public async Task A_second_save_while_one_is_running_is_ignored()
    {
        var (vm, _) = await StartAsync();
        Fill(vm, 1, 3m);
        var pending = new TaskCompletionSource<Result<ReviewSummaryDto>>();
        _review.SubmitAsync(Arg.Any<SubmitReviewRequest>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        var first = vm.SubmitCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.SubmitCommand.CanExecute(null));
        vm.SubmitCommand.Execute(null);
        pending.SetResult(Result<ReviewSummaryDto>.Success(Summary()));
        await first;

        _ = _review.Received(1).SubmitAsync(Arg.Any<SubmitReviewRequest>(), Arg.Any<CancellationToken>());
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Back_home_navigates_to_the_dashboard()
    {
        var (vm, _) = await StartAsync();

        await vm.BackHomeCommand.ExecuteAsync(null);

        _ = _nav.Received(1).NavigateTo<DashboardViewModel>(Arg.Any<object?>());
    }

    // ---------- summary (S-06, UIR-39, UIR-40) ----------

    private static ReviewSummaryDto SummaryWith(LevelChangeDto? levelUp, params BadgeDto[] badges) =>
        new(Guid.NewGuid(), Today, DateTimeOffset.UtcNow, 0.45m, 9, [], "Insight.", 1, levelUp, badges);

    private static BadgeDto Badge(string name) => new(1, name, "Deskripsi", CriteriaType.StreakDays, 7, true, null, 7);

    private async Task<(EveningReviewViewModel Vm, List<(string Title, string Message, DialogIcon Icon, ReviewMode Mode)> Dialogs)> SaveAsync(ReviewSummaryDto summary)
    {
        var (vm, _) = await StartAsync();
        var dialogs = new List<(string, string, DialogIcon, ReviewMode)>();
        _dialogs.ShowInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<DialogIcon>()).Returns(call =>
        {
            dialogs.Add((call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<DialogIcon>(3), vm.Mode));
            return Task.CompletedTask;
        });
        Fill(vm, 1, 3m);
        SubmitReturns(Result<ReviewSummaryDto>.Success(summary));

        await vm.SubmitCommand.ExecuteAsync(null);
        return (vm, dialogs);
    }

    [Fact]
    public async Task The_summary_is_built_from_the_submitted_dto_and_titled_saved()
    {
        var (vm, _) = await SaveAsync(SummaryWith(null));

        Assert.Equal(ReviewMode.Summary, vm.Mode);
        Assert.Equal("0,45 kg", vm.Summary!.TotalText);
        Assert.Equal("Refleksi hari ini tersimpan", vm.Title);
    }

    [Fact]
    public async Task Reopening_the_same_day_says_already_saved_and_never_opens_the_milestone_dialog()
    {
        GetTodayReturns(TodayReviewState.Submitted);
        var summary = SummaryWith(new LevelChangeDto("Eco Starter", "Eco Explorer"), Badge("7 Hari"));
        _review.GetTodayAsync(Arg.Any<CancellationToken>())
            .Returns(Result<TodayReviewDto>.Success(new TodayReviewDto(TodayReviewState.Submitted, Today, summary)));

        var (vm, _) = await StartAsync();

        Assert.Equal("Refleksi hari ini sudah tersimpan", vm.Title);
        Assert.NotNull(vm.Summary);
        _ = _dialogs.DidNotReceive().ShowInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<DialogIcon>());
    }

    [Fact]
    public async Task No_level_up_and_no_badge_opens_no_dialog()
    {
        var (_, dialogs) = await SaveAsync(SummaryWith(null));

        Assert.Empty(dialogs);
    }

    [Fact]
    public async Task A_level_up_opens_one_dialog_with_the_trophy_after_the_summary_is_shown()
    {
        var (_, dialogs) = await SaveAsync(SummaryWith(new LevelChangeDto("Eco Starter", "Eco Explorer")));

        var dialog = Assert.Single(dialogs);
        Assert.Equal("Kamu kini Eco Explorer", dialog.Title);
        Assert.Equal(DialogIcon.Level, dialog.Icon);
        Assert.Equal(ReviewMode.Summary, dialog.Mode);
    }

    [Fact]
    public async Task New_badges_open_one_dialog_with_the_award_icon()
    {
        var (_, dialogs) = await SaveAsync(SummaryWith(null, Badge("7 Hari Refleksi Berturut-turut")));

        var dialog = Assert.Single(dialogs);
        Assert.Equal("Milestone baru: 7 Hari Refleksi Berturut-turut", dialog.Title);
        Assert.Equal(DialogIcon.Badge, dialog.Icon);
    }

    [Fact]
    public async Task A_level_up_together_with_badges_is_still_one_dialog()
    {
        var (_, dialogs) = await SaveAsync(SummaryWith(new LevelChangeDto("Eco Starter", "Eco Explorer"), Badge("A"), Badge("B")));

        var dialog = Assert.Single(dialogs);
        Assert.Equal("Kamu kini Eco Explorer", dialog.Title);
        Assert.Equal("Milestone baru: A" + Environment.NewLine + "Milestone baru: B", dialog.Message);
        Assert.Equal(DialogIcon.Level, dialog.Icon);
    }

    [Fact]
    public async Task The_weather_card_loads_after_the_summary_shows_and_a_slow_answer_does_not_block_it()
    {
        var pending = new TaskCompletionSource<WeatherSuggestionDto>();
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);

        var (vm, _) = await SaveAsync(SummaryWith(null));

        Assert.Equal(ReviewMode.Summary, vm.Mode);
        Assert.True(vm.Weather.IsLoading);
        pending.SetResult(await _world.Weather.GetTomorrowSuggestionAsync());
        await vm.Weather.LoadTask;
        Assert.False(vm.Weather.IsLoading);
        Assert.Equal("Besok di Yogyakarta", vm.Weather.ContentTitle);
    }

    [Fact]
    public async Task A_weather_timeout_ends_on_Saran_umum_without_dialog_or_banner()
    {
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<WeatherSuggestionDto>().Task);
        var (vm, _) = await StartAsync(weatherTimeout: TimeSpan.FromMilliseconds(30));
        Fill(vm, 1, 3m);
        SubmitReturns(Result<ReviewSummaryDto>.Success(SummaryWith(null)));

        await vm.SubmitCommand.ExecuteAsync(null);
        await vm.Weather.LoadTask;

        Assert.Equal("Saran umum", vm.Weather.ContentTitle);
        Assert.False(vm.HasBanner);
        Assert.Equal(ReviewMode.Summary, vm.Mode);
        _ = _dialogs.DidNotReceive().ShowInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<DialogIcon>());
    }

    [Fact]
    public async Task Leaving_the_screen_cancels_the_weather_load()
    {
        CancellationToken seen = default;
        _weather.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            seen = call.Arg<CancellationToken>();
            return new TaskCompletionSource<WeatherSuggestionDto>().Task;
        });
        var (vm, _) = await SaveAsync(SummaryWith(null));

        vm.OnNavigatedFrom();

        Assert.True(seen.IsCancellationRequested);
        Assert.False(vm.Weather.IsLoading);
    }
}
