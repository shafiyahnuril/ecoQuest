using CommunityToolkit.Mvvm.Input;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

// STUB (FE-02). To be replaced entirely by FE-04 (Login view and viewmodel).
public sealed partial class LoginViewModel(INavigationService navigation) : ViewModelBase
{
    // TODO(FE-04): TEMPORARY, pure navigation so the main area can be reached before authentication exists.
    // It must not call IAuthService or the network; FE-04 replaces it with the real LoginCommand.
    [RelayCommand]
    private Task LoginAsync() => navigation.NavigateTo<DashboardViewModel>();
}
