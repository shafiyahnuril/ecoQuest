using System.Windows;
using System.Windows.Controls;

namespace EcoQuest.Controls;

/// <summary>Visual panel shared by the Login and Register pages: only the heading and body text differ. No logic.</summary>
public partial class AuthVisualPanel : UserControl
{
    public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(
        nameof(Heading), typeof(string), typeof(AuthVisualPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(string), typeof(AuthVisualPanel), new PropertyMetadata(""));

    public AuthVisualPanel()
    {
        InitializeComponent();
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
}
