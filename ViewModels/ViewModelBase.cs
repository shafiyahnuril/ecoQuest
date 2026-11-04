using CommunityToolkit.Mvvm.ComponentModel;
using EcoQuest.Core.Common;

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

    /// <summary>
    /// Runs a service call and keeps State / ErrorMessage / IsBusy consistent (SDD-Frontend 2.1). Returns the value on success,
    /// otherwise default. It does NOT touch IsEmpty: the calling ViewModel decides that after it received the data.
    /// <list type="bullet">
    /// <item>Re-entrancy: if IsBusy is already true the call returns default without running <paramref name="op"/>.</item>
    /// <item>Failed Result: State = Error and ErrorMessage = Result.Message (no try/catch for business errors).</item>
    /// <item>Cancelled via <paramref name="ct"/>: not an error; State and ErrorMessage go back to what they were.</item>
    /// <item>Unexpected exception: not caught (architecture: it is a bug for DispatcherUnhandledException), but IsBusy is reset and
    /// State/ErrorMessage go back to their previous values instead of staying on Loading.</item>
    /// <item><paramref name="silent"/>: when true and State is already Loaded, the old content stays visible (State is not set to Loading).</item>
    /// </list>
    /// </summary>
    protected async Task<T?> RunAsync<T>(Func<CancellationToken, Task<Result<T>>> op, CancellationToken ct = default, bool silent = false)
    {
        var result = await ExecuteAsync(op, ct, silent);
        return result is { IsSuccess: true } ? result.Value : default;
    }

    /// <summary>Same as <see cref="RunAsync{T}"/> for operations without a value. Returns true when the operation succeeded.</summary>
    protected async Task<bool> RunAsync(Func<CancellationToken, Task<Result>> op, CancellationToken ct = default, bool silent = false)
    {
        var result = await ExecuteAsync(op, ct, silent);
        return result is { IsSuccess: true };
    }

    private async Task<TResult?> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> op, CancellationToken ct, bool silent)
        where TResult : Result
    {
        if (IsBusy)
        {
            return null;
        }

        var previousState = State;
        var previousError = ErrorMessage;
        IsBusy = true;
        ErrorMessage = null;
        if (!(silent && previousState == LoadState.Loaded))
        {
            State = LoadState.Loading;
        }

        var completed = false;
        try
        {
            var result = await op(ct);
            completed = true;
            if (result.IsSuccess)
            {
                State = LoadState.Loaded;
            }
            else
            {
                ErrorMessage = result.Message;
                State = LoadState.Error;
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (!completed)
            {
                State = previousState;
                ErrorMessage = previousError;
            }

            IsBusy = false;
        }
    }
}
