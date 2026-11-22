using System.Windows;
using System.Windows.Controls;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only helper: sizes and places an element that sits in a Canvas over the area of that Canvas which the enclosing
/// ScrollViewer currently shows, so a busy overlay covers what the user sees instead of the whole (taller) page.
/// The Canvas must fill the content it overlays and does not clip, so the overlay may reach into the page padding; without a
/// ScrollViewer the element covers the whole Canvas.
/// </summary>
public static class ViewportOverlay
{
    public static readonly DependencyProperty PinProperty = DependencyProperty.RegisterAttached(
        "Pin", typeof(bool), typeof(ViewportOverlay), new PropertyMetadata(false, OnPinChanged));

    private static readonly DependencyProperty TrackerProperty = DependencyProperty.RegisterAttached(
        "Tracker", typeof(Tracker), typeof(ViewportOverlay));

    public static bool GetPin(DependencyObject element) => (bool)element.GetValue(PinProperty);

    public static void SetPin(DependencyObject element, bool value) => element.SetValue(PinProperty, value);

    private static void OnPinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && (bool)e.NewValue && d.GetValue(TrackerProperty) is null)
        {
            d.SetValue(TrackerProperty, new Tracker(element));
        }
    }

    private sealed class Tracker(FrameworkElement overlay) : ScrollViewerTracker(overlay)
    {
        protected override void Update()
        {
            if (Slot is not { } slot)
            {
                return;
            }

            var height = slot.ActualHeight;
            var top = 0d;
            if (CanTrack())
            {
                height = Viewer!.ViewportHeight;
                top = Viewer.VerticalOffset - SlotTopInContent();
            }

            Canvas.SetTop(Element, top);
            Element.Width = slot.ActualWidth;
            Element.Height = height;
        }
    }
}
