using System.Runtime.CompilerServices;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class ShellViewModelTests
{
    private static readonly UserSession Dinda = new(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null);
    private static readonly DateOnly Today = new(2026, 10, 25);

    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly IEveningReviewService _review = Substitute.For<IEveningReviewService>();
    private readonly FakeSessionService _session = new();

    private ShellViewModel Create() =>
        new(_nav, _dialogs, _text, new ToastViewModel(), _session, _auth, _review);

    // Only the screen TYPE matters to the shell, so skip the real constructors.
    private static T Screen<T>() where T : ViewModelBase => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private void NavigateTo(ViewModelBase screen)
    {
        _nav.Current.Returns(screen);
        _nav.Navigated += Raise.Event();
    }

    private static Result<TodayReviewDto> Ok(TodayReviewState state) =>
        Result<TodayReviewDto>.Success(new TodayReviewDto(state, Today, null));

    private static Result<TodayReviewDto> NetworkFail() =>
        Result<TodayReviewDto>.Failure(ErrorCode.NetworkError, "Tidak ada koneksi");

    private void ReviewReturns(Result<TodayReviewDto> result) =>
        _review.GetTodayAsync(Arg.Any<CancellationToken>()).Returns(result);

    // ---------- session -> account block (UIR-25) ----------

    [Fact]
    public void Account_block_is_empty_without_session()
    {
        var shell = Create();
        Assert.Equal("", shell.DisplayName);
        Assert.Equal("", shell.AvatarInitial);
    }

    [Fact]
    public void SessionChanged_updates_name_city_and_initial()
    {
        var shell = Create();

        _session.Start(Dinda);
        Assert.Equal("dinda", shell.DisplayName);
        Assert.Equal("Yogyakarta", shell.City);
        Assert.Equal("D", shell.AvatarInitial);

        _session.Update(Dinda with { UserName = "rani", City = "Sleman", AvatarPath = @"C:\a.png" });
        Assert.Equal("rani", shell.DisplayName);
        Assert.Equal("Sleman", shell.City);
        Assert.Equal("R", shell.AvatarInitial);
        Assert.Equal(@"C:\a.png", shell.AvatarImagePath);
    }

    [Fact]
    public void Existing_session_is_read_in_constructor()
    {
        _session.Start(Dinda);
        Assert.Equal("dinda", Create().DisplayName);
    }

    [Fact]
    public void IsAuthenticated_needs_session_and_a_main_area_screen()
    {
        var shell = Create();

        NavigateTo(Screen<DashboardViewModel>());
        Assert.False(shell.IsAuthenticated);          // main screen but no session

        _session.Start(Dinda);
        Assert.True(shell.IsAuthenticated);

        NavigateTo(Screen<OnboardingViewModel>());
        Assert.False(shell.IsAuthenticated);          // session exists but auth-area screen

        NavigateTo(Screen<ProfileViewModel>());
        Assert.True(shell.IsAuthenticated);

        _session.End();
        Assert.False(shell.IsAuthenticated);
    }

    [Fact]
    public void Dispose_unsubscribes_from_session_and_navigation()
    {
        var shell = Create();
        shell.Dispose();

        _session.Start(Dinda);
        NavigateTo(Screen<DashboardViewModel>());

        Assert.Equal("", shell.DisplayName);
        Assert.Null(shell.MainViewModel);
        shell.Dispose();   // idempotent
    }

    // ---------- navigation menu ----------

    [Fact]
    public void Navigated_marks_only_the_matching_menu_item_active()
    {
        var shell = Create();
        _session.Start(Dinda);
        ReviewReturns(Ok(TodayReviewState.Submitted));

        NavigateTo(Screen<HistoryViewModel>());

        Assert.Equal(MainMenu.History, shell.SelectedMenu);
        Assert.Equal([MainMenu.History], shell.MenuItems.Where(i => i.IsSelected).Select(i => i.Menu));
        Assert.NotNull(shell.MainViewModel);
        Assert.Null(shell.AuthViewModel);
    }

    [Fact]
    public void Login_screen_goes_to_auth_area_without_menu()
    {
        var shell = Create();
        NavigateTo(Screen<LoginViewModel>());

        Assert.Null(shell.SelectedMenu);
        Assert.NotNull(shell.AuthViewModel);
        Assert.Null(shell.MainViewModel);
        Assert.DoesNotContain(shell.MenuItems, i => i.IsSelected);
    }

    [Fact]
    public async Task NavigateCommand_maps_each_menu_entry_to_its_screen()
    {
        var shell = Create();
        _session.Start(Dinda);
        ReviewReturns(Ok(TodayReviewState.Submitted));
        NavigateTo(Screen<ProfileViewModel>());   // authenticated, on Profile

        await shell.NavigateCommand.ExecuteAsync(MainMenu.Dashboard);
        await shell.NavigateCommand.ExecuteAsync(MainMenu.EveningReview);
        await shell.NavigateCommand.ExecuteAsync(MainMenu.History);
        await shell.NavigateCommand.ExecuteAsync(MainMenu.Achievements);
        await shell.NavigateCommand.ExecuteAsync(MainMenu.Leaderboard);

        await _nav.Received(1).NavigateTo<DashboardViewModel>(Arg.Any<object?>());
        await _nav.Received(1).NavigateTo<EveningReviewViewModel>(Arg.Any<object?>());
        await _nav.Received(1).NavigateTo<HistoryViewModel>(Arg.Any<object?>());
        await _nav.Received(1).NavigateTo<AchievementsViewModel>(Arg.Any<object?>());
        await _nav.Received(1).NavigateTo<LeaderboardViewModel>(Arg.Any<object?>());
    }

    [Fact]
    public async Task NavigateCommand_is_disabled_when_signed_out_and_ignores_the_current_menu()
    {
        var shell = Create();
        Assert.False(shell.NavigateCommand.CanExecute(MainMenu.Dashboard));

        _session.Start(Dinda);
        ReviewReturns(Ok(TodayReviewState.Submitted));
        NavigateTo(Screen<DashboardViewModel>());
        Assert.True(shell.NavigateCommand.CanExecute(MainMenu.Dashboard));

        await shell.NavigateCommand.ExecuteAsync(MainMenu.Dashboard);
        await _nav.DidNotReceive().NavigateTo<DashboardViewModel>(Arg.Any<object?>());
    }

    // ---------- logout (UIR-23) ----------

    [Fact]
    public async Task Logout_confirmed_logs_out_clears_history_then_opens_login()
    {
        var shell = Create();
        _dialogs.ConfirmAsync(default!, default!, default!, default).ReturnsForAnyArgs(true);

        await shell.LogoutCommand.ExecuteAsync(null);

        Received.InOrder(() =>
        {
            _auth.Logout();
            _nav.ClearHistory();
            _nav.NavigateTo<LoginViewModel>(Arg.Any<object?>());
        });
        Assert.False(shell.HasPendingReview);
    }

    [Fact]
    public async Task Logout_cancelled_does_nothing()
    {
        var shell = Create();
        _dialogs.ConfirmAsync(default!, default!, default!, default).ReturnsForAnyArgs(false);

        await shell.LogoutCommand.ExecuteAsync(null);

        _auth.DidNotReceive().Logout();
        _nav.DidNotReceive().ClearHistory();
        await _nav.DidNotReceive().NavigateTo<LoginViewModel>(Arg.Any<object?>());
    }

    [Fact]
    public async Task Logout_command_is_disabled_while_running_so_it_cannot_double_submit()
    {
        var shell = Create();
        var dialog = new TaskCompletionSource<bool>();
        _dialogs.ConfirmAsync(default!, default!, default!, default).ReturnsForAnyArgs(dialog.Task);

        var first = shell.LogoutCommand.ExecuteAsync(null);
        Assert.False(shell.LogoutCommand.CanExecute(null));
        // A button bound to the command is disabled while it runs, so a second click cannot start another logout.
        Assert.False(((System.Windows.Input.ICommand)shell.LogoutCommand).CanExecute(null));
        dialog.SetResult(true);
        await first;
        Assert.True(shell.LogoutCommand.CanExecute(null));

        _auth.Received(1).Logout();
        await _nav.Received(1).NavigateTo<LoginViewModel>(Arg.Any<object?>());
    }

    // ---------- pending-review dot (UIR-56) ----------

    [Fact]
    public async Task RefreshPending_maps_review_state_to_the_dot()
    {
        var shell = Create();
        _session.Start(Dinda);
        var reviewItem = shell.MenuItems.Single(i => i.Menu == MainMenu.EveningReview);

        ReviewReturns(Ok(TodayReviewState.NotSubmitted));
        await shell.RefreshPendingAsync();
        Assert.True(shell.HasPendingReview);
        Assert.True(reviewItem.IsPendingIndicatorVisible);

        ReviewReturns(Ok(TodayReviewState.Submitted));
        await shell.RefreshPendingAsync();
        Assert.False(shell.HasPendingReview);
        Assert.False(reviewItem.IsPendingIndicatorVisible);
    }

    [Fact]
    public async Task RefreshPending_keeps_previous_value_on_network_error()
    {
        var shell = Create();
        _session.Start(Dinda);

        ReviewReturns(Ok(TodayReviewState.NotSubmitted));
        await shell.RefreshPendingAsync();
        ReviewReturns(NetworkFail());
        await shell.RefreshPendingAsync();
        Assert.True(shell.HasPendingReview);

        ReviewReturns(Ok(TodayReviewState.Submitted));
        await shell.RefreshPendingAsync();
        ReviewReturns(NetworkFail());
        await shell.RefreshPendingAsync();
        Assert.False(shell.HasPendingReview);
    }

    [Fact]
    public async Task RefreshPending_does_nothing_without_session()
    {
        var shell = Create();
        await shell.RefreshPendingAsync();
        await _review.DidNotReceive().GetTodayAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Overlapping_refreshes_never_run_concurrently_and_are_merged()
    {
        var shell = Create();
        _session.Start(Dinda);

        var running = 0;
        var maxRunning = 0;
        var calls = 0;
        var gates = new List<TaskCompletionSource<Result<TodayReviewDto>>>();
        _review.GetTodayAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls++;
            maxRunning = Math.Max(maxRunning, ++running);
            var gate = new TaskCompletionSource<Result<TodayReviewDto>>();
            gates.Add(gate);
            return gate.Task.ContinueWith(t => { running--; return t.Result; });
        });

        var first = shell.RefreshPendingAsync();
        var second = shell.RefreshPendingAsync();   // arrives while the first is in flight
        var third = shell.RefreshPendingAsync();    // merged with the second
        Assert.Equal(1, calls);

        gates[0].SetResult(Ok(TodayReviewState.NotSubmitted));
        await Task.Delay(100);
        Assert.Equal(2, calls);                      // exactly one follow-up run
        gates[1].SetResult(Ok(TodayReviewState.Submitted));
        await Task.WhenAll(first, second, third);

        Assert.Equal(2, calls);
        Assert.Equal(1, maxRunning);
        Assert.False(shell.HasPendingReview);        // the later answer wins
    }

    [Fact]
    public async Task Entering_a_main_screen_triggers_a_refresh_but_login_does_not()
    {
        var shell = Create();
        ReviewReturns(Ok(TodayReviewState.NotSubmitted));

        NavigateTo(Screen<LoginViewModel>());
        await _review.DidNotReceive().GetTodayAsync(Arg.Any<CancellationToken>());

        _session.Start(Dinda);
        NavigateTo(Screen<DashboardViewModel>());
        await Task.Delay(100);

        await _review.Received(1).GetTodayAsync(Arg.Any<CancellationToken>());
        Assert.True(shell.HasPendingReview);
    }

    [Fact]
    public async Task RefreshPendingCommand_wraps_the_public_method()
    {
        var shell = Create();
        _session.Start(Dinda);
        ReviewReturns(Ok(TodayReviewState.NotSubmitted));

        await shell.RefreshPendingCommand.ExecuteAsync(null);

        Assert.True(shell.HasPendingReview);
    }
}
