using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoQuest.Controls;

/// <summary>Base of the visual-only helpers that follow the visible part of the enclosing ScrollViewer while an element is loaded.</summary>
internal abstract class ScrollViewerTracker
{
    private FrameworkElement? _slot;

    protected ScrollViewerTracker(FrameworkElement element)
    {
        Element = element;
        element.Loaded += (_, _) => Attach();
        element.Unloaded += (_, _) => Detach();
        element.SizeChanged += (_, _) => Update();
        if (element.IsLoaded)
        {
            Attach();
        }
    }

    protected FrameworkElement Element { get; }

    protected ScrollViewer? Viewer { get; private set; }

    /// <summary>The panel the element sits in; its natural space is what the element follows.</summary>
    protected FrameworkElement? Slot => Element.Parent as FrameworkElement;

    protected abstract void Update();

    protected virtual void Reset()
    {
    }

    protected double SlotTopInContent()
    {
        var content = (Visual)Viewer!.Content;
        return Slot!.TransformToAncestor(content).Transform(new Point()).Y;
    }

    protected bool CanTrack() => Viewer?.Content is Visual content && Slot is { } slot && slot.IsDescendantOf(content);

    private void Attach()
    {
        Viewer = FindViewer();
        _slot = Slot;
        if (_slot is not null)
        {
            _slot.SizeChanged += OnSlotSizeChanged;
        }

        if (Viewer is not null)
        {
            Viewer.ScrollChanged += OnScrollChanged;
        }

        Update();
    }

    private void Detach()
    {
        if (Viewer is not null)
        {
            Viewer.ScrollChanged -= OnScrollChanged;
            Viewer = null;
        }

        if (_slot is not null)
        {
            _slot.SizeChanged -= OnSlotSizeChanged;
            _slot = null;
        }

        Reset();
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Update();

    private void OnSlotSizeChanged(object sender, SizeChangedEventArgs e) => Update();

    private ScrollViewer? FindViewer()
    {
        for (var parent = VisualTreeHelper.GetParent(Element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        return null;
    }
}
