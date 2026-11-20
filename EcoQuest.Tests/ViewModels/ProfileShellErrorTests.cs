using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Controls;
using EcoQuest.Core.Contracts.Dtos;
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
/// ProfileView inside the real MainWindow: field errors must show only as the red border and the text under the field, never as WPF's default
/// error adorner on a container. The shell binds its content to a ViewModel that is itself an INotifyDataErrorInfo, so those bindings must not validate.
/// </summary>
[Collection(WpfCollection.Name)]
public class ProfileShellErrorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ecoquest-profile-shell-" + Guid.NewGuid().ToString("N"));

    public ProfileShellErrorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static T Find<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants(root).OfType<T>().First(e => e.Name == name);

    private ProfileViewModel NewViewModel(FakeWorld world, IUiText text)
    {
        world.Auth.LoginAsync(new LoginRequest(FakeData.DemoUserName, FakeData.DemoPassword)).GetAwaiter().GetResult();
        return new ProfileViewModel(new FakeProfileService(world.Context, world.Store, world.Session, _root), new FakePhotoStorageService(_root),
            Substitute.For<IDialogService>(), text, world.Clock);
    }

    private void InWindow(Action<ProfileViewModel, ecoQuest.MainWindow> test) => WpfTestHost.Run(() =>
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var text = new ResourceUiText();
        var vm = NewViewModel(world, text);
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();
        var nav = Substitute.For<INavigationService>();
        nav.Current.Returns(vm);
        var shell = new ShellViewModel(nav, Substitute.For<IDialogService>(), text, new ToastViewModel(), world.Session, world.Auth, world.Review);
        var window = new ecoQuest.MainWindow { DataContext = shell, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, Width = 1280, Height = 800 };
        var template = new DataTemplate(typeof(ProfileViewModel)) { VisualTree = new FrameworkElementFactory(typeof(ProfileView)) };
        window.Resources.Add(template.DataTemplateKey, template);
        window.Show();
        nav.Navigated += Raise.Event();
        window.UpdateLayout();
        try
        {
            test(vm, window);
        }
        finally
        {
            window.Close();
        }

        return 0;
    });

    private static void Submit(ProfileViewModel vm, ecoQuest.MainWindow window, string old, string next, string confirm, string? city = null)
    {
        vm.OldPassword = old;
        vm.NewPassword = next;
        vm.ConfirmNewPassword = confirm;
        vm.ChangePasswordCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        if (city is not null)
        {
            vm.City = city;
            vm.SaveProfileCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        }

        WpfTestHost.Flush();
        window.UpdateLayout();
    }

    [Fact]
    public async Task A_wrong_old_password_is_an_error_of_the_OldPassword_property_only()
    {
        var vm = NewViewModel(new FakeWorld(FakeScenario.Normal), new ResourceUiText());
        vm.OldPassword = "salah";
        vm.NewPassword = "Baru12345";
        vm.ConfirmNewPassword = "Baru12345";

        await vm.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Equal("Password lama salah", vm.OldPasswordError);
        Assert.Null(vm.NewPasswordError);
        Assert.Null(vm.ConfirmNewPasswordError);
        Assert.Null(vm.CityError);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.GetErrors(null).Cast<object>());
    }

    [Fact]
    public void A_wrong_old_password_leaves_only_the_old_password_field_in_error_inside_the_shell()
    {
        InWindow((vm, window) =>
        {
            Submit(vm, window, "salah", "Baru12345", "Baru12345");

            var inError = Descendants(window).Prepend(window).OfType<FrameworkElement>().Where(Validation.GetHasError).ToList();
            Assert.Equal(["OldPasswordField"], inError.Select(e => e.Name));
            Assert.All(inError, e => Assert.Null(Validation.GetErrorTemplate(e)));
            Assert.True(Find<PasswordInput>(window, "OldPasswordField").IsInvalid);
        });
    }

    [Fact]
    public void Every_field_error_shows_in_red_under_its_field()
    {
        InWindow((vm, window) =>
        {
            Submit(vm, window, FakeData.DemoPassword, "pendek", "beda", city: "Atlantis");
            var error = (SolidColorBrush)Application.Current.FindResource("State.Error");

            foreach (var (message, field) in new[]
            {
                ("NewPasswordMessage", "NewPasswordField"), ("ConfirmPasswordMessage", "ConfirmPasswordField"), ("CityMessage", "CityBox"),
            })
            {
                var text = Find<TextBlock>(window, message);
                var input = Find<FrameworkElement>(window, field);
                Assert.Equal(Visibility.Visible, text.Visibility);
                Assert.False(string.IsNullOrWhiteSpace(text.Text));
                Assert.Equal(error.Color, ((SolidColorBrush)text.Foreground).Color);
                Assert.True(text.ActualHeight > 0);
                Assert.True(text.TranslatePoint(new Point(0, 0), window).Y >= input.TranslatePoint(new Point(0, input.ActualHeight), window).Y);
            }

            Assert.Equal(Visibility.Collapsed, Find<TextBlock>(window, "OldPasswordMessage").Visibility);
        });
    }

    [Fact]
    public void A_wrong_old_password_shows_its_message_under_the_old_password_field_and_pushes_the_next_field_down()
    {
        InWindow((vm, window) =>
        {
            var newField = Find<FrameworkElement>(window, "NewPasswordField");
            var before = newField.TranslatePoint(new Point(0, 0), window).Y;

            Submit(vm, window, "salah", "Baru12345", "Baru12345");

            var message = Find<TextBlock>(window, "OldPasswordMessage");
            Assert.Equal(Visibility.Visible, message.Visibility);
            Assert.Equal("Password lama salah", message.Text);
            Assert.True(newField.TranslatePoint(new Point(0, 0), window).Y > before);
        });
    }
}
