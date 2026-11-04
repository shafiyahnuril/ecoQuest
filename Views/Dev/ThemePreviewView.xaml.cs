using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EcoQuest.Controls;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Services.Fakes;
using EcoQuest.ViewModels.Items;

namespace ecoQuest.Views.Dev;

public partial class ThemePreviewView : UserControl
{
    public ThemePreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ActivityItems.ItemsSource = FakeData.ActivityTypes.Select(CreateActivityItem).ToList();
    }

    // Stands in for the Evening Review ViewModel (next task) so Validate/Preview results can be eyeballed.
    private ActivityInputItemViewModel CreateActivityItem(ActivityTypeDto type)
    {
        var item = ActivityInputItemFactory.Create(type);
        item.Changed += (_, _) =>
        {
            var input = item.ToActivityInput();
            var validation = FakeEveningReviewService.ValidateItems([input], FakeData.ActivityTypes);
            item.ApplyIssues(validation.Issues);
            item.ApplyImpact(item.IsFilled && !validation.HasErrors ? FakeImpact.Build(input, type) : null);
        };
        return item;
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
