using CommunityToolkit.Mvvm.ComponentModel;

namespace EcoQuest.ViewModels;

/// <summary>State of the single toast shown bottom-right; visible for 3.5 s (UI-UX-Flow-Frontend 2.8).</summary>
public sealed partial class ToastViewModel : ObservableObject
{
    private static readonly TimeSpan VisibleFor = TimeSpan.FromMilliseconds(3500);

    private CancellationTokenSource? _hideAfter;

    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _isVisible;

    /// <summary>Shows <paramref name="message"/>; a newer toast replaces the current one and restarts the timer.</summary>
    public void Show(string message)
    {
        _hideAfter?.Cancel();
        _hideAfter?.Dispose();
        _hideAfter = new CancellationTokenSource();

        Message = message;
        IsVisible = true;
        _ = HideLaterAsync(_hideAfter.Token);
    }

    private async Task HideLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(VisibleFor, cancellationToken);
            IsVisible = false;
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer toast.
        }
    }
}
