using CommunityToolkit.Mvvm.ComponentModel;

namespace EcoQuest.ViewModels;

/// <summary>Loading lifecycle shared by every data screen (Loading / Empty / Error / Normal).</summary>
public enum LoadState { Idle, Loading, Loaded, Error }

/// <summary>
/// Base class of all ViewModels (SDD-Frontend 2.1). Deliberately free of System.Windows.* (UIR-07).
/// </summary>
public abstract partial class ViewModelBase : ObservableValidator
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private LoadState _state = LoadState.Idle;

    /// <summary>Called by NavigationService after this ViewModel became the current one.</summary>
    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    /// <summary>Called by NavigationService right before another ViewModel replaces this one.</summary>
    public virtual void OnNavigatedFrom() { }

    // TODO(FE-03+): add the RunAsync<T>(Func<CancellationToken, Task<Result<T>>>) helper of SDD-Frontend 2.1
    // once Result/Result<T> exist in Core/Contracts.
}
