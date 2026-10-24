using System.Windows;

namespace EcoQuest.Controls;

/// <summary>Visual-only helper: lets a ViewModel close a modal Window by binding its result (no code-behind in the dialog).</summary>
public static class DialogHelper
{
    public static readonly DependencyProperty DialogResultProperty = DependencyProperty.RegisterAttached(
        "DialogResult", typeof(bool?), typeof(DialogHelper), new PropertyMetadata(null, OnDialogResultChanged));

    public static bool? GetDialogResult(DependencyObject element) => (bool?)element.GetValue(DialogResultProperty);

    public static void SetDialogResult(DependencyObject element, bool? value) => element.SetValue(DialogResultProperty, value);

    private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window { } window && e.NewValue is bool result)
        {
            window.DialogResult = result;
        }
    }
}
