using System.Windows;
using System.Windows.Controls;

namespace EcoQuest.Controls;

/// <summary>Translucent busy layer that blocks pointer input over the content below it. The text is optional and supplied by the caller.</summary>
public partial class BusyOverlay : UserControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(BusyOverlay));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(BusyOverlay));

    public BusyOverlay()
    {
        InitializeComponent();
    }

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
