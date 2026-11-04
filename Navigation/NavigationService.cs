using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EcoQuest.Navigation;

public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    public ViewModelBase? Current { get; private set; }

    public event EventHandler? Navigated;

    public async Task NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
    {
        Current?.OnNavigatedFrom();

        // ViewModels are transient, so every navigation starts with fresh state.
        var viewModel = services.GetRequiredService<TViewModel>();
        Current = viewModel;
        Navigated?.Invoke(this, EventArgs.Empty);

        await viewModel.OnNavigatedToAsync(parameter);
    }

    public void ClearHistory()
    {
        // There is no back stack in the MVP; clearing the current screen guarantees that nothing
        // from the authenticated area can be reached again until the next explicit NavigateTo.
        Current?.OnNavigatedFrom();
        Current = null;
    }
}
