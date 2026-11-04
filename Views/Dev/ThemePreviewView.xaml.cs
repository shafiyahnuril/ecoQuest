using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ecoQuest.Views.Dev;

public partial class ThemePreviewView : UserControl
{
    public ThemePreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Visual-only: forces the Validation.HasError state so the error border of TextBox.Default can be inspected.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var binding = new Binding { Source = this, Path = new PropertyPath(nameof(Tag)), Mode = BindingMode.OneWay };
        var expression = BindingOperations.SetBinding(TxtError, TagProperty, binding);
        Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Username sudah digunakan.", null));
    }
}
