using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoQuest.Controls;

/// <summary>Presentation-only card of the dashboard metric row: icon bubble, value and label. No logic; every text is bound by the caller.</summary>
public partial class MetricCard : UserControl
{
    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(MetricCard));

    public static readonly DependencyProperty BubbleBackgroundProperty =
        DependencyProperty.Register(nameof(BubbleBackground), typeof(Brush), typeof(MetricCard));

    public static readonly DependencyProperty IconForegroundProperty =
        DependencyProperty.Register(nameof(IconForeground), typeof(Brush), typeof(MetricCard));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(MetricCard));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricCard));

    public static readonly DependencyProperty ExplanationProperty =
        DependencyProperty.Register(nameof(Explanation), typeof(string), typeof(MetricCard));

    public MetricCard()
    {
        InitializeComponent();
    }

    public Geometry? IconData { get => (Geometry?)GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }

    public Brush? BubbleBackground { get => (Brush?)GetValue(BubbleBackgroundProperty); set => SetValue(BubbleBackgroundProperty, value); }

    public Brush? IconForeground { get => (Brush?)GetValue(IconForegroundProperty); set => SetValue(IconForegroundProperty, value); }

    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Optional tooltip text (service-generated; UIR-50). Empty means no tooltip.</summary>
    public string? Explanation { get => (string?)GetValue(ExplanationProperty); set => SetValue(ExplanationProperty, value); }
}
