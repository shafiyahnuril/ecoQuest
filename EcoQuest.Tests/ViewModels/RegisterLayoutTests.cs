using System.Windows;
using System.Windows.Controls;
using EcoQuest.Controls;
using EcoQuest.Converters;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Measures the real RegisterView with the real theme at several window sizes (DIP), on the WPF host shared with LoginLayoutTests
/// (one Application per process; the collection makes both classes run one after another). Needs a desktop session (no window is shown).
/// </summary>
[Collection(WpfCollection.Name)]
public class RegisterLayoutTests(ITestOutputHelper output)
{
    private sealed record Metrics(
        double Width, double Height, double Page, double FormColumn, double Card,
        double UserNameTop, double EmailTop, double PasswordTop, double ConfirmTop, double CityTop,
        double UserNameWidth, double EmailWidth, double CityWidth, double Gap,
        double UserNameHeight, double EmailHeight, double PasswordHeight, double ConfirmHeight, double CityHeight,
        double FieldGap);

    private static RegisterViewModel NewViewModel() =>
        new(new FakeWorld(FakeScenario.Normal).Auth, new FakeWorld(FakeScenario.Normal).Profile, Substitute.For<INavigationService>());

    private static RegisterView NewView(double width, double height, RegisterViewModel? vm = null)
    {
        var view = new RegisterView { DataContext = vm ?? NewViewModel() };
        Layout(view, width, height);
        return view;
    }

    private static void Layout(RegisterView view, double width, double height)
    {
        // Twice: the first pass sets ActualHeight, which the height-driven bindings then read.
        for (var i = 0; i < 2; i++)
        {
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
        }
    }

    private static T Named<T>(FrameworkElement view, string name) where T : class => (T)view.FindName(name);

    private static double Top(FrameworkElement view, string name) => Named<FrameworkElement>(view, name).TranslatePoint(new Point(0, 0), view).Y;

    private static double Left(FrameworkElement view, string name) => Named<FrameworkElement>(view, name).TranslatePoint(new Point(0, 0), view).X;

