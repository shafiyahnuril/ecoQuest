
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// Drives MainWindow (SDD-Frontend 3.1): which screen is shown, the sidebar menu and logout.
/// Singleton. Mirrors the session into the account block (UIR-25), owns logout (UIR-23) and the amber dot (UIR-56).
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IUiText _text;
    private readonly ISessionService _session;
    private readonly IAuthService _auth;
    private readonly IEveningReviewService _review;
    private readonly IPhotoStorageService? _photos;
    private readonly NavItemViewModel _reviewItem;
    private int _refreshRunning;
    private volatile bool _refreshAgain;
    private bool _disposed;

    public ShellViewModel(
        INavigationService navigation,
        IDialogService dialogs,
        IUiText text,
        ToastViewModel toast,
        ISessionService session,
        IAuthService auth,
        IEveningReviewService review,
        IPhotoStorageService? photos = null)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _text = text;
        Toast = toast;
        _session = session;
        _auth = auth;
        _review = review;
        _photos = photos;

        _reviewItem = new NavItemViewModel(
            MainMenu.EveningReview,
            "Nav.Review",
            "Icon.BookOpen"
        );

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
        _session.SessionChanged += OnSessionChanged;

        ApplySession();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _navigation.Navigated -= OnNavigated;
        _session.SessionChanged -= OnSessionChanged;
    }

    public ToastViewModel Toast { get; }

    public IReadOnlyList<NavItemViewModel> MenuItems { get; }

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    /// <summary>
    /// Screen shown in the auth area (no sidebar).
    /// Includes Login, Register, Onboarding, and ConnectionError.
    /// At most one of Auth/MainViewModel is non-null, so a view is never built twice.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _authViewModel;


    /// <summary>
    /// Screen shown in the main area (sidebar + page).
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _mainViewModel;

    /// <summary>
    /// True when the authenticated main area (sidebar) is shown.
    /// False for Login, Register, Onboarding, and ConnectionError.
    /// </summary>

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateCommand))]
    private bool _isAuthenticated;

    /// <summary>
    /// Menu entry of the current screen.
    /// Null on screens without a corresponding menu entry.
    /// </summary>
    [ObservableProperty]
    private MainMenu? _selectedMenu;

    /// <summary>
    /// Amber dot on Evening Review.
    /// </summary>
    [ObservableProperty]
    private bool _hasPendingReview;

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _city = "";

    [ObservableProperty]
    private string? _avatarImagePath;

    /// <summary>
    /// First letter of the username.
    /// </summary>
    [ObservableProperty]
    private string _avatarInitial = "";

    partial void OnHasPendingReviewChanged(bool value)
    {
        _reviewItem.IsPendingIndicatorVisible = value;
    }

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
            case MainMenu.Dashboard:
                await _navigation.NavigateTo<DashboardViewModel>();
                break;

            case MainMenu.EveningReview:
                await _navigation.NavigateTo<EveningReviewViewModel>();
                break;

            case MainMenu.History:
                await _navigation.NavigateTo<HistoryViewModel>();
                break;

            case MainMenu.Achievements:
                await _navigation.NavigateTo<AchievementsViewModel>();
                break;

            case MainMenu.Leaderboard:
                await _navigation.NavigateTo<LeaderboardViewModel>();
                break;

            case MainMenu.Profile:
                await _navigation.NavigateTo<ProfileViewModel>();
                break;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _text.Get("Logout.Title"),
            _text.Get("Logout.Body"),
            _text.Get("Logout.Confirm")
        );

        if (!confirmed)
        {
            return;
        }

        _auth.Logout();

        HasPendingReview = false;

        _navigation.ClearHistory();

        await _navigation.NavigateTo<LoginViewModel>();
    }

    /// <summary>
    /// Re-reads today's review state.
    /// Only one refresh runs at a time.
    /// </summary>
    public async Task RefreshPendingAsync()
    {
        if (!_session.IsAuthenticated)
        {
            return;
        }

        if (Interlocked.CompareExchange(
            ref _refreshRunning, 1, 0) != 0)
        {
            _refreshAgain = true;
            return;
        }

        try
        {
            do
            {
                _refreshAgain = false;

                var result = await _review.GetTodayAsync();

                if (result.IsSuccess &&
                    result.Value is not null &&
                    _session.IsAuthenticated)
                {
                    HasPendingReview =
                        result.Value.State ==
                        TodayReviewState.NotSubmitted;
                }
            }
            while (_refreshAgain);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshRunning, 0);
        }
    }

    [RelayCommand]
    private Task RefreshPending()
    {
        return RefreshPendingAsync();
    }

    [RelayCommand]
    private void OpenThemePreview()
    {
        _dialogs.ShowThemePreview();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        ApplySession();
        UpdateIsAuthenticated();
    }

    private void ApplySession()
    {
        var user = _session.Current;

        DisplayName = user?.UserName ?? "";
        City = user?.City ?? "";
        // The session keeps the stored relative path; the image converter needs a file that exists on this device.
        AvatarImagePath = _photos?.ResolveAbsolutePath(user?.AvatarPath) ?? user?.AvatarPath;

        AvatarInitial = string.IsNullOrWhiteSpace(user?.UserName)
            ? ""
            : user.UserName.Trim()[..1].ToUpperInvariant();
    }

    /// <summary>
    /// Determines whether a screen belongs to the main area.
    ///
    /// ARC-07:
    /// ConnectionErrorViewModel must be displayed before login,
    /// so it belongs to the auth area rather than the main area.
    /// </summary>
    private static bool IsMainAreaScreen(ViewModelBase? screen) =>
        screen is not null and not (
            LoginViewModel or
            RegisterViewModel or
            OnboardingViewModel or
            ConnectionErrorViewModel
        );

    private void UpdateIsAuthenticated()
    {
        IsAuthenticated =
            _session.IsAuthenticated &&
            IsMainAreaScreen(_navigation.Current);
    }

    private void OnNavigated(object? sender, EventArgs e)
    {
        var current = _navigation.Current;

        CurrentViewModel = current;

        UpdateIsAuthenticated();

        if (IsMainAreaScreen(current))
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

        if (IsAuthenticated)
        {
            _ = RefreshPendingAsync();
        }
    }
}
