using EcoQuest.ViewModels;

namespace EcoQuest.Navigation;

/// <summary>Single entry point for screen changes (UIR-02). View selection happens through DataTemplates in App.xaml.</summary>
public interface INavigationService
{
    ViewModelBase? Current { get; }

    event EventHandler? Navigated;

    Task NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;

    /// <summary>Forgets the current screen so nothing is left to go back to; called on logout (UIR-23).</summary>
    void ClearHistory();
}
