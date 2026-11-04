using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using EcoQuest.Controls;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>Behaviour and measured size (DIP) of the Evening Review controls on the shared WPF host. Visual fidelity is NOT checked here.</summary>
[Collection(WpfCollection.Name)]
public class CustomControlsTests
{
    private sealed class Holder : ObservableObject
    {
        private decimal? _amount;
        private TimeOnly? _start;
        private TimeOnly? _end;

        public decimal? Amount { get => _amount; set => SetProperty(ref _amount, value); }

        public TimeOnly? Start { get => _start; set => SetProperty(ref _start, value); }

        public TimeOnly? End { get => _end; set => SetProperty(ref _end, value); }
    }

    private static T Part<T>(FrameworkElement root, string name) where T : class => (T)root.FindName(name);

    private static void Type(TextBox box, string text) => box.Text = text;

    private static void LoseFocus(TextBox box, FrameworkElement owner) =>
        box.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, box, owner) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(600, 300));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
    }

    private static NumericStepper BoundStepper(Holder holder, bool isInteger = false)
    {
        var stepper = new NumericStepper { IsInteger = isInteger };
        BindingOperations.SetBinding(stepper, NumericStepper.ValueProperty, new Binding(nameof(Holder.Amount)) { Source = holder, Mode = BindingMode.TwoWay });
        return stepper;
    }

    private static TimeRangePicker BoundPicker(Holder holder)
    {
        var picker = new TimeRangePicker();
        BindingOperations.SetBinding(picker, TimeRangePicker.StartTimeProperty, new Binding(nameof(Holder.Start)) { Source = holder, Mode = BindingMode.TwoWay });
        BindingOperations.SetBinding(picker, TimeRangePicker.EndTimeProperty, new Binding(nameof(Holder.End)) { Source = holder, Mode = BindingMode.TwoWay });
        return picker;
    }

    [Fact]
    public void Stepper_value_follows_valid_typing_without_rewriting_the_text()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder();
            var stepper = BoundStepper(holder);
            var box = Part<TextBox>(stepper, "Box");

            Type(box, "3,456");

            Assert.Equal(3.46m, holder.Amount);
            Assert.Equal("3,456", box.Text);
            return 0;
        });
    }

    [Fact]
    public void Stepper_reformats_the_text_on_focus_loss()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder();
            var stepper = BoundStepper(holder);
            var box = Part<TextBox>(stepper, "Box");

            Type(box, "3.5");
            LoseFocus(box, stepper);

            Assert.Equal(3.5m, holder.Amount);
            Assert.Equal("3,50", box.Text);
            return 0;
        });
    }

    [Fact]
    public void Stepper_restores_the_last_valid_value_when_the_text_is_invalid()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder { Amount = 3m };
            var stepper = BoundStepper(holder);
            var box = Part<TextBox>(stepper, "Box");

            Type(box, "abc");
            Assert.Equal(3m, holder.Amount);

            LoseFocus(box, stepper);
            Assert.Equal("3,00", box.Text);
            return 0;
        });
    }

    [Fact]
    public void Stepper_empty_text_is_null_and_negative_text_is_passed_on()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder { Amount = 2m };
            var stepper = BoundStepper(holder);
            var box = Part<TextBox>(stepper, "Box");

            Type(box, "");
            Assert.Null(holder.Amount);

            Type(box, "-1");
            Assert.Equal(-1m, holder.Amount);
            return 0;
        });
    }

    [Fact]
    public void Stepper_shows_a_source_change_made_outside_the_control()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder();
            var stepper = BoundStepper(holder);
            var box = Part<TextBox>(stepper, "Box");

            holder.Amount = 2m;

            Assert.Equal("2,00", box.Text);
            return 0;
        });
    }

    [Fact]
    public void Stepper_buttons_step_and_decrease_stops_at_zero()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder();
            var stepper = BoundStepper(holder);
            var decrease = Part<Button>(stepper, "DecreaseButton");
            var increase = Part<Button>(stepper, "IncreaseButton");

            Click(decrease);
            Assert.Equal(0m, holder.Amount);

            Click(increase);
            Click(increase);
            Assert.Equal(1m, holder.Amount);

            Click(decrease);
            Click(decrease);
            Click(decrease);
            Assert.Equal(0m, holder.Amount);
            return 0;
        });
    }

    [Fact]
    public void Stepper_integer_mode_steps_by_one_and_shows_whole_numbers()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder { Amount = 2m };
            var stepper = BoundStepper(holder, isInteger: true);

            Click(Part<Button>(stepper, "IncreaseButton"));

            Assert.Equal(3m, holder.Amount);
            Assert.Equal("3", Part<TextBox>(stepper, "Box").Text);
            return 0;
        });
    }

    [Fact]
    public void Stepper_buttons_have_automation_names()
    {
        WpfTestHost.Run(() =>
        {
            var stepper = new NumericStepper();

            Assert.Equal("Kurangi", System.Windows.Automation.AutomationProperties.GetName(Part<Button>(stepper, "DecreaseButton")));
            Assert.Equal("Tambah", System.Windows.Automation.AutomationProperties.GetName(Part<Button>(stepper, "IncreaseButton")));
            return 0;
        });
    }

    [Fact]
    public void Stepper_parts_have_the_specified_size()
    {
        var sizes = WpfTestHost.Run(() =>
        {
            var stepper = new NumericStepper { Value = 1m };
            Layout(stepper);
            var decrease = Part<Button>(stepper, "DecreaseButton");
            var box = Part<TextBox>(stepper, "Box");
            return (decrease.ActualWidth, decrease.ActualHeight, box.ActualWidth, box.ActualHeight);
        });

        Assert.Equal((33d, 38d, 84d, 40d), sizes);
    }

    [Fact]
    public void Time_picker_value_follows_typing_and_formats_on_focus_loss()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder();
            var picker = BoundPicker(holder);
            var start = Part<TextBox>(picker, "StartBox");

            Type(start, "2100");
            Assert.Equal(new TimeOnly(21, 0), holder.Start);
            Assert.Equal("2100", start.Text);

            LoseFocus(start, picker);
            Assert.Equal("21:00", start.Text);
            return 0;
        });
    }

    [Fact]
    public void Time_picker_accepts_a_range_that_crosses_midnight()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder();
            var picker = BoundPicker(holder);

            Type(Part<TextBox>(picker, "StartBox"), "2300");
            Type(Part<TextBox>(picker, "EndBox"), "0030");

            Assert.Equal(new TimeOnly(23, 0), holder.Start);
            Assert.Equal(new TimeOnly(0, 30), holder.End);
            return 0;
        });
    }

    [Fact]
    public void Time_picker_restores_the_last_valid_time_when_the_text_is_invalid()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder { Start = new TimeOnly(9, 30) };
            var picker = BoundPicker(holder);
            var start = Part<TextBox>(picker, "StartBox");

            Type(start, "25:00");
            Assert.Equal(new TimeOnly(9, 30), holder.Start);

            LoseFocus(start, picker);
            Assert.Equal("09:30", start.Text);
            return 0;
        });
    }

    [Fact]
    public void Time_picker_clear_button_empties_both_times_and_both_boxes()
    {
        WpfTestHost.Run(() =>
        {
            var holder = new Holder { Start = new TimeOnly(21, 0), End = new TimeOnly(23, 30) };
            var picker = BoundPicker(holder);

            Click(Part<Button>(picker, "ClearButton"));

            Assert.Null(holder.Start);
            Assert.Null(holder.End);
            Assert.Equal(string.Empty, Part<TextBox>(picker, "StartBox").Text);
            Assert.Equal(string.Empty, Part<TextBox>(picker, "EndBox").Text);
            return 0;
        });
    }

    [Fact]
    public void Time_picker_clear_button_is_named_and_only_visible_when_a_time_is_set()
    {
        WpfTestHost.Run(() =>
        {
            var picker = new TimeRangePicker();
            var clear = Part<Button>(picker, "ClearButton");
            Layout(picker);
            Assert.Equal(Visibility.Hidden, clear.Visibility);
            Assert.Equal("Hapus waktu", System.Windows.Automation.AutomationProperties.GetName(clear));

            picker.StartTime = new TimeOnly(21, 0);
            Layout(picker);
            Assert.Equal(Visibility.Visible, clear.Visibility);
            return 0;
        });
    }

    [Fact]
    public void Switch_toggles_and_shows_Ya_or_Tidak()
    {
        WpfTestHost.Run(() =>
        {
            var toggle = new ToggleButton { Style = (Style)Application.Current.Resources["ToggleButton.Switch"] };
            Layout(toggle);
            var label = (TextBlock)toggle.Template.FindName("Label", toggle);
            Assert.Equal("Tidak", label.Text);

            toggle.IsChecked = true;
            Layout(toggle);
            Assert.Equal("Ya", label.Text);
            return 0;
        });
    }

    [Fact]
    public void Switch_track_is_44_by_24()
    {
        var size = WpfTestHost.Run(() =>
        {
            var toggle = new ToggleButton { Style = (Style)Application.Current.Resources["ToggleButton.Switch"] };
            Layout(toggle);
            var track = (FrameworkElement)toggle.Template.FindName("Track", toggle);
            return (track.ActualWidth, track.ActualHeight);
        });

        Assert.Equal((44d, 24d), size);
    }
}
