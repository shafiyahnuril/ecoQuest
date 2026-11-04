using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// The auth pages draw their own error look (red border + text under the field). WPF's default error adorner (a red rectangle) must
/// never appear, so no element of Login/Register may keep a non-null <see cref="Validation.ErrorTemplate"/>.
/// </summary>
[Collection(WpfCollection.Name)]
public class AuthErrorTemplateTests(Xunit.Abstractions.ITestOutputHelper output)
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

    private static void Layout(FrameworkElement view)
    {
        for (var i = 0; i < 2; i++)
        {
            view.Measure(new Size(1280, 800));
            view.Arrange(new Rect(0, 0, 1280, 800));
            view.UpdateLayout();
        }
    }

    private static RegisterViewModel NewRegisterViewModel()
    {
        var world = new FakeWorld(FakeScenario.Normal);
        return new RegisterViewModel(world.Auth, world.Profile, Substitute.For<INavigationService>());
    }

    [Fact]
    public void Every_text_input_of_Login_and_Register_has_no_error_template()
    {
        var offenders = WpfTestHost.Run(() =>
        {
            var world = new FakeWorld(FakeScenario.Normal);
            var login = new LoginView { DataContext = new LoginViewModel(world.Auth, Substitute.For<INavigationService>()) };
            var register = new RegisterView { DataContext = NewRegisterViewModel() };
            Layout(login);
            Layout(register);

            var inputs = 0;
            var bad = new List<string>();
            foreach (var (name, view) in new[] { ("Login", (FrameworkElement)login), ("Register", register) })
            {
                foreach (var control in Descendants(view).OfType<Control>().Where(c => c is TextBox or PasswordBox or ComboBox or Controls.PasswordInput))
                {
                    inputs++;
                    if (Validation.GetErrorTemplate(control) is not null)
                    {
                        bad.Add($"{name}: {control.GetType().Name} {control.Name}");
                    }
                }
            }

            Assert.True(inputs >= 10, $"only {inputs} inputs found: the tree walk is broken");
            return bad;
        });

        Assert.Empty(offenders);
    }

    [Fact]
    public void No_element_with_a_validation_error_in_Register_can_show_the_default_adorner()
    {
        var offenders = WpfTestHost.Run(() =>
        {
            var vm = NewRegisterViewModel();
            var view = new RegisterView { DataContext = vm };
            Layout(view);

            // Every field fails: any binding to a field property now carries a validation error.
            vm.Password = "abc";
            vm.ConfirmPassword = "xyz";
            foreach (var field in new[] { "UserName", "Email", "Password", "ConfirmPassword", "City" })
            {
                vm.ValidateFieldCommand.Execute(field);
            }

            vm.City = "";
            vm.ValidateFieldCommand.Execute("City");
            WpfTestHost.Flush();
            Layout(view);

            var withError = Descendants(view).OfType<FrameworkElement>().Where(Validation.GetHasError).ToList();
            Assert.True(withError.Count >= 5, $"expected the inputs to have errors, found {withError.Count}");
            return withError.Where(e => Validation.GetErrorTemplate(e) is not null).Select(e => $"{e.GetType().Name} {e.Name}").ToList();
        });

        foreach (var offender in offenders)
        {
            output.WriteLine("default error template on: " + offender);
        }

        Assert.Empty(offenders);
    }
}