    private static Metrics Measure(double width, double height)
    {
        var view = NewView(width, height);
        var page = Named<Grid>(view, "PageRoot");
        var userName = Named<FrameworkElement>(view, "UserNameBox");
        var email = Named<FrameworkElement>(view, "EmailBox");
        var password = Named<FrameworkElement>(view, "PasswordField");
        var confirm = Named<FrameworkElement>(view, "ConfirmField");
        var city = Named<FrameworkElement>(view, "CityBox");
        return new Metrics(
            width, height, page.ActualWidth, page.ColumnDefinitions[1].ActualWidth, Named<FrameworkElement>(view, "FormCard").ActualWidth,
            Top(view, "UserNameBox"), Top(view, "EmailBox"), Top(view, "PasswordField"), Top(view, "ConfirmField"), Top(view, "CityBox"),
            userName.ActualWidth, email.ActualWidth, city.ActualWidth, Left(view, "EmailBox") - Left(view, "UserNameBox") - userName.ActualWidth,
            userName.ActualHeight, email.ActualHeight, password.ActualHeight, confirm.ActualHeight, city.ActualHeight,
            // distance between the bottom of the Username message slot and the top of the Password label = the field gap
            Top(view, "PasswordLabel") - (Top(view, "UserNameMessage") + Named<FrameworkElement>(view, "UserNameMessage").ActualHeight));
    }

    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private static List<Metrics> RunAll() => WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H)).ToList());

    [Fact]
    public void Card_is_at_most_490_and_the_two_columns_are_16_apart_and_equally_wide()
    {
        var all = RunAll();
        foreach (var m in all)
        {
            output.WriteLine(
                $"{m.Width}x{m.Height}: page {m.Page:F1}, form col {m.FormColumn:F1}, card {m.Card:F1} | " +
                $"username {m.UserNameWidth:F1} gap {m.Gap:F1} email {m.EmailWidth:F1}, city {m.CityWidth:F1} | " +
                $"heights {m.UserNameHeight:F1}/{m.EmailHeight:F1}/{m.PasswordHeight:F1}/{m.ConfirmHeight:F1}/{m.CityHeight:F1} | " +
                $"tops user {m.UserNameTop:F1} email {m.EmailTop:F1} pass {m.PasswordTop:F1} confirm {m.ConfirmTop:F1} city {m.CityTop:F1} | field gap {m.FieldGap:F1}");

            Assert.True(m.Card <= 490.5);
            Assert.Equal(m.UserNameWidth, m.EmailWidth, 1);
            Assert.Equal(16, m.Gap, 1);
            Assert.Equal(m.UserNameWidth * 2 + m.Gap, m.CityWidth, 1);
        }

        Assert.Equal(490, all[1].Card, 1);
        Assert.Equal(490, all[2].Card, 1);
        Assert.Equal(490, all[3].Card, 1);
    }

    [Fact]
    public void Inputs_follow_clamp_36_4_7vh_44_of_the_view_height()
    {
        foreach (var m in RunAll())
        {
            var expected = VhClampConverter.Clamp(m.Height, 36, 44, 0.047);
            Assert.Equal(expected, m.UserNameHeight, 0.5);
            Assert.Equal(expected, m.EmailHeight, 0.5);
            Assert.Equal(expected, m.PasswordHeight, 0.5);
            Assert.Equal(expected, m.ConfirmHeight, 0.5);
            Assert.Equal(expected, m.CityHeight, 0.5);
        }
    }

    [Fact]
    public void The_boxes_of_a_row_share_their_top_even_though_only_one_column_has_a_hint()
    {
        foreach (var m in RunAll())
        {
            Assert.Equal(m.UserNameTop, m.EmailTop, 0.5);
            Assert.Equal(m.PasswordTop, m.ConfirmTop, 0.5);
            Assert.True(m.PasswordTop > m.UserNameTop);
            Assert.True(m.CityTop > m.PasswordTop);
        }
    }

    [Fact]
    public void Field_gap_follows_clamp_9_1_5vh_16_and_is_7_on_short_windows()
    {
        foreach (var m in RunAll())
        {
            var expected = m.Height <= 740 ? 7 : VhClampConverter.Clamp(m.Height, 9, 16, 0.015);
            // the hint is the tallest part of the left cell; its bottom edge to the next label is the field margin
            Assert.Equal(expected, m.FieldGap, 0.5);
        }
    }

    [Fact]
    public void An_error_takes_the_place_of_the_hint_in_the_same_slot_and_keeps_the_columns_aligned()
    {
        // Offsets are relative to the username box: the whole card is centred, so a message that wraps to a second line makes the card
        // taller and moves it up by half of that. The slot itself (message below its box) and the row alignment must not change.
        var result = WpfTestHost.Run(() =>
        {
            var vm = NewViewModel();
            var view = NewView(1536, 864, vm);
            (double Slot, double Column, double PasswordRow, string Text, object Foreground, bool Tag) Snapshot()
            {
                var message = Named<TextBlock>(view, "UserNameMessage");
                return (Top(view, "UserNameMessage") - Top(view, "UserNameBox"), Top(view, "EmailBox") - Top(view, "UserNameBox"),
                    Top(view, "PasswordField") - Top(view, "ConfirmField"), message.Text, message.Foreground, (bool)message.Tag);
            }

            var before = Snapshot();
            vm.UserName = "ab";
            vm.ValidateFieldCommand.Execute("UserName");
            WpfTestHost.Flush();
            Layout(view, 1536, 864);
            return (before, after: Snapshot(), vm);
        });

        output.WriteLine($"hint: '{result.before.Text}' -> error: '{result.after.Text}'");
        Assert.Equal(result.before.Slot, result.after.Slot, 0.5);        // same slot under the same box
        Assert.Equal(0, result.after.Column, 0.5);                       // Email stays level with Username
        Assert.Equal(0, result.after.PasswordRow, 0.5);
        Assert.Equal("3–30 karakter: huruf, angka, titik, atau _", result.before.Text);
        Assert.NotEqual(result.before.Text, result.after.Text);
        Assert.Equal(result.vm.GetErrors("UserName").Cast<System.ComponentModel.DataAnnotations.ValidationResult>().First().ErrorMessage, result.after.Text);
        Assert.NotEqual(result.before.Foreground, result.after.Foreground);   // red instead of grey
        Assert.True(result.after.Tag);
        Assert.False(result.before.Tag);
    }

    [Fact]
    public void Both_password_columns_get_the_red_border_when_they_have_an_error()
    {
        var states = WpfTestHost.Run(() =>
        {
            var vm = NewViewModel();
            var view = NewView(1280, 800, vm);
            var password = Named<PasswordInput>(view, "PasswordField");
            var confirm = Named<PasswordInput>(view, "ConfirmField");
            var passwordBorder = Named<Border>(password, "InvalidBorder");
            var confirmBorder = Named<Border>(confirm, "InvalidBorder");
            var clean = (password.IsInvalid, confirm.IsInvalid, passwordBorder.Visibility, confirmBorder.Visibility);

            vm.Password = "abc";                      // too weak
            vm.ConfirmPassword = "xyz";               // and different
            vm.ValidateFieldCommand.Execute("Password");
            vm.ValidateFieldCommand.Execute("ConfirmPassword");
            WpfTestHost.Flush();
            Layout(view, 1280, 800);

            var failed = (password.IsInvalid, confirm.IsInvalid, passwordBorder.Visibility, confirmBorder.Visibility,
                Named<TextBlock>(view, "PasswordMessage").Text, Named<TextBlock>(view, "ConfirmMessage").Text);

            vm.Password = "Rahasia123";
            vm.ConfirmPassword = "Rahasia123";
            WpfTestHost.Flush();
            Layout(view, 1280, 800);
            var fixedAgain = (password.IsInvalid, confirm.IsInvalid, passwordBorder.Visibility, confirmBorder.Visibility);
            return (clean, failed, fixedAgain, vm);
        });

        Assert.Equal((false, false, Visibility.Collapsed, Visibility.Collapsed), states.clean);
        Assert.True(states.failed.Item1);
        Assert.True(states.failed.Item2);
        Assert.Equal(Visibility.Visible, states.failed.Item3);
        Assert.Equal(Visibility.Visible, states.failed.Item4);
        Assert.Equal("Password minimal 8 karakter dan mengandung huruf serta angka", states.failed.Item5);
        Assert.Equal("Konfirmasi password tidak sama", states.failed.Item6);
        Assert.Equal((false, false, Visibility.Collapsed, Visibility.Collapsed), states.fixedAgain);
    }

    [Fact]
    public void Submit_button_and_fields_are_disabled_while_busy()
    {
        var enabled = WpfTestHost.Run(() =>
        {
            var vm = NewViewModel();
            var view = NewView(1280, 800, vm);
            vm.IsBusy = true;
            Layout(view, 1280, 800);
            return (Named<TextBox>(view, "UserNameBox").IsEnabled, Named<PasswordInput>(view, "PasswordField").IsEnabled, Named<ComboBox>(view, "CityBox").IsEnabled);
        });

        Assert.Equal((false, false, false), enabled);
    }
}
