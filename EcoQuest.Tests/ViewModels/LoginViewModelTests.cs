using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class LoginViewModelTests
{
    private static readonly UserSession Dinda = new(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null);

    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    private LoginViewModel Create(string identifier = "dinda", string password = "Rahasia123") =>
        new(_auth, _nav) { Identifier = identifier, Password = password };

    private void AuthReturns(Result<UserSession> result) =>
        _auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(result);

    // ---------- CanExecute ----------

    [Theory]
    [InlineData("", "")]
    [InlineData("dinda", "")]
    [InlineData("", "Rahasia123")]
    [InlineData("   ", "Rahasia123")]
    public void Login_is_disabled_while_a_field_is_empty(string identifier, string password)
    {
        var vm = Create(identifier, password);
        Assert.False(vm.LoginCommand.CanExecute(null));
    }

    [Fact]
    public void Login_is_enabled_when_both_fields_are_filled()
    {
        Assert.True(Create().LoginCommand.CanExecute(null));
    }

    [Fact]
    public void Editing_a_field_updates_the_command_state()
    {
        var vm = Create(password: "");
        var changes = 0;
        vm.LoginCommand.CanExecuteChanged += (_, _) => changes++;

        vm.Password = "x";

        Assert.True(changes > 0);
        Assert.True(vm.LoginCommand.CanExecute(null));
    }

    // ---------- success ----------

    [Fact]
    public async Task Success_navigates_to_dashboard_and_clears_the_password()
    {
        AuthReturns(Result<UserSession>.Success(Dinda));
        var vm = Create();

        await vm.LoginCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
        await _nav.DidNotReceive().NavigateTo<OnboardingViewModel>(Arg.Any<object?>());
        Assert.Equal("", vm.Password);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Request_uses_trimmed_identifier_and_the_password_unchanged()
    {
        AuthReturns(Result<UserSession>.Success(Dinda));
        var vm = Create("  dinda  ", " Rahasia123 ");

        await vm.LoginCommand.ExecuteAsync(null);

        await _auth.Received(1).LoginAsync(new LoginRequest("dinda", " Rahasia123 "), Arg.Any<CancellationToken>());
    }

    // ---------- failures ----------

    [Fact]
    public async Task InvalidCredentials_shows_the_message_as_is_clears_password_and_requests_focus()
    {
        const string message = "Kombinasi username/email dan password salah";
        AuthReturns(Result<UserSession>.Failure(ErrorCode.InvalidCredentials, message));
        var vm = Create(password: "salah");
        var focusBefore = vm.PasswordFocusRequest;

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.ErrorMessage);
        Assert.Equal("", vm.Password);
        Assert.Equal("dinda", vm.Identifier);
        Assert.Equal(focusBefore + 1, vm.PasswordFocusRequest);
        Assert.False(vm.IsBusy);
        await _nav.DidNotReceive().NavigateTo<DashboardViewModel>(Arg.Any<object?>());
    }

    [Fact]
    public async Task NetworkError_keeps_both_fields_and_shows_the_message()
    {
        const string message = "Tidak dapat terhubung ke server. Periksa koneksi internet lalu coba lagi";
        AuthReturns(Result<UserSession>.Failure(ErrorCode.NetworkError, message));
        var vm = Create();
        var focusBefore = vm.PasswordFocusRequest;

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.ErrorMessage);
        Assert.Equal("dinda", vm.Identifier);
        Assert.Equal("Rahasia123", vm.Password);
        Assert.Equal(focusBefore, vm.PasswordFocusRequest);
        Assert.True(vm.LoginCommand.CanExecute(null));      // pressing "Masuk" again is the retry
        await _nav.DidNotReceive().NavigateTo<DashboardViewModel>(Arg.Any<object?>());
    }

    [Fact]
    public async Task A_new_attempt_clears_the_previous_error_first()
    {
        AuthReturns(Result<UserSession>.Failure(ErrorCode.NetworkError, "gagal"));
        var vm = Create();
        await vm.LoginCommand.ExecuteAsync(null);
        Assert.Equal("gagal", vm.ErrorMessage);

        AuthReturns(Result<UserSession>.Success(Dinda));
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
    }

    // ---------- busy / double submit ----------

    [Fact]
    public async Task While_the_request_runs_the_button_is_disabled_and_a_second_submit_is_ignored()
    {
        var gate = new TaskCompletionSource<Result<UserSession>>();
        _auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = Create();

        var first = vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.LoginCommand.CanExecute(null));
        vm.LoginCommand.Execute(null);                       // e.g. Enter pressed again
        await _auth.Received(1).LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>());

        gate.SetResult(Result<UserSession>.Success(Dinda));
        await first;

        Assert.False(vm.IsBusy);
        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
    }

    // ---------- register link ----------

    [Fact]
    public async Task GoToRegister_navigates_to_register()
    {
        await Create().GoToRegisterCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<RegisterViewModel>(null);
    }

    // ---------- network error banner ----------

    [Fact]
    public async Task NetworkError_shows_the_banner_and_retry_resubmits_the_same_input()
    {
        AuthReturns(Result<UserSession>.Failure(ErrorCode.NetworkError, "gagal"));
        var vm = Create();

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasNetworkError);
        Assert.Equal("gagal", vm.ErrorMessage);

        AuthReturns(Result<UserSession>.Success(Dinda));
        await vm.LoginCommand.ExecuteAsync(null);                  // what the banner's "Coba lagi" runs

        await _auth.Received(2).LoginAsync(Arg.Is<LoginRequest>(r => r.Identifier == "dinda" && r.Password == "Rahasia123"), Arg.Any<CancellationToken>());
        Assert.False(vm.HasNetworkError);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Other_errors_do_not_show_the_banner()
    {
        AuthReturns(Result<UserSession>.Failure(ErrorCode.InvalidCredentials, "salah"));
        var vm = Create();

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(vm.HasNetworkError);
        Assert.Equal("salah", vm.ErrorMessage);
        Assert.Equal("", vm.Password);
    }

    [Fact]
    public async Task Editing_a_field_hides_the_banner()
    {
        AuthReturns(Result<UserSession>.Failure(ErrorCode.NetworkError, "gagal"));
        var vm = Create();
        await vm.LoginCommand.ExecuteAsync(null);

        vm.Identifier = "dinda2";

        Assert.False(vm.HasNetworkError);
    }

    [Fact]
    public async Task The_banner_is_hidden_and_retry_is_disabled_while_the_retry_runs()
    {
        AuthReturns(Result<UserSession>.Failure(ErrorCode.NetworkError, "gagal"));
        var vm = Create();
        await vm.LoginCommand.ExecuteAsync(null);

        var gate = new TaskCompletionSource<Result<UserSession>>();
        _auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var retry = vm.LoginCommand.ExecuteAsync(null);

        Assert.False(vm.HasNetworkError);
        Assert.False(vm.LoginCommand.CanExecute(null));
        vm.LoginCommand.Execute(null);
        await _auth.Received(2).LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>());

        gate.SetResult(Result<UserSession>.Success(Dinda));
        await retry;
    }
}
