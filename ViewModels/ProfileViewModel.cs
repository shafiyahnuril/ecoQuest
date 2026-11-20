using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// Profile screen S-10 (UIR-24/25). Every field error is a message the service returned (<see cref="Result.FieldErrors"/>), shown as it came;
/// this class only decides which field it belongs to. The session is updated by <see cref="IProfileService"/> itself, so the sidebar follows
/// through <see cref="ISessionService.SessionChanged"/> without any call from here.
/// </summary>
public sealed partial class ProfileViewModel : ViewModelBase
{
    private static readonly string[] AccountFields = [nameof(City)];
    private static readonly string[] PasswordFields = [nameof(OldPassword), nameof(NewPassword), nameof(ConfirmNewPassword)];

    private readonly IProfileService _profile;
    private readonly IPhotoStorageService _photos;
    private readonly IDialogService _dialogs;
    private readonly IUiText _text;
    private readonly Dictionary<string, string> _serverErrors = [];
    private string? _savedAvatarPath;
    private string? _pendingAvatarSource;
    private bool _removeAvatar;
    private bool _isResetting;

    public ProfileViewModel(IProfileService profile, IPhotoStorageService photos, IDialogService dialogs, IUiText text, IClock clock)
    {
        _profile = profile;
        _photos = photos;
        _dialogs = dialogs;
        _text = text;
        Cities = profile.GetSupportedCities();
        TodayText = UiFormat.LongDate(clock.Today);
        ErrorsChanged += (_, e) =>
        {
            if (e.PropertyName is { } field && (AccountFields.Contains(field) || PasswordFields.Contains(field)))
            {
                OnPropertyChanged(field + "Error");
            }
        };
    }

    public IReadOnlyList<string> Cities { get; }

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    [property: CustomValidation(typeof(ProfileViewModel), nameof(CheckField))]
    private string _city = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    [NotifyCanExecuteChangedFor(nameof(RemoveAvatarCommand))]
    private string? _avatarImagePath;

    [ObservableProperty]
    private string _avatarInitial = "";

    [ObservableProperty]
    private string? _avatarError;

    // Public strings only because binding needs them. Never log them; ResetPasswords clears all three after a successful change.
    [ObservableProperty]
    [property: CustomValidation(typeof(ProfileViewModel), nameof(CheckField))]
    private string _oldPassword = "";

    [ObservableProperty]
    [property: CustomValidation(typeof(ProfileViewModel), nameof(CheckField))]
    private string _newPassword = "";

    [ObservableProperty]
    [property: CustomValidation(typeof(ProfileViewModel), nameof(CheckField))]
    private string _confirmNewPassword = "";

    public string TodayText { get; }

    public bool HasAvatar => !string.IsNullOrEmpty(AvatarImagePath);

    public string? CityError => FirstError(nameof(City));

    public string? OldPasswordError => FirstError(nameof(OldPassword));

    public string? NewPasswordError => FirstError(nameof(NewPassword));

    public string? ConfirmNewPasswordError => FirstError(nameof(ConfirmNewPassword));

    public static ValidationResult? CheckField(string? value, ValidationContext context)
    {
        var vm = (ProfileViewModel)context.ObjectInstance;
        return vm._serverErrors.TryGetValue(context.MemberName ?? "", out var message) ? new ValidationResult(message) : ValidationResult.Success;
    }

    partial void OnCityChanged(string value) => RetireServerError(nameof(City));

    partial void OnOldPasswordChanged(string value) => RetireServerError(nameof(OldPassword));

    partial void OnNewPasswordChanged(string value) => RetireServerError(nameof(NewPassword));

    partial void OnConfirmNewPasswordChanged(string value) => RetireServerError(nameof(ConfirmNewPassword));

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var profile = await RunAsync(_profile.GetProfileAsync, ct);
        if (profile is null)
        {
            return;
        }

