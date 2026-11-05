using System.Windows;
using System.Windows.Media;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only helper for CSS "position: sticky; bottom: N": keeps the element N px above the bottom of the enclosing ScrollViewer
/// while its natural position (the end of the content) is still below the viewport. The element must sit alone in a placeholder panel
/// that reserves its natural space.
/// </summary>
public static class StickyBottom
{
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached(
        "Offset", typeof(double), typeof(StickyBottom), new PropertyMetadata(double.NaN, OnOffsetChanged));

    private static readonly DependencyProperty TrackerProperty = DependencyProperty.RegisterAttached(
        "Tracker", typeof(Tracker), typeof(StickyBottom));

    public static double GetOffset(DependencyObject element) => (double)element.GetValue(OffsetProperty);

    public static void SetOffset(DependencyObject element, double value) => element.SetValue(OffsetProperty, value);

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && d.GetValue(TrackerProperty) is null)
        {
            d.SetValue(TrackerProperty, new Tracker(element));
        }
    }

    private sealed class Tracker : ScrollViewerTracker
    {
        private readonly TranslateTransform _shift = new();

        public Tracker(FrameworkElement footer)
            : base(footer)
        {
            footer.RenderTransform = _shift;
        }

        protected override void Update()
        {
            if (!CanTrack())
            {
                return;
            }

            var slotBottom = SlotTopInContent() + Slot!.ActualHeight;
            var visibleBottom = Viewer!.VerticalOffset + Viewer.ViewportHeight - GetOffset(Element);
            _shift.Y = Math.Min(0, visibleBottom - slotBottom);
        }

        protected override void Reset() => _shift.Y = 0;
    }
}
