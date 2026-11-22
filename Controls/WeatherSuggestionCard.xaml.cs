using System.Windows;
using System.Windows.Controls;

namespace EcoQuest.Controls;

public partial class WeatherSuggestionCard : UserControl
{
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(WeatherSuggestionCard), new PropertyMetadata(false));

    public WeatherSuggestionCard()
    {
        InitializeComponent();
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }
}
