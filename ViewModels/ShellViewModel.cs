using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// Drives MainWindow (SDD-Frontend 3.1): which screen is shown, the sidebar menu and logout.
/// Singleton. Intentionally holds NO service from Core/Contracts yet; see the TODO block below.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IUiText _text;
    private readonly NavItemViewModel _reviewItem;

    public ShellViewModel(INavigationService navigation, IDialogService dialogs, IUiText text, ToastViewModel toast)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _text = text;
        Toast = toast;

        _reviewItem = new NavItemViewModel(MainMenu.EveningReview, "Nav.Review", "Icon.BookOpen");
        MenuItems =
        [
            new NavItemViewModel(MainMenu.Dashboard, "Nav.Dashboard", "Icon.Home"),
            _reviewItem,
            new NavItemViewModel(MainMenu.History, "Nav.History", "Icon.History"),
            new NavItemViewModel(MainMenu.Achievements, "Nav.Achievements", "Icon.Award"),
            new NavItemViewModel(MainMenu.Leaderboard, "Nav.Leaderboard", "Icon.Trophy"),
            new NavItemViewModel(MainMenu.Profile, "Nav.Profile", "Icon.User"),
        ];

        _navigation.Navigated += OnNavigated;
    }

    public ToastViewModel Toast { get; }

    public IReadOnlyList<NavItemViewModel> MenuItems { get; }

    [ObservableProperty] private ViewModelBase? _currentViewModel;

    /// <summary>Screen shown in the auth area (no sidebar). At most one of Auth/MainViewModel is non-null, so a view is never built twice.</summary>
    [ObservableProperty] private ViewModelBase? _authViewModel;

    /// <summary>Screen shown in the main area (sidebar + page).</summary>
    [ObservableProperty] private ViewModelBase? _mainViewModel;

    /// <summary>True when the sidebar (main area) is shown; false for Login, Register and Onboarding (auth area).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateCommand))]
    private bool _isAuthenticated;

    /// <summary>Menu entry of the current screen; null on screens that have none (e.g. the connection-error screen).</summary>
    [ObservableProperty] private MainMenu? _selectedMenu;

    /// <summary>Amber dot on "Evening Review". Stays false until the review contract exists (D-05).</summary>
    [ObservableProperty] private bool _hasPendingReview;

    // TODO(after Core/Contracts exists): the account block of the sidebar needs these, fed by ISessionService.SessionChanged (UIR-25):
    //   [ObservableProperty] string UserName, string City, string? AvatarImagePath  (initial letter + local avatar, UIR-24)
    //   HasPendingReview must be refreshed through IEveningReviewService.GetTodayAsync via RefreshPendingAsync() (UIR-56).
    //   IsAuthenticated must come from the session instead of the current screen type, and LogoutCommand must also call
    //   IAuthService.Logout before navigating. Until then the account block in MainWindow.xaml shows no name, city or avatar.

    partial void OnHasPendingReviewChanged(bool value) => _reviewItem.IsPendingIndicatorVisible = value;

    private bool CanNavigate() => IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private async Task NavigateAsync(MainMenu menu)
    {
        if (SelectedMenu == menu)
        {
            return;
        }

        switch (menu)
        {
            case MainMenu.Dashboard: await _navigation.NavigateTo<DashboardViewModel>(); break;
            case MainMenu.EveningReview: await _navigation.NavigateTo<EveningReviewViewModel>(); break;
            case MainMenu.History: await _navigation.NavigateTo<HistoryViewModel>(); break;
            case MainMenu.Achievements: await _navigation.NavigateTo<AchievementsViewModel>(); break;
            case MainMenu.Leaderboard: await _navigation.NavigateTo<LeaderboardViewModel>(); break;
            case MainMenu.Profile: await _navigation.NavigateTo<ProfileViewModel>(); break;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _text.Get("Logout.Title"), _text.Get("Logout.Body"), _text.Get("Logout.Confirm"));
        if (!confirmed)
        {
            return;
        }

        // TODO(after Core/Contracts exists): IAuthService.Logout() before leaving (FR-1.5).
        HasPendingReview = false;
        _navigation.ClearHistory();
        await _navigation.NavigateTo<LoginViewModel>();
    }

    [RelayCommand]
    private void OpenThemePreview() => _dialogs.ShowThemePreview();

    private void OnNavigated(object? sender, EventArgs e)
    {
        var current = _navigation.Current;
        CurrentViewModel = current;
        IsAuthenticated = current is not null and not (LoginViewModel or RegisterViewModel or OnboardingViewModel);
        if (IsAuthenticated)
        {
            AuthViewModel = null;
            MainViewModel = current;
        }
        else
        {
            MainViewModel = null;
            AuthViewModel = current;
        }

        SelectedMenu = current switch
        {
            DashboardViewModel => MainMenu.Dashboard,
            EveningReviewViewModel => MainMenu.EveningReview,
            HistoryViewModel => MainMenu.History,
            AchievementsViewModel => MainMenu.Achievements,
            LeaderboardViewModel => MainMenu.Leaderboard,
            ProfileViewModel => MainMenu.Profile,
            _ => null,
        };

        foreach (var item in MenuItems)
        {
            item.IsSelected = item.Menu == SelectedMenu;
        }
    }
}
