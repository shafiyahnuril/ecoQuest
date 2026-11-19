using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EcoQuest.Navigation;

public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    private (Type Screen, object? Parameter)? _previous;
    private object? _currentParameter;

    public ViewModelBase? Current { get; private set; }

    public bool CanGoBack => _previous is not null;

    public event EventHandler? Navigated;

    public Task NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase =>
        ShowAsync(typeof(TViewModel), parameter, remember: true);

    public async Task GoBackAsync()
    {
        if (_previous is not var (screen, parameter))
        {
            return;
        }

        _previous = null;
        await ShowAsync(screen, parameter, remember: false);
    }

    public void ClearHistory()
    {
        // There is no back stack in the MVP; clearing the current screen guarantees that nothing
        // from the authenticated area can be reached again until the next explicit NavigateTo.
        Current?.OnNavigatedFrom();
        Current = null;
        _previous = null;
    }

    private async Task ShowAsync(Type screen, object? parameter, bool remember)
    {
        if (Current is { } leaving)
        {
            leaving.OnNavigatedFrom();
            // A failing "Coba lagi" must not overwrite the screen it should return to.
            if (remember && leaving is not ConnectionErrorViewModel)
            {
                _previous = (leaving.GetType(), _currentParameter);
            }
        }

        // ViewModels are transient, so every navigation starts with fresh state.
        var viewModel = (ViewModelBase)services.GetRequiredService(screen);
        Current = viewModel;
        _currentParameter = parameter;
        Navigated?.Invoke(this, EventArgs.Empty);

        await viewModel.OnNavigatedToAsync(parameter);
    }
}
