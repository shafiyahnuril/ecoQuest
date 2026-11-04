namespace EcoQuest.Controls;

/// <summary>
/// Rules of the show/hide password toggle (D-16), kept free of WPF types so they can be unit-tested.
/// The password text itself is never stored here.
/// </summary>
public sealed class PasswordRevealState
{
    /// <summary>True while the password is shown as plain text.</summary>
    public bool IsRevealed { get; private set; }

    public void Set(bool revealed) => IsRevealed = revealed;

    public void Toggle() => IsRevealed = !IsRevealed;

    /// <summary>Back to hidden. Called after submit, after a failed login and when the screen is left.</summary>
    public void Conceal() => IsRevealed = false;

    /// <summary>
    /// The password changed. Typing (even deleting everything) keeps the current mode; a change that did not come from the
    /// user and leaves the field empty (the ViewModel clearing it) hides the password again.
    /// </summary>
    public void OnPasswordChanged(string? value, bool changedByUser)
    {
        if (!changedByUser && string.IsNullOrEmpty(value))
        {
            Conceal();
        }
    }

    /// <summary>A request started (submit): hide the password.</summary>
    public void OnBusyChanged(bool isBusy)
    {
        if (isBusy)
        {
            Conceal();
        }
    }
}
