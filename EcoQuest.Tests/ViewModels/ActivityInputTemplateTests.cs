using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using EcoQuest.Controls;
using EcoQuest.Selectors;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels.Items;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class ActivityInputTemplateTests
{
    private static ActivityInputItemViewModel Item(int id) =>
        ActivityInputItemFactory.Create(FakeData.ActivityTypes.Single(t => t.ActivityTypeId == id));

    private static ContentControl Render(ActivityInputItemViewModel item)
    {
        var templates = new ResourceDictionary { Source = new Uri("pack://application:,,,/EcoQuest;component/Views/Templates/ActivityInputTemplates.xaml") };
        var host = new ContentControl
        {
            Content = item,
            ContentTemplateSelector = (ActivityInputTemplateSelector)templates["ActivityInput.Selector"],
        };
        host.Measure(new Size(600, 300));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();
        return host;
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match || (match = Find<T>(child)!) is not null)
            {
                return match;
            }
        }

        return null;
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    public void Quantity_activities_get_a_stepper_bound_both_ways(int id, bool isInteger)
    {
        WpfTestHost.Run(() =>
        {
            var item = (QuantityItemViewModel)Item(id);
            var host = Render(item);
            var stepper = Find<NumericStepper>(host)!;

            Assert.NotNull(stepper);
            Assert.Equal(isInteger, stepper.IsInteger);

            item.Quantity = 2m;
            Assert.Equal(2m, stepper.Value);

            stepper.Value = 3m;
            Assert.Equal(3m, item.Quantity);
            return 0;
        });
    }

    [Fact]
    public void Quantity_templates_show_the_unit_beside_the_stepper()
    {
        WpfTestHost.Run(() =>
        {
            var texts = new[] { 1, 4 }.Select(id => Find<TextBlock>(Render(Item(id)))!.Text).ToArray();

            Assert.Equal(["km", "kali"], texts);
            return 0;
        });
    }

    [Fact]
    public void Time_range_activity_gets_a_picker_bound_both_ways()
    {
        WpfTestHost.Run(() =>
        {
            var item = (TimeRangeItemViewModel)Item(3);
            var picker = Find<TimeRangePicker>(Render(item))!;

            Assert.NotNull(picker);

            item.StartTime = new TimeOnly(21, 0);
            Assert.Equal(new TimeOnly(21, 0), picker.StartTime);

            picker.EndTime = new TimeOnly(0, 30);
            Assert.Equal(new TimeOnly(0, 30), item.EndTime);
            return 0;
        });
    }

    [Fact]
    public void Toggle_activity_gets_a_switch_bound_both_ways()
    {
        WpfTestHost.Run(() =>
        {
            var item = (ToggleItemViewModel)Item(5);
            var toggle = Find<ToggleButton>(Render(item))!;

            Assert.NotNull(toggle);
            Assert.NotEqual(true, toggle.IsChecked);

            item.IsYes = true;
            Assert.True(toggle.IsChecked);

            toggle.IsChecked = false;
            Assert.False(item.IsYes);
            return 0;
        });
    }

    [Fact]
    public void Selector_returns_no_template_for_other_content()
    {
        WpfTestHost.Run(() =>
        {
            var selector = new ActivityInputTemplateSelector { NumericTemplate = new DataTemplate() };

            Assert.Null(selector.SelectTemplate("text", new ContentControl()));
            return 0;
        });
    }
}
