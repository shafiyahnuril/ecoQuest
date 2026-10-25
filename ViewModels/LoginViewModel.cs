using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// Login screen S-01 (UIR-20, SDD-Frontend 3.2). Shows <see cref="Result.Message"/> exactly as returned.
/// InvalidCredentials clears the password and refocuses it; any other failure (e.g. NetworkError) keeps both fields so the user can press "Masuk" again.
/// </summary>
public sealed partial class LoginViewModel(IAuthService auth, INavigationService navigation) : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _identifier = "";

    // A public string only because binding needs it (SDD-Frontend 3.2). Never log it or put it in ToString/messages;
    // it is cleared after a successful login and after InvalidCredentials, and the view hides it again on submit.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = "";

    /// <summary>Incremented to ask the view to move focus to the password box (bound to PasswordBoxHelper.FocusRequest).</summary>
    [ObservableProperty] private int _passwordFocusRequest;

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Identifier) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken ct)
    {
        // Execute() on an async command does not re-check CanExecute, so guard here against a second submit.
        if (!CanLogin())
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        LoginCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await auth.LoginAsync(new LoginRequest(Identifier.Trim(), Password), ct);
            if (result.IsSuccess)
            {
                Password = "";
                await navigation.NavigateTo<DashboardViewModel>();
                return;
            }

            ErrorMessage = result.Message;
            if (result.Error == ErrorCode.InvalidCredentials)
            {
                Password = "";
                PasswordFocusRequest++;
            }
        }
        finally
        {
            IsBusy = false;
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private Task GoToRegisterAsync() => navigation.NavigateTo<RegisterViewModel>();
}
