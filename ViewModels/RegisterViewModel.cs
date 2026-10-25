using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// Register screen S-02 (UIR-21/22, SDD-Frontend 3.3). Field errors live in the <see cref="ObservableValidator"/> store (INotifyDataErrorInfo), so the
/// view shows them per field. The messages are never written here: format errors come from <see cref="IAuthService.ValidateRegisterFormat"/>
/// and server errors from <see cref="EcoQuest.Core.Common.Result.FieldErrors"/>, both shown exactly as returned.
/// </summary>
public sealed partial class RegisterViewModel : ViewModelBase
{
    private const string DefaultCity = "Yogyakarta";

    // Names equal the keys of ValidateRegisterFormat / FieldErrors (RegisterRequest property names).
    private static readonly string[] Fields = [nameof(UserName), nameof(Email), nameof(Password), nameof(ConfirmPassword), nameof(City)];

    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;

    // Fields the user has left (or a submit has checked). Only these are re-validated while typing, so no error shows before the first blur.
    private readonly HashSet<string> _touched = [];
    private readonly Dictionary<string, string> _serverErrors = [];
    private IReadOnlyDictionary<string, string> _formatErrors = new Dictionary<string, string>();
    private bool _isResetting;

    public RegisterViewModel(IAuthService auth, IProfileService profile, INavigationService navigation)
    {
        _auth = auth;
        _navigation = navigation;
        Cities = profile.GetSupportedCities();
        City = Cities.Contains(DefaultCity) ? DefaultCity : "";
        ErrorsChanged += (_, _) => RegisterCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<string> Cities { get; }

    [ObservableProperty]
    [property: CustomValidation(typeof(RegisterViewModel), nameof(CheckField))]
    private string _userName = "";

    [ObservableProperty]
    [property: CustomValidation(typeof(RegisterViewModel), nameof(CheckField))]
    private string _email = "";

    // Public strings only because binding needs them (SDD-Frontend 3.3). Never log them or put them in ToString/messages;
    // both are cleared after a successful registration and the view hides them again.
    [ObservableProperty]
    [property: CustomValidation(typeof(RegisterViewModel), nameof(CheckField))]
    private string _password = "";

    [ObservableProperty]
    [property: CustomValidation(typeof(RegisterViewModel), nameof(CheckField))]
    private string _confirmPassword = "";

    [ObservableProperty]
    [property: CustomValidation(typeof(RegisterViewModel), nameof(CheckField))]
    private string _city = "";

    /// <summary>Validation hook shared by every field: hands the stored service message (server error first) to the validator.</summary>
    public static ValidationResult? CheckField(string? value, ValidationContext context)
    {
        var vm = (RegisterViewModel)context.ObjectInstance;
        return vm.MessageFor(context.MemberName ?? "") is { } message ? new ValidationResult(message) : ValidationResult.Success;
    }

    private string? MessageFor(string field) =>
        _serverErrors.TryGetValue(field, out var server) ? server
        : _formatErrors.TryGetValue(field, out var format) ? format
        : null;

    partial void OnUserNameChanged(string value) => OnFieldEdited(nameof(UserName));

    partial void OnEmailChanged(string value) => OnFieldEdited(nameof(Email));

    partial void OnPasswordChanged(string value) => OnFieldEdited(nameof(Password));

    partial void OnConfirmPasswordChanged(string value) => OnFieldEdited(nameof(ConfirmPassword));

    partial void OnCityChanged(string value) => OnFieldEdited(nameof(City));

    private void OnFieldEdited(string field)
    {
        if (_isResetting)
        {
            return;
        }

        // A server error (UsernameTaken ...) describes the old value; editing the field retires it.
        var hadServerError = _serverErrors.Remove(field);

        if (_touched.Contains(field))
        {
            ValidateOne(field);
        }
        else if (hadServerError)
        {
            ClearErrors(field);
        }

        // Password and its confirmation depend on each other: keep the "tidak sama" error in step with both.
        if (field == nameof(Password) && (_touched.Contains(nameof(ConfirmPassword)) || ConfirmPassword.Length > 0))
        {
            _touched.Add(nameof(ConfirmPassword));
            ValidateOne(nameof(ConfirmPassword));
        }
        else if (field == nameof(ConfirmPassword) && _touched.Contains(nameof(Password)))
        {
            ValidateOne(nameof(Password));
        }
    }

    private object? ValueOf(string field) => field switch
    {
        nameof(UserName) => UserName,
        nameof(Email) => Email,
        nameof(Password) => Password,
        nameof(ConfirmPassword) => ConfirmPassword,
        nameof(City) => City,
        _ => null,
    };

    private RegisterRequest BuildRequest() => new(UserName.Trim(), Email.Trim(), Password, ConfirmPassword, City ?? "");

    /// <summary>Re-reads the format rules from the service and updates the error of one field only.</summary>
    private void ValidateOne(string field)
    {
        _formatErrors = _auth.ValidateRegisterFormat(BuildRequest());
        ValidateProperty(ValueOf(field), field);
    }

    private void ValidateAll()
    {
        _formatErrors = _auth.ValidateRegisterFormat(BuildRequest());
        foreach (var field in Fields)
        {
            _touched.Add(field);
            ValidateProperty(ValueOf(field), field);
        }
    }

    /// <summary>Puts server-side field errors on their fields. Returns false when none of the keys belongs to a field.</summary>
    private bool ApplyFieldErrors(IReadOnlyDictionary<string, string> errors)
    {
        var applied = false;
        foreach (var (field, message) in errors)
        {
            if (!Fields.Contains(field))
            {
                continue;
            }

            _serverErrors[field] = message;
            _touched.Add(field);
            ValidateProperty(ValueOf(field), field);
            applied = true;
        }

        return applied;
    }

    private void ResetSecrets()
    {
        _isResetting = true;
        try
        {
            Password = "";
            ConfirmPassword = "";
        }
        finally
        {
            _isResetting = false;
        }

        foreach (var field in new[] { nameof(Password), nameof(ConfirmPassword) })
        {
            _touched.Remove(field);
            _serverErrors.Remove(field);
            ClearErrors(field);
        }
    }

    /// <summary>Called when a field loses keyboard focus (UIR-21): shows the error of that field only.</summary>
    [RelayCommand]
    private void ValidateField(string? propertyName)
    {
        if (propertyName is null || !Fields.Contains(propertyName) || IsBusy)
        {
            return;
        }

        _touched.Add(propertyName);
        ValidateOne(propertyName);

        if (propertyName == nameof(Password) && ConfirmPassword.Length > 0)
        {
            _touched.Add(nameof(ConfirmPassword));
            ValidateOne(nameof(ConfirmPassword));
        }
    }

    private bool CanRegister() => !IsBusy && !HasErrors;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync(CancellationToken ct)
    {
        // Execute() on an async command does not re-check CanExecute, so guard here against a second submit.
        if (!CanRegister())
        {
            return;
        }

        ErrorMessage = null;
        ValidateAll();
        if (HasErrors)
        {
            return;
        }

        IsBusy = true;
        RegisterCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await _auth.RegisterAsync(BuildRequest(), ct);
            if (result.IsSuccess)
            {
                ResetSecrets();
                // TODO(UIR-22): navigate to OnboardingViewModel once the Onboarding screen exists (it is still a stub); until then go to the Dashboard.
                await _navigation.NavigateTo<DashboardViewModel>();
                return;
            }

            if (!ApplyFieldErrors(result.FieldErrors))
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            IsBusy = false;
            RegisterCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private Task GoToLoginAsync() => _navigation.NavigateTo<LoginViewModel>();
}
