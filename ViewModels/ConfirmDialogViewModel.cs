using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>Content and result of <c>Views/Dialogs/ConfirmDialog</c>. <see cref="Result"/> closes the window (DialogHelper).</summary>
public sealed partial class ConfirmDialogViewModel(
    string title, string message, string confirmText, string? cancelText, DialogIcon icon = DialogIcon.Info)
    : ObservableObject
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public string ConfirmText { get; } = confirmText;

    /// <summary>Null for information dialogs, which only have the confirm button.</summary>
    public string? CancelText { get; } = cancelText;

    public bool HasCancel => CancelText is not null;

    public string IconKey { get; } = icon switch
    {
        DialogIcon.Warning => "Icon.Alert",
        DialogIcon.Leaf => "Icon.Leaf",
        DialogIcon.Level => "Icon.Trophy",
        DialogIcon.Badge => "Icon.Award",
        _ => "Icon.Info",
    };

    /// <summary>Milestone dialogs (D-04) pop their icon once; other dialogs stay still.</summary>
    public bool AnimatesIcon { get; } = icon is DialogIcon.Level or DialogIcon.Badge;

    /// <summary>Null while open; true = confirmed, false = cancelled (button, Esc or click outside the card).</summary>
    [ObservableProperty] private bool? _result;

    [RelayCommand]
    private void Confirm() => Result = true;

    [RelayCommand]
    private void Cancel() => Result = false;
}
