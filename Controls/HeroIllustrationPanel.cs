using System.Windows;
using System.Windows.Controls;

namespace EcoQuest.Controls;

/// <summary>
/// Places its child like the prototype's absolutely positioned <c>.hero-img</c>, as fractions of the panel's own size (no named bindings,
/// so it also works when the view is created from a DataTemplate): width = min(WidthFraction x W, MaxChildWidth), height = HeightFraction x H,
/// right edge RightFraction x W inside the right edge, top TopFraction x H. Fractions may be negative (the hero clips what sticks out).
/// The panel asks for no space of its own, so it never changes the size of the hero.
/// </summary>
public class HeroIllustrationPanel : Panel
{
    public static readonly DependencyProperty WidthFractionProperty = Register(nameof(WidthFraction), 0.52);
    public static readonly DependencyProperty HeightFractionProperty = Register(nameof(HeightFraction), 1.16);
    public static readonly DependencyProperty RightFractionProperty = Register(nameof(RightFraction), 0.01);
    public static readonly DependencyProperty TopFractionProperty = Register(nameof(TopFraction), -0.07);

    public static readonly DependencyProperty MaxChildWidthProperty = DependencyProperty.Register(
        nameof(MaxChildWidth), typeof(double), typeof(HeroIllustrationPanel),
        new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsArrange));

    public double WidthFraction { get => (double)GetValue(WidthFractionProperty); set => SetValue(WidthFractionProperty, value); }

    public double HeightFraction { get => (double)GetValue(HeightFractionProperty); set => SetValue(HeightFractionProperty, value); }

    public double RightFraction { get => (double)GetValue(RightFractionProperty); set => SetValue(RightFractionProperty, value); }

    public double TopFraction { get => (double)GetValue(TopFractionProperty); set => SetValue(TopFractionProperty, value); }

    public double MaxChildWidth { get => (double)GetValue(MaxChildWidthProperty); set => SetValue(MaxChildWidthProperty, value); }

    private static DependencyProperty Register(string name, double defaultValue) =>
        DependencyProperty.Register(name, typeof(double), typeof(HeroIllustrationPanel),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsArrange));

    protected override Size MeasureOverride(Size availableSize) => new(0, 0);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(finalSize.Width * WidthFraction, MaxChildWidth);
        var height = finalSize.Height * HeightFraction;
        var left = finalSize.Width - finalSize.Width * RightFraction - width;
        var top = finalSize.Height * TopFraction;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(Math.Max(0, width), Math.Max(0, height)));
            child.Arrange(new Rect(left, top, Math.Max(0, width), Math.Max(0, height)));
        }

        return finalSize;
    }
}
