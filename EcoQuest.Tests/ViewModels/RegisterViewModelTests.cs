using System.ComponentModel.DataAnnotations;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class RegisterViewModelTests
{
    private static readonly UserSession Dinda = new(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null);

    // The format rules and their messages come from the real fake service (SRS FR-1.2); everything else is substituted.
    private readonly FakeWorld _world = new(FakeScenario.Normal);
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly IProfileService _profile = Substitute.For<IProfileService>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    public RegisterViewModelTests()
    {
        _auth.ValidateRegisterFormat(Arg.Any<RegisterRequest>()).Returns(call => _world.Auth.ValidateRegisterFormat(call.Arg<RegisterRequest>()));
        _profile.GetSupportedCities().Returns(FakeData.Cities);
        RegisterReturns(Result<UserSession>.Success(Dinda));
    }

    private void RegisterReturns(Result<UserSession> result) =>
        _auth.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>()).Returns(result);

    private RegisterViewModel Create() => new(_auth, _profile, _nav);

    private RegisterViewModel CreateFilled()
    {
        var vm = Create();
        vm.UserName = "dinda_2026";
        vm.Email = "dinda@mail.ugm.ac.id";
        vm.Password = "Rahasia123";
        vm.ConfirmPassword = "Rahasia123";
        return vm;
    }

    private static string? ErrorOf(RegisterViewModel vm, string field) =>
        vm.GetErrors(field).OfType<ValidationResult>().Select(r => r.ErrorMessage).FirstOrDefault();

    private static void Blur(RegisterViewModel vm, string field) => vm.ValidateFieldCommand.Execute(field);

    private string ServiceMessage(RegisterViewModel vm, string field) =>
        _world.Auth.ValidateRegisterFormat(new RegisterRequest(vm.UserName.Trim(), vm.Email.Trim(), vm.Password, vm.ConfirmPassword, vm.City))[field];

    // ---------- initial state ----------

    [Fact]
    public void Cities_come_from_the_profile_service_and_start_on_Yogyakarta()
    {
        var vm = Create();

        Assert.Equal(FakeData.Cities, vm.Cities);
        Assert.Equal("Yogyakarta", vm.City);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void City_is_empty_when_Yogyakarta_is_not_in_the_list()
    {
        _profile.GetSupportedCities().Returns(new[] { "Jakarta", "Bandung" });

        Assert.Equal("", Create().City);
    }

    [Fact]
    public void Typing_shows_no_error_before_the_field_is_left()
    {
        var vm = Create();

        vm.UserName = "a";
        vm.Email = "x";
        vm.Password = "1";

        Assert.False(vm.HasErrors);
    }

    // ---------- format rules per field (validated on blur) ----------

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("bad name")]
    [InlineData("bad!")]
    public void Username_with_a_bad_format_gets_the_service_message_on_blur(string userName)
    {
        var vm = Create();
        vm.UserName = userName;

        Blur(vm, "UserName");

        Assert.Equal(ServiceMessage(vm, "UserName"), ErrorOf(vm, "UserName"));
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Username_longer_than_30_is_an_error_and_a_valid_one_is_not()
    {
        var vm = Create();
        vm.UserName = new string('a', 31);
        Blur(vm, "UserName");
        Assert.NotNull(ErrorOf(vm, "UserName"));

        vm.UserName = "dinda_1.x";     // fixes it while typing: the field was already left once
        Assert.Null(ErrorOf(vm, "UserName"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("foo")]
    [InlineData("a@b")]
    public void Email_with_a_bad_format_gets_the_service_message_on_blur(string email)
    {
        var vm = Create();
        vm.Email = email;

        Blur(vm, "Email");

        Assert.Equal(ServiceMessage(vm, "Email"), ErrorOf(vm, "Email"));
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    public void Password_without_letters_and_digits_or_too_short_is_an_error(string password)
    {
        var vm = Create();
        vm.Password = password;

        Blur(vm, "Password");

        Assert.Equal(ServiceMessage(vm, "Password"), ErrorOf(vm, "Password"));
    }

    [Fact]
    public void Password_longer_than_64_is_an_error_and_8_to_64_with_letters_and_digits_is_valid()
    {
        var vm = Create();
        vm.Password = new string('a', 64) + "1";
        Blur(vm, "Password");
        Assert.NotNull(ErrorOf(vm, "Password"));

        vm.Password = "Rahasia123";
        Assert.Null(ErrorOf(vm, "Password"));
    }

    [Fact]
    public void City_is_required_and_must_be_in_the_list()
    {
        var vm = Create();
        vm.City = "";
        Blur(vm, "City");
        Assert.Equal(ServiceMessage(vm, "City"), ErrorOf(vm, "City"));

        vm.City = "Bandung";
        Assert.Null(ErrorOf(vm, "City"));
    }

    [Fact]
    public void Only_the_blurred_field_shows_its_error()
    {
        var vm = Create();           // every text field is still empty (and invalid)

        Blur(vm, "UserName");

        Assert.NotNull(ErrorOf(vm, "UserName"));
        Assert.Null(ErrorOf(vm, "Email"));
        Assert.Null(ErrorOf(vm, "Password"));
    }

    // ---------- password confirmation ----------

    [Fact]
    public void Confirmation_must_equal_the_password()
    {
        var vm = CreateFilled();
        vm.ConfirmPassword = "Rahasia124";

        Blur(vm, "ConfirmPassword");

        Assert.Equal(ServiceMessage(vm, "ConfirmPassword"), ErrorOf(vm, "ConfirmPassword"));
    }

    [Fact]
    public void Changing_the_password_revalidates_the_confirmation_in_both_directions()
    {
        var vm = CreateFilled();
        Blur(vm, "Password");
        Blur(vm, "ConfirmPassword");
        Assert.Null(ErrorOf(vm, "ConfirmPassword"));

        vm.Password = "Rahasia1234";                       // no longer equal: the error appears without leaving any field
        Assert.NotNull(ErrorOf(vm, "ConfirmPassword"));

        vm.Password = "Rahasia123";                        // equal again: it disappears
        Assert.Null(ErrorOf(vm, "ConfirmPassword"));

        vm.ConfirmPassword = "x";                          // typing in the confirmation re-evaluates it too
        Assert.NotNull(ErrorOf(vm, "ConfirmPassword"));
        vm.ConfirmPassword = "Rahasia123";
        Assert.Null(ErrorOf(vm, "ConfirmPassword"));
    }

    [Fact]
    public void An_untouched_empty_confirmation_stays_quiet_while_the_password_is_typed()
    {
        var vm = Create();

        vm.Password = "Rahasia123";

        Assert.Null(ErrorOf(vm, "ConfirmPassword"));
        Assert.False(vm.HasErrors);
    }

    // ---------- submit ----------

    [Fact]
    public async Task Submit_with_invalid_fields_marks_every_bad_field_and_does_not_call_the_service()
    {
        var vm = Create();

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.NotNull(ErrorOf(vm, "UserName"));
        Assert.NotNull(ErrorOf(vm, "Email"));
        Assert.NotNull(ErrorOf(vm, "Password"));
        Assert.Null(ErrorOf(vm, "City"));
        Assert.False(vm.RegisterCommand.CanExecute(null));
        await _auth.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Success_navigates_to_dashboard_and_clears_both_passwords()
    {
        var vm = CreateFilled();

        await vm.RegisterCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
        Assert.Equal("", vm.Password);
        Assert.Equal("", vm.ConfirmPassword);
        Assert.False(vm.HasErrors);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Request_carries_trimmed_name_and_email_and_the_passwords_unchanged()
    {
        var vm = CreateFilled();
        vm.UserName = "  dinda_2026  ";
        vm.Email = " dinda@mail.ugm.ac.id ";

        await vm.RegisterCommand.ExecuteAsync(null);

        await _auth.Received(1).RegisterAsync(
            new RegisterRequest("dinda_2026", "dinda@mail.ugm.ac.id", "Rahasia123", "Rahasia123", "Yogyakarta"), Arg.Any<CancellationToken>());
    }

    // ---------- server errors ----------

    [Fact]
    public async Task UsernameTaken_goes_to_the_username_field_only_and_editing_it_frees_the_button()
    {
        const string message = "Username sudah digunakan";
        RegisterReturns(Result<UserSession>.Failure(ErrorCode.UsernameTaken, message,
            new Dictionary<string, string> { ["UserName"] = message }));
        var vm = CreateFilled();

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(message, ErrorOf(vm, "UserName"));
        Assert.Null(ErrorOf(vm, "Email"));
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.RegisterCommand.CanExecute(null));
        await _nav.DidNotReceive().NavigateTo<DashboardViewModel>(Arg.Any<object?>());

        vm.UserName = "dinda_2027";

        Assert.Null(ErrorOf(vm, "UserName"));
        Assert.True(vm.RegisterCommand.CanExecute(null));
        Assert.Equal("Rahasia123", vm.Password);      // the typed passwords are kept so the user can retry
    }

    [Fact]
    public async Task EmailTaken_goes_to_the_email_field_only()
    {
        const string message = "Email sudah terdaftar";
        RegisterReturns(Result<UserSession>.Failure(ErrorCode.EmailTaken, message,
            new Dictionary<string, string> { ["Email"] = message }));
        var vm = CreateFilled();

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(message, ErrorOf(vm, "Email"));
        Assert.Null(ErrorOf(vm, "UserName"));
        Assert.False(vm.RegisterCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failure_without_field_errors_shows_the_message_and_allows_a_retry()
    {
        const string message = "Tidak dapat terhubung ke server. Periksa koneksi internet lalu coba lagi";
        RegisterReturns(Result<UserSession>.Failure(ErrorCode.NetworkError, message));
        var vm = CreateFilled();

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.ErrorMessage);
        Assert.False(vm.HasErrors);
        Assert.Equal("dinda_2026", vm.UserName);
        Assert.Equal("Rahasia123", vm.Password);
        Assert.True(vm.RegisterCommand.CanExecute(null));

        RegisterReturns(Result<UserSession>.Success(Dinda));
        await vm.RegisterCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorMessage);
    }

    // ---------- busy / double submit ----------

    [Fact]
    public async Task While_the_request_runs_the_button_is_disabled_and_a_second_submit_is_ignored()
    {
        var gate = new TaskCompletionSource<Result<UserSession>>();
        _auth.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = CreateFilled();

        var first = vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.RegisterCommand.CanExecute(null));
        vm.RegisterCommand.Execute(null);                   // e.g. Enter pressed again
        await _auth.Received(1).RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());

        gate.SetResult(Result<UserSession>.Success(Dinda));
        await first;

        Assert.False(vm.IsBusy);
        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
    }

    [Fact]
    public void The_button_is_disabled_exactly_while_there_is_an_error()
    {
        var vm = CreateFilled();
        Assert.True(vm.RegisterCommand.CanExecute(null));

        vm.UserName = "ab";
        Blur(vm, "UserName");
        Assert.False(vm.RegisterCommand.CanExecute(null));

        vm.UserName = "abc";
        Assert.True(vm.RegisterCommand.CanExecute(null));
    }

    // ---------- login link ----------

    [Fact]
    public async Task GoToLogin_navigates_to_login()
    {
        await Create().GoToLoginCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<LoginViewModel>(null);
    }
}
