using System.Windows;

namespace EcoQuest.Controls;

/// <summary>
/// Lets a composite control draw the error border on its inner TextBox: inner boxes are not binding targets, so their own
/// <c>Validation.HasError</c> never turns true.
/// </summary>
public static class InputState
{
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.RegisterAttached(
        "IsInvalid", typeof(bool), typeof(InputState), new PropertyMetadata(false));

    public static bool GetIsInvalid(DependencyObject element) => (bool)element.GetValue(IsInvalidProperty);

    public static void SetIsInvalid(DependencyObject element, bool value) => element.SetValue(IsInvalidProperty, value);
}
