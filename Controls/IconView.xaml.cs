using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoQuest.Controls;

public partial class IconView : UserControl
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(IconView), new PropertyMetadata(null));

    public IconView()
    {
        InitializeComponent();
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
