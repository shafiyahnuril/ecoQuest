using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace EcoQuest.Controls;

public partial class Skeleton : UserControl
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(Skeleton), new PropertyMetadata(new CornerRadius(8)));

    public Skeleton()
    {
        InitializeComponent();
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Purely visual placeholder: no automation peer, so screen readers do not see it (the host announces the loading state).</summary>
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}
