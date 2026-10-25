using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only bridge for <see cref="PasswordBox"/> (UIR-20): PasswordBox.Password is not a dependency property, so this exposes
/// <c>BoundPassword</c> for two-way binding, <c>IsEmpty</c> for a placeholder, and <c>FocusRequest</c> to focus the box from a ViewModel counter.
/// The helper never logs or stores the value anywhere except the bound property.
/// </summary>
public static class PasswordBoxHelper
{
    // Default is null (not ""), so the first binding of "" counts as a change and attaches the PasswordChanged handler.
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword", typeof(string), typeof(PasswordBoxHelper),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey IsEmptyPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsEmpty", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(true));

    public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;

    /// <summary>Every change of this counter moves keyboard focus to the PasswordBox (used after a failed login).</summary>
    public static readonly DependencyProperty FocusRequestProperty = DependencyProperty.RegisterAttached(
        "FocusRequest", typeof(int), typeof(PasswordBoxHelper), new PropertyMetadata(0, OnFocusRequestChanged));

    public static string? GetBoundPassword(DependencyObject element) => (string?)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string? value) => element.SetValue(BoundPasswordProperty, value);

    public static bool GetIsEmpty(DependencyObject element) => (bool)element.GetValue(IsEmptyProperty);

    public static int GetFocusRequest(DependencyObject element) => (int)element.GetValue(FocusRequestProperty);

    public static void SetFocusRequest(DependencyObject element, int value) => element.SetValue(FocusRequestProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordChanged;
        box.PasswordChanged += OnPasswordChanged;

        if (!(bool)box.GetValue(IsUpdatingProperty))
        {
            var text = e.NewValue as string ?? "";
            if (box.Password != text)
            {
                box.Password = text;
            }
        }

        box.SetValue(IsEmptyPropertyKey, string.IsNullOrEmpty(box.Password));
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(IsUpdatingProperty, true);
        try
        {
            SetBoundPassword(box, box.Password);
        }
        finally
        {
            box.SetValue(IsUpdatingProperty, false);
        }

        box.SetValue(IsEmptyPropertyKey, string.IsNullOrEmpty(box.Password));
    }

    private static void OnFocusRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox box && (int)e.NewValue != 0)
        {
            // Input priority: runs after the command finished, so the box is enabled again when it receives focus.
            box.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                box.Focus();
                Keyboard.Focus(box);
            });
        }
    }
}
