using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EcoQuest.Controls;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// The Evening Review controls draw their own error border on the inner TextBox. WPF's default error adorner (a red rectangle around the
/// whole control) must never appear, so neither the control nor anything inside it may keep a non-null <see cref="Validation.ErrorTemplate"/>.
/// </summary>
[Collection(WpfCollection.Name)]
public class CustomControlsErrorTemplateTests
{
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static List<string> ElementsWithDefaultAdorner(FrameworkElement control, DependencyProperty boundProperty)
    {
        var binding = new Binding { Source = new FrameworkElement(), Path = new PropertyPath(nameof(FrameworkElement.Tag)), Mode = BindingMode.OneWay };
        var expression = BindingOperations.SetBinding(control, boundProperty, binding);
        Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Nilai tidak valid.", null));

        control.Measure(new Size(400, 200));
        control.Arrange(new Rect(0, 0, 400, 200));
        control.UpdateLayout();

        Assert.True(Validation.GetHasError(control));
        return Descendants(control).OfType<FrameworkElement>().Prepend(control)
            .Where(element => Validation.GetHasError(element) && Validation.GetErrorTemplate(element) is not null)
            .Select(element => $"{element.GetType().Name} {element.Name}")
            .ToList();
    }

    [Fact]
    public void NumericStepper_in_error_shows_no_default_adorner()
    {
        var offenders = WpfTestHost.Run(() => ElementsWithDefaultAdorner(new NumericStepper(), NumericStepper.ValueProperty));

        Assert.Empty(offenders);
    }

    [Fact]
    public void TimeRangePicker_in_error_shows_no_default_adorner()
    {
        var offenders = WpfTestHost.Run(() => ElementsWithDefaultAdorner(new TimeRangePicker(), TimeRangePicker.StartTimeProperty));

        Assert.Empty(offenders);
    }

    [Fact]
    public void NumericStepper_in_error_marks_its_inner_box_invalid()
    {
        var invalid = WpfTestHost.Run(() =>
        {
            var stepper = new NumericStepper();
            ElementsWithDefaultAdorner(stepper, NumericStepper.ValueProperty);
            return InputState.GetIsInvalid((TextBox)stepper.FindName("Box"));
        });

        Assert.True(invalid);
    }
}
