using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EcoQuest.Controls;

namespace ecoQuest.Views.Dev;

public partial class ThemePreviewView : UserControl
{
    public ThemePreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Visual-only: forces the Validation.HasError state so the error borders can be inspected.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MarkInvalid(TxtError, TagProperty, "Username sudah digunakan.");
        MarkInvalid(StepperError, NumericStepper.ValueProperty, "Jarak melebihi batas wajar per hari.");
        MarkInvalid(TimeError, TimeRangePicker.StartTimeProperty, "Waktu selesai harus berbeda dari waktu mulai.");

        TimeFilled.StartTime = new TimeOnly(21, 0);
        TimeFilled.EndTime = new TimeOnly(0, 30);
        TimeDisabled.StartTime = new TimeOnly(21, 0);
        TimeDisabled.EndTime = new TimeOnly(23, 30);
    }

    private void MarkInvalid(FrameworkElement target, DependencyProperty property, string message)
    {
        var binding = new Binding { Source = this, Path = new PropertyPath(nameof(Tag)), Mode = BindingMode.OneWay };
        var expression = BindingOperations.SetBinding(target, property, binding);
        Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, message, null));
    }
}
