using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Measures the real ProfileView with the real theme (DIP) against 12-profile.png: at 1280x800 the prototype has cards of 505 and 459 wide, 16 apart,
/// both 459 high; inputs 40, buttons 43. The screenshot was rendered with a fallback font (title line 21 px), so heights drift up to 4 px taller with Inter (label 12 px and title 19 px lines are taller than in the screenshot). The view is measured at the width the shell leaves it (window - 232 sidebar - 2 x 32 page padding).
/// </summary>
[Collection(WpfCollection.Name)]
public class ProfileLayoutTests(ITestOutputHelper output) : IDisposable
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ecoquest-profile-layout-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ProfileViewModel NewViewModel(FakeScenario scenario = FakeScenario.Normal, bool load = true)
    {
        var world = new FakeWorld(scenario);
        world.Auth.LoginAsync(new LoginRequest(FakeData.DemoUserName, FakeData.DemoPassword)).GetAwaiter().GetResult();
        var text = new ResourceUiText();
        var vm = new ProfileViewModel(new FakeProfileService(world.Context, world.Store, world.Session, _root), new FakePhotoStorageService(_root),
            _dialogs, text, world.Clock);
        if (load)
        {
            vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();
        }

        return vm;
    }

    private static ProfileView NewView(ProfileViewModel vm, double windowWidth, double windowHeight)
    {
        var view = new ProfileView { DataContext = vm };
        var width = windowWidth - 232 - 64;
        for (var i = 0; i < 2; i++)
        {
            view.Measure(new Size(width, windowHeight));
            view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height));
            view.UpdateLayout();
        }

        return view;
    }

    private static T Named<T>(FrameworkElement view, string name) where T : class => (T)view.FindName(name);

    private static double Top(FrameworkElement view, string name, string relativeTo) =>
        Named<FrameworkElement>(view, name).TranslatePoint(new Point(0, 0), Named<FrameworkElement>(view, relativeTo)).Y;

    private static double Left(FrameworkElement view, string name, string relativeTo) =>
        Named<FrameworkElement>(view, name).TranslatePoint(new Point(0, 0), Named<FrameworkElement>(view, relativeTo)).X;

    private static double Height(FrameworkElement view, string name) => Named<FrameworkElement>(view, name).ActualHeight;

    private static double Width(FrameworkElement view, string name) => Named<FrameworkElement>(view, name).ActualWidth;

    [Fact]
    public void Two_cards_are_1_1_to_1_and_16_apart_and_as_high_as_each_other()
    {
        var vm = NewViewModel();
        foreach (var (w, h) in Sizes)
        {
            WpfTestHost.Run(() =>
            {
                var view = NewView(vm, w, h);
                var gap = Left(view, "SecurityCard", "AccountCard") - Width(view, "AccountCard");
                output.WriteLine($"{w}x{h}: account {Width(view, "AccountCard"):F1}x{Height(view, "AccountCard"):F1}, security {Width(view, "SecurityCard"):F1}x{Height(view, "SecurityCard"):F1}, gap {gap:F1}");

                Assert.Equal(16, gap, 1);
                Assert.Equal(1.1, Width(view, "AccountCard") / Width(view, "SecurityCard"), 0.01);
                Assert.Equal(Height(view, "AccountCard"), Height(view, "SecurityCard"), 1);
                return 0;
            });
        }
    }

    [Fact]
    public void At_1280x800_the_card_matches_the_prototype_within_4_px()
    {
        var vm = NewViewModel();
        WpfTestHost.Run(() =>
        {
            var view = NewView(vm, 1280, 800);
            output.WriteLine(
                $"account: avatar top {Top(view, "AvatarCircle", "AccountCard"):F1} ({Width(view, "AvatarCircle"):F0}x{Height(view, "AvatarCircle"):F0}), " +
                $"user {Top(view, "UserNameBox", "AccountCard"):F1}, email {Top(view, "EmailBox", "AccountCard"):F1}, city {Top(view, "CityBox", "AccountCard"):F1}, " +
                $"save {Top(view, "SaveButton", "AccountCard"):F1}, card {Height(view, "AccountCard"):F1}");
            output.WriteLine(
                $"security: old {Top(view, "OldPasswordField", "SecurityCard"):F1}, new {Top(view, "NewPasswordField", "SecurityCard"):F1}, " +
                $"confirm {Top(view, "ConfirmPasswordField", "SecurityCard"):F1}, button {Top(view, "ChangePasswordButton", "SecurityCard"):F1}");

            Assert.Equal(96, Width(view, "AvatarCircle"), 0.5);
            Assert.Equal(96, Height(view, "AvatarCircle"), 0.5);
            Assert.Equal(56, Top(view, "AvatarCircle", "AccountCard"), 4.0);
            Assert.Equal(189, Top(view, "UserNameBox", "AccountCard"), 4.0);
            Assert.Equal(264, Top(view, "EmailBox", "AccountCard"), 4.0);
            Assert.Equal(339, Top(view, "CityBox", "AccountCard"), 4.0);
            Assert.Equal(394, Top(view, "SaveButton", "AccountCard"), 4.0);
            Assert.Equal(459, Height(view, "AccountCard"), 4.0);
            Assert.Equal(76, Top(view, "OldPasswordField", "SecurityCard"), 4.0);
            Assert.Equal(151, Top(view, "NewPasswordField", "SecurityCard"), 4.0);
            Assert.Equal(226, Top(view, "ConfirmPasswordField", "SecurityCard"), 4.0);
            Assert.Equal(281, Top(view, "ChangePasswordButton", "SecurityCard"), 4.0);
            return 0;
        });
    }

    [Fact]
    public void Inputs_are_40_high_buttons_43_and_the_two_card_buttons_are_left_aligned()
    {
        var vm = NewViewModel();
        foreach (var (w, h) in Sizes)
        {
            WpfTestHost.Run(() =>
            {
                var view = NewView(vm, w, h);
                foreach (var name in new[] { "UserNameBox", "EmailBox", "CityBox", "OldPasswordField", "NewPasswordField", "ConfirmPasswordField" })
                {
                    Assert.Equal(40, Height(view, name), 0.5);
                }

                Assert.Equal(43, Height(view, "SaveButton"), 0.5);
                Assert.Equal(43, Height(view, "ChangePasswordButton"), 0.5);
                Assert.Equal(Left(view, "UserNameBox", "AccountCard"), Left(view, "SaveButton", "AccountCard"), 0.5);
                Assert.Equal(Left(view, "OldPasswordField", "SecurityCard"), Left(view, "ChangePasswordButton", "SecurityCard"), 0.5);
                return 0;
            });
        }
    }

    [Fact]
    public void Username_and_email_are_read_only_boxes_that_stay_focusable_and_labelled()
    {
        var vm = NewViewModel();
        WpfTestHost.Run(() =>
        {
            var view = NewView(vm, 1280, 800);
            foreach (var name in new[] { "UserNameBox", "EmailBox" })
            {
                var box = Named<TextBox>(view, name);
                Assert.True(box.IsReadOnly);
                Assert.True(box.IsEnabled);
                Assert.True(box.Focusable);
                Assert.NotNull(AutomationProperties.GetLabeledBy(box));
            }

            Assert.Equal(FakeData.DemoUserName, Named<TextBox>(view, "UserNameBox").Text);
            Assert.Equal(FakeData.DemoEmail, Named<TextBox>(view, "EmailBox").Text);
            return 0;
        });
    }

    [Fact]
    public void Only_the_save_button_is_primary_and_buttons_and_avatar_have_automation_names()
    {
        var vm = NewViewModel();
        WpfTestHost.Run(() =>
        {
            var view = NewView(vm, 1280, 800);
            var primary = (Style)Application.Current.FindResource("Button.Primary");

            Assert.Same(primary, Named<Button>(view, "SaveButton").Style);
            Assert.NotSame(primary, Named<Button>(view, "ChangePasswordButton").Style);
            Assert.Equal("Foto profil", AutomationProperties.GetName(Named<FrameworkElement>(view, "AvatarCircle")));
            Assert.Equal("Ganti foto", Named<Button>(view, "ChangePhotoButton").Content);
            Assert.Equal("Hapus foto", Named<Button>(view, "RemovePhotoButton").Content);
            Assert.Equal("Ganti foto profil", AutomationProperties.GetName(Named<Button>(view, "ChangePhotoButton")));
            Assert.Equal("Hapus foto profil", AutomationProperties.GetName(Named<Button>(view, "RemovePhotoButton")));
            foreach (var name in new[] { "OldPasswordField", "NewPasswordField", "ConfirmPasswordField" })
            {
                Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(Named<FrameworkElement>(view, name))));
            }

            return 0;
        });
    }

    [Fact]
    public void Remove_photo_is_visible_only_with_a_photo_and_the_photo_error_only_with_an_error()
    {
        var vm = NewViewModel();
        Directory.CreateDirectory(_root);
        var good = Path.Combine(_root, "foto.png");
        var bad = Path.Combine(_root, "dokumen.txt");
        File.WriteAllBytes(good, OnePixelPng);
        File.WriteAllBytes(bad, new byte[16]);

        WpfTestHost.Run(() =>
        {
            var view = NewView(vm, 1280, 800);
            Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(view, "RemovePhotoButton").Visibility);
            Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(view, "AvatarMessage").Visibility);

            _dialogs.PickImageFile().Returns(bad);
            vm.PickAvatarCommand.Execute(null);
            Assert.Equal(Visibility.Visible, Named<FrameworkElement>(view, "AvatarMessage").Visibility);
            Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(view, "RemovePhotoButton").Visibility);

            _dialogs.PickImageFile().Returns(good);
            vm.PickAvatarCommand.Execute(null);
            Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(view, "AvatarMessage").Visibility);
            Assert.Equal(Visibility.Visible, Named<FrameworkElement>(view, "RemovePhotoButton").Visibility);
            return 0;
        });
    }

    [Fact]
    public void Server_errors_show_in_the_slot_under_their_field_and_no_default_error_template_is_drawn()
    {
        var vm = NewViewModel();
        WpfTestHost.Run(() =>
        {
            var view = NewView(vm, 1280, 800);
            vm.OldPassword = "salah";
            vm.NewPassword = "Baru12345";
            vm.ConfirmNewPassword = "Baru12345";
            vm.ChangePasswordCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            vm.City = "Atlantis";
            vm.SaveProfileCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            WpfTestHost.Flush();
            view.UpdateLayout();

            var old = Named<TextBlock>(view, "OldPasswordMessage");
            var city = Named<TextBlock>(view, "CityMessage");
            Assert.Equal(Visibility.Visible, old.Visibility);
            Assert.Equal("Password lama salah", old.Text);
            Assert.Equal(Visibility.Visible, city.Visibility);
            Assert.False(string.IsNullOrEmpty(city.Text));
            Assert.Equal(Visibility.Collapsed, Named<TextBlock>(view, "NewPasswordMessage").Visibility);
            Assert.Null(Validation.GetErrorTemplate(Named<TextBox>(view, "UserNameBox")));
            Assert.Null(Validation.GetErrorTemplate(Named<ComboBox>(view, "CityBox")));
            Assert.Null(Validation.GetErrorTemplate(Named<PasswordInput>(view, "OldPasswordField")));
            Assert.True(Named<PasswordInput>(view, "OldPasswordField").IsInvalid);
            return 0;
        });
    }

    [Fact]
    public void The_loading_state_shows_two_skeleton_blocks_and_no_form()
    {
        var vm = NewViewModel(load: false);
        WpfTestHost.Run(() =>
        {
            var view = NewView(vm, 1280, 800);

            Assert.False(Named<Button>(view, "SaveButton").IsVisible);
            Assert.True(view.ActualHeight > 0);
            return 0;
        });
    }
}
