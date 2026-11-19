using CommunityToolkit.Mvvm.Input;
using EcoQuest.Infrastructure;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// S-11. Network variant: "Coba lagi" returns to the previous screen (or Login). Configuration variant: lists only the NAMES
/// of the missing .env keys; the configuration cannot be reloaded while running, so there is no retry.
/// </summary>
// TODO(ARC-07): the composition root opens this screen when Supabase is unreachable or required keys are missing.
public sealed partial class ConnectionErrorViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    public ConnectionErrorViewModel(AppConfig config, INavigationService navigation, IUiText text)
    {
        _navigation = navigation;

        var missingKeys = config.MissingRequiredKeys();
        IsConfigurationVariant = missingKeys.Count > 0;
        Title = text.Get("Offline.Title");
        Message = IsConfigurationVariant
            ? string.Format(text.Get("Offline.ConfigBody"), string.Join(", ", missingKeys))
            : text.Get("Offline.Body");
        Hint = IsConfigurationVariant ? text.Get("Offline.ConfigHint") : null;
    }

    public bool IsConfigurationVariant { get; }

    public string Title { get; }

    public string Message { get; }

    public string? Hint { get; }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => _navigation.CanGoBack ? _navigation.GoBackAsync() : _navigation.NavigateTo<LoginViewModel>();

    private bool CanRetry() => !IsConfigurationVariant;
}
