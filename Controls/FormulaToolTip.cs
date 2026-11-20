using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace EcoQuest.Controls;

/// <summary>
/// Shows a service-provided explanation as the shared tooltip and as the automation help text (FR-4.5). Nothing is attached while the text is blank.
/// Focusable hosts also open it on keyboard focus; it closes on Esc, on lost focus and on unload.
/// </summary>
public static class FormulaToolTip
{
    private const int MouseShowDelayMs = 300;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(FormulaToolTip), new PropertyMetadata(null, OnTextChanged));

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static bool IsOpen(FrameworkElement element) => element.ToolTip is ToolTip { IsOpen: true };

    public static void Show(FrameworkElement element)
    {
        if (element.ToolTip is ToolTip tip)
        {
            tip.PlacementTarget = element;
            tip.Placement = PlacementMode.Bottom;
            tip.IsOpen = true;
        }
    }

    public static void Hide(FrameworkElement element)
    {
        if (element.ToolTip is ToolTip tip)
        {
            tip.IsOpen = false;
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.GotKeyboardFocus -= OnGotKeyboardFocus;
        element.LostKeyboardFocus -= OnLostKeyboardFocus;
        element.PreviewKeyDown -= OnPreviewKeyDown;
        element.Unloaded -= OnUnloaded;

        if (string.IsNullOrWhiteSpace(e.NewValue as string))
        {
            Hide(element);
            element.ClearValue(FrameworkElement.ToolTipProperty);
            element.ClearValue(AutomationProperties.HelpTextProperty);
            return;
        }

        var text = (string)e.NewValue;
        var tip = element.ToolTip as ToolTip ?? CreateToolTip(element);
        tip.Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        element.ToolTip = tip;
        ToolTipService.SetInitialShowDelay(element, MouseShowDelayMs);
        AutomationProperties.SetHelpText(element, text);

        element.GotKeyboardFocus += OnGotKeyboardFocus;
        element.LostKeyboardFocus += OnLostKeyboardFocus;
        element.PreviewKeyDown += OnPreviewKeyDown;
        element.Unloaded += OnUnloaded;
    }

    private static ToolTip CreateToolTip(FrameworkElement element) =>
        new() { Style = (Style)element.FindResource("ToolTip.Default") };

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Mouse clicks also focus buttons; the hover tooltip already covers that case.
        if (sender is FrameworkElement element && InputManager.Current.MostRecentInputDevice is KeyboardDevice)
        {
            Show(element);
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Hide((FrameworkElement)sender);

    private static void OnUnloaded(object sender, RoutedEventArgs e) => Hide((FrameworkElement)sender);

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (e.Key == Key.Escape && IsOpen(element))
        {
            Hide(element);
            e.Handled = true;
        }
    }
}