        ResetPasswords();
        ApplyProfile(profile);
    }

    private bool CanSubmit() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SaveProfileAsync(CancellationToken ct)
    {
        if (!CanSubmit())
        {
            return;
        }

        ClearAccountErrors();
        SetBusy(true);
        try
        {
            var result = await _profile.UpdateProfileAsync(new UpdateProfileRequest(City, _pendingAvatarSource, _removeAvatar), ct);
            if (result.IsSuccess)
            {
                ApplyProfile(result.Value!);
                _dialogs.ShowToast(_text.Get("Profile.Saved"));
            }
            else if (result.Error == ErrorCode.FileError)
            {
                AvatarError = result.Message;
            }
            else if (!ApplyFieldErrors(result.FieldErrors, AccountFields))
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task ChangePasswordAsync(CancellationToken ct)
    {
        if (!CanSubmit())
        {
            return;
        }

        ClearPasswordErrors();
        SetBusy(true);
        try
        {
            var result = await _profile.ChangePasswordAsync(new ChangePasswordRequest(OldPassword, NewPassword, ConfirmNewPassword), ct);
            if (result.IsSuccess)
            {
                ResetPasswords();
                _dialogs.ShowToast(_text.Get("Profile.PasswordChanged"));
            }
            else if (!ApplyFieldErrors(PasswordErrorsOf(result), PasswordFields))
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand]
    private void PickAvatar()
    {
        if (_dialogs.PickImageFile() is not { } path)
        {
            return;
        }

        var check = _photos.ValidatePhoto(path);
        if (!check.IsSuccess)
        {
            AvatarError = check.Message;
            return;
        }

        AvatarError = null;
        _pendingAvatarSource = path;
        _removeAvatar = false;
        AvatarImagePath = path;
    }

    [RelayCommand(CanExecute = nameof(HasAvatar))]
    private async Task RemoveAvatarAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _text.Get("Profile.RemovePhotoTitle"), _text.Get("Profile.RemovePhotoMessage"), _text.Get("Profile.RemovePhotoConfirm"), icon: DialogIcon.Warning);
        if (!confirmed)
        {
            return;
        }

        AvatarError = null;
        _pendingAvatarSource = null;
        _removeAvatar = _savedAvatarPath is not null;
        AvatarImagePath = null;
    }

    private void ApplyProfile(UserProfileDto profile)
    {
        UserName = profile.UserName;
        Email = profile.Email;
        City = profile.City;
        AvatarInitial = profile.UserName.Trim() is { Length: > 0 } name ? name[..1].ToUpperInvariant() : "";
        _savedAvatarPath = profile.AvatarPath;
        _pendingAvatarSource = null;
        _removeAvatar = false;
        AvatarError = null;
        AvatarImagePath = _photos.ResolveAbsolutePath(profile.AvatarPath);
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        SaveProfileCommand.NotifyCanExecuteChanged();
        ChangePasswordCommand.NotifyCanExecuteChanged();
    }

    private bool ApplyFieldErrors(IReadOnlyDictionary<string, string> errors, string[] fields)
    {
        var applied = false;
        foreach (var (field, message) in errors)
        {
            if (!fields.Contains(field))
            {
                continue;
            }

            _serverErrors[field] = message;
            ValidateProperty(ValueOf(field), field);
            applied = true;
        }

        return applied;
    }

    // The wording of a wrong old password is UI copy; the service message for this code is not shown.
    private IReadOnlyDictionary<string, string> PasswordErrorsOf(Result result) =>
        result.Error == ErrorCode.WrongOldPassword
            ? new Dictionary<string, string>(result.FieldErrors) { [nameof(OldPassword)] = _text.Get("Profile.WrongOldPassword") }
            : result.FieldErrors;

    private string? FirstError(string field) =>
        GetErrors(field).OfType<ValidationResult>().Select(r => r.ErrorMessage).FirstOrDefault();

    private object? ValueOf(string field) => field switch
    {
        nameof(City) => City,
        nameof(OldPassword) => OldPassword,
        nameof(NewPassword) => NewPassword,
        nameof(ConfirmNewPassword) => ConfirmNewPassword,
        _ => null,
    };

    private void RetireServerError(string field)
    {
        if (!_isResetting && _serverErrors.Remove(field))
        {
            ClearErrors(field);
        }
    }

    private void ClearAccountErrors()
    {
        ErrorMessage = null;
        AvatarError = null;
        ClearFieldErrors(AccountFields);
    }

    private void ClearPasswordErrors()
    {
        ErrorMessage = null;
        ClearFieldErrors(PasswordFields);
    }

    private void ClearFieldErrors(string[] fields)
    {
        foreach (var field in fields)
        {
            _serverErrors.Remove(field);
            ClearErrors(field);
        }
    }

    private void ResetPasswords()
    {
        _isResetting = true;
        try
        {
            OldPassword = "";
            NewPassword = "";
            ConfirmNewPassword = "";
        }
        finally
        {
            _isResetting = false;
        }

        ClearFieldErrors(PasswordFields);
    }
}
