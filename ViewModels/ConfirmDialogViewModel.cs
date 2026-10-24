using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EcoQuest.ViewModels;

/// <summary>Content and result of <c>Views/Dialogs/ConfirmDialog</c>. <see cref="Result"/> closes the window (DialogHelper).</summary>
public sealed partial class ConfirmDialogViewModel(string title, string message, string confirmText, string? cancelText)
    : ObservableObject
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string ConfirmText { get; } = confirmText;

    /// <summary>Null for information dialogs, which only have the confirm button.</summary>
    public string? CancelText { get; } = cancelText;

    public bool HasCancel => CancelText is not null;

    /// <summary>Null while open; true = confirmed, false = cancelled (button, Esc or click outside the card).</summary>
    [ObservableProperty] private bool? _result;

    [RelayCommand]
    private void Confirm() => Result = true;

    [RelayCommand]
    private void Cancel() => Result = false;
}
