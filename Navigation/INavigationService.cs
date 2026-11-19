using EcoQuest.ViewModels;

namespace EcoQuest.Navigation;

/// <summary>Single entry point for screen changes (UIR-02). View selection happens through DataTemplates in App.xaml.</summary>
public interface INavigationService
{
    ViewModelBase? Current { get; }

    /// <summary>True when one earlier screen is remembered (never the connection-error screen itself).</summary>
    bool CanGoBack { get; }

    event EventHandler? Navigated;

    Task NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;

    /// <summary>Returns to the remembered screen with a fresh instance; does nothing when <see cref="CanGoBack"/> is false.</summary>
    Task GoBackAsync();

    /// <summary>Forgets the current screen and the remembered one so nothing is left to go back to; called on logout (UIR-23).</summary>
    void ClearHistory();
}
