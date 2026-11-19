using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only helper: caps the height of an element so that it ends at the bottom of the enclosing ScrollViewer's viewport (minus the page's
/// bottom padding). A long list then scrolls inside itself, which is what lets its panel virtualize; the element's own MinHeight is the floor.
/// Until the viewport is known the element is capped to the work area, so a long list never realizes every item on its first layout pass.
/// </summary>
public static class ViewportFill
{
    public static readonly DependencyProperty PinProperty = DependencyProperty.RegisterAttached(
        "Pin", typeof(bool), typeof(ViewportFill), new PropertyMetadata(false, OnPinChanged));

    private static readonly DependencyProperty TrackerProperty = DependencyProperty.RegisterAttached(
        "Tracker", typeof(Tracker), typeof(ViewportFill));

    public static bool GetPin(DependencyObject element) => (bool)element.GetValue(PinProperty);

    public static void SetPin(DependencyObject element, bool value) => element.SetValue(PinProperty, value);

    private static void OnPinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && (bool)e.NewValue && d.GetValue(TrackerProperty) is null)
        {
            d.SetValue(TrackerProperty, new Tracker(element));
        }
    }

    private sealed class Tracker
    {
        private readonly FrameworkElement _element;
        private ScrollViewer? _viewer;

        public Tracker(FrameworkElement element)
        {
            _element = element;
            element.MaxHeight = SystemParameters.WorkArea.Height;
            element.Loaded += (_, _) => Attach();
            element.Unloaded += (_, _) => Detach();
            if (element.IsLoaded)
            {
                Attach();
            }
        }

        private void Attach()
        {
            _viewer = FindViewer();
            _element.LayoutUpdated += OnLayoutUpdated;
            Update();
        }

        private void Detach()
        {
            _element.LayoutUpdated -= OnLayoutUpdated;
            _viewer = null;
            _element.ClearValue(FrameworkElement.MaxHeightProperty);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e) => Update();

        private void Update()
        {
            if (_viewer?.Content is not Visual content || !_element.IsDescendantOf(content))
            {
                return;
            }

            var top = _element.TransformToAncestor(content).Transform(new Point()).Y;
            var bottomPadding = (_viewer.Content as Border)?.Padding.Bottom ?? 0;
            var height = Math.Max(_viewer.ViewportHeight - top - bottomPadding, _element.MinHeight);
            if (Math.Abs(_element.MaxHeight - height) > 0.5)
            {
                _element.MaxHeight = height;
            }
        }

        private ScrollViewer? FindViewer()
        {
            for (var parent = VisualTreeHelper.GetParent(_element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ScrollViewer viewer)
                {
                    return viewer;
                }
            }

            return null;
        }
    }
}
