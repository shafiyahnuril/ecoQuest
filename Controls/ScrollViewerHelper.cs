using System.Windows;
using System.Windows.Controls;

namespace EcoQuest.Controls;

/// <summary>Visual-only helper: scrolls a ScrollViewer to the top whenever the bound value changes (page change, UI-UX-Flow-Frontend 2.8).</summary>
public static class ScrollViewerHelper
{
    public static readonly DependencyProperty ScrollToTopOnChangeProperty = DependencyProperty.RegisterAttached(
        "ScrollToTopOnChange", typeof(object), typeof(ScrollViewerHelper), new PropertyMetadata(null, OnChanged));

    public static object? GetScrollToTopOnChange(DependencyObject element) => element.GetValue(ScrollToTopOnChangeProperty);

    public static void SetScrollToTopOnChange(DependencyObject element, object? value) => element.SetValue(ScrollToTopOnChangeProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer viewer)
        {
            viewer.ScrollToTop();
        }
    }
}
