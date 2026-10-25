using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only password column with a show/hide toggle (D-16). The value lives only in <see cref="Password"/> (bound to the
/// ViewModel); this control keeps no copy, has no ToString override and logs nothing. Rules are in <see cref="PasswordRevealState"/>.
/// </summary>
public partial class PasswordInput : UserControl
{
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password), typeof(string), typeof(PasswordInput),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(PasswordInput), new PropertyMetadata(""));

    public static readonly DependencyProperty IsRevealedProperty = DependencyProperty.Register(
        nameof(IsRevealed), typeof(bool), typeof(PasswordInput),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsRevealedChanged));

    /// <summary>While true the password is hidden again (bind to IsBusy: hides on submit).</summary>
    public static readonly DependencyProperty ConcealWhenProperty = DependencyProperty.Register(
        nameof(ConcealWhen), typeof(bool), typeof(PasswordInput), new PropertyMetadata(false, OnConcealWhenChanged));

    /// <summary>Every change of this counter focuses the visible input (used after a failed login).</summary>
    public static readonly DependencyProperty FocusRequestProperty = DependencyProperty.Register(
        nameof(FocusRequest), typeof(int), typeof(PasswordInput), new PropertyMetadata(0, OnFocusRequestChanged));

    private readonly PasswordRevealState _state = new();
    private bool _userEditing;

    public PasswordInput()
    {
        InitializeComponent();

        // Typing marks the following Password change as "by the user" (see PasswordRevealState.OnPasswordChanged).
        HiddenBox.PreviewTextInput += (_, _) => _userEditing = true;
        HiddenBox.PreviewKeyDown += (_, _) => _userEditing = true;
        ShownBox.PreviewTextInput += (_, _) => _userEditing = true;
        ShownBox.PreviewKeyDown += (_, _) => _userEditing = true;
        Unloaded += (_, _) => Conceal();
    }

    public string? Password
    {
        get => (string?)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public bool ConcealWhen
    {
        get => (bool)GetValue(ConcealWhenProperty);
        set => SetValue(ConcealWhenProperty, value);
    }

    public int FocusRequest
    {
        get => (int)GetValue(FocusRequestProperty);
        set => SetValue(FocusRequestProperty, value);
    }

    private void Conceal()
    {
        _state.Conceal();
        IsRevealed = _state.IsRevealed;
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (PasswordInput)d;
        input._state.OnPasswordChanged(e.NewValue as string, input._userEditing);
        input._userEditing = false;
        if (input.IsRevealed != input._state.IsRevealed)
        {
            input.IsRevealed = input._state.IsRevealed;
        }
    }

    private static void OnConcealWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (PasswordInput)d;
        input._state.OnBusyChanged((bool)e.NewValue);
        if (input.IsRevealed != input._state.IsRevealed)
        {
            input.IsRevealed = input._state.IsRevealed;
        }
    }

    private static void OnIsRevealedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (PasswordInput)d;
        var revealed = (bool)e.NewValue;
        input._state.Set(revealed);

        // Keep focus in the column after the swap (only when the user was working in it). Caret ends up at the end of the text.
        if (input.IsKeyboardFocusWithin)
        {
            input.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => input.FocusVisibleBox(revealed));
        }
    }

    private static void OnFocusRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (PasswordInput)d;
        if ((int)e.NewValue != 0)
        {
            // Input priority: after the command finished, so the control is enabled again and already concealed.
            input.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => input.FocusVisibleBox(input.IsRevealed));
        }
    }

    private void FocusVisibleBox(bool revealed)
    {
        if (revealed)
        {
            ShownBox.Focus();
            Keyboard.Focus(ShownBox);
            ShownBox.CaretIndex = ShownBox.Text.Length;
        }
        else
        {
            HiddenBox.Focus();
            Keyboard.Focus(HiddenBox);
            // PasswordBox has no public caret API, so the caret is at the end of the text after switching back.
        }
    }

}
