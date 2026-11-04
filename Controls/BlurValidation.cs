using System.Windows;
using System.Windows.Input;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only: runs <see cref="CommandProperty"/> with <see cref="ParameterProperty"/> when keyboard focus leaves the element AND all of its
/// children (so moving from a password box to its own show/hide button does not count). Used to validate a field on blur (UIR-21).
/// </summary>
public static class BlurValidation
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(BlurValidation), new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty ParameterProperty = DependencyProperty.RegisterAttached(
        "Parameter", typeof(object), typeof(BlurValidation), new PropertyMetadata(null));

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    public static object? GetParameter(DependencyObject element) => element.GetValue(ParameterProperty);

    public static void SetParameter(DependencyObject element, object? value) => element.SetValue(ParameterProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.IsKeyboardFocusWithinChanged -= OnFocusWithinChanged;
        if (e.NewValue is not null)
        {
            element.IsKeyboardFocusWithinChanged += OnFocusWithinChanged;
        }
    }

    private static void OnFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue || sender is not DependencyObject element)
        {
            return;
        }

        var parameter = GetParameter(element);
        if (GetCommand(element) is { } command && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }
}
