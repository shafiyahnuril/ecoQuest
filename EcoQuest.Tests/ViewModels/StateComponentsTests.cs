using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Controls;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>Structure and behaviour of the shared state components, using the real theme on the shared WPF host. Visual fidelity is NOT checked here.</summary>
[Collection(WpfCollection.Name)]
public class StateComponentsTests
{
    private static Visibility Part(StateHost host, string name)
    {
        EnsureTemplate(host);
        Assert.True(host.Template is not null, $"Template null; Style={(host.Style is null ? "null" : "set")}; implicit={Application.Current.TryFindResource(typeof(StateHost)) is not null}");
        var part = host.Template!.FindName(name, host);
        Assert.True(part is not null, $"FindName({name}) null; visualChildren={System.Windows.Media.VisualTreeHelper.GetChildrenCount(host)}");
        return ((UIElement)part!).Visibility;
    }

    // An element made in code gets its implicit style (and so its template) at EndInit, as XAML-created ones do.
    private static void EnsureTemplate(StateHost host)
    {
        if (!host.IsInitialized)
        {
            host.BeginInit();
            host.EndInit();
        }

        host.Measure(new Size(400, 300));
        host.ApplyTemplate();
    }

    private static StateHost NewHost(LoadState state, bool isEmpty = false) => new()
    {
        State = state,
        IsEmpty = isEmpty,
        LoadingContent = new TextBlock { Text = "loading" },
        EmptyContent = new TextBlock { Text = "empty" },
        Content = new TextBlock { Text = "content" },
        ErrorMessage = "pesan",
    };

    [Theory]
    [InlineData(LoadState.Idle, false, "LoadingHost")]
    [InlineData(LoadState.Idle, true, "LoadingHost")]
    [InlineData(LoadState.Loading, false, "LoadingHost")]
    [InlineData(LoadState.Loading, true, "LoadingHost")]
    [InlineData(LoadState.Error, false, "ErrorHost")]
    [InlineData(LoadState.Error, true, "ErrorHost")]
    [InlineData(LoadState.Loaded, true, "EmptyHost")]
    [InlineData(LoadState.Loaded, false, "ContentHost")]
    public void StateHost_shows_exactly_the_matching_slot(LoadState state, bool isEmpty, string expectedVisible)
    {
        WpfTestHost.Run(() =>
        {
            var host = NewHost(state, isEmpty);
            foreach (var name in new[] { "LoadingHost", "ErrorHost", "EmptyHost", "ContentHost" })
            {
                var expected = name == expectedVisible ? Visibility.Visible : Visibility.Collapsed;
                Assert.True(expected == Part(host, name), $"{state}/IsEmpty={isEmpty}: {name} should be {expected}");
            }

            return 0;
        });
    }

    [Fact]
    public void StateHost_switches_when_properties_change()
    {
        WpfTestHost.Run(() =>
        {
            var host = NewHost(LoadState.Loading);
            Assert.Equal(Visibility.Visible, Part(host, "LoadingHost"));

            host.State = LoadState.Loaded;
            Assert.Equal(Visibility.Visible, Part(host, "ContentHost"));
            Assert.Equal(Visibility.Collapsed, Part(host, "LoadingHost"));

            host.IsEmpty = true;
            Assert.Equal(Visibility.Visible, Part(host, "EmptyHost"));
            Assert.Equal(Visibility.Collapsed, Part(host, "ContentHost"));

            host.State = LoadState.Error;
            Assert.Equal(Visibility.Visible, Part(host, "ErrorHost"));
            Assert.Equal(Visibility.Collapsed, Part(host, "EmptyHost"));
            return 0;
        });
    }

    // Runs the button's own click handling (event + command) synchronously; the UIA Invoke pattern is dispatched asynchronously.
    private static void Invoke(Button button)
        => typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);

    [Fact]
    public void ErrorBanner_shows_message_as_given_and_fires_retry_command()
    {
        WpfTestHost.Run(() =>
        {
            var taps = 0;
            var banner = new ErrorBanner { Message = "Tidak dapat terhubung ke server.", RetryText = "Coba lagi", RetryCommand = new RelayCommand(() => taps++) };
            banner.Measure(new Size(600, 200));
            banner.Arrange(new Rect(0, 0, 600, 200));
            banner.UpdateLayout();

            Assert.Equal("Tidak dapat terhubung ke server.", ((TextBlock)banner.FindName("MessageText")).Text);
            var button = (Button)banner.FindName("RetryButton");
            Assert.Equal("Coba lagi", button.Content);

            Invoke(button);
            Assert.Equal(1, taps);
            return 0;
        });
    }

    [Fact]
    public void StateHost_error_slot_uses_Common_Retry_and_the_bound_command()
    {
        WpfTestHost.Run(() =>
        {
            var taps = 0;
            var host = NewHost(LoadState.Error);
            host.RetryCommand = new RelayCommand(() => taps++);
            EnsureTemplate(host);
            var banner = (ErrorBanner)host.Template.FindName("ErrorHost", host);
            banner.Measure(new Size(600, 200));
            banner.Arrange(new Rect(0, 0, 600, 200));
            banner.UpdateLayout();

            var button = (Button)banner.FindName("RetryButton");
            Assert.Equal("Coba lagi", button.Content);
            Assert.Equal("pesan", ((TextBlock)banner.FindName("MessageText")).Text);
            Invoke(button);
            Assert.Equal(1, taps);
            return 0;
        });
    }

    [Fact]
    public void ErrorBanner_is_announced_assertively()
    {
        WpfTestHost.Run(() =>
        {
            var banner = new ErrorBanner { Message = "x" };
            var border = (Border)banner.Content;
            Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(border));
            return 0;
        });
    }

    [Fact]
    public void EmptyState_collapses_unset_parts_and_runs_its_action()
    {
        WpfTestHost.Run(() =>
        {
            var taps = 0;
            var empty = new EmptyState { Text = "Belum ada refleksi pada periode ini." };
            var image = (Image)empty.FindName("Illustration");
            var title = (TextBlock)empty.FindName("TitleText");
            var button = (Button)empty.FindName("ActionButton");

            Assert.Equal(Visibility.Collapsed, image.Visibility);
            Assert.Equal(Visibility.Collapsed, title.Visibility);
            Assert.Equal(Visibility.Collapsed, button.Visibility);

            empty.ImageSource = new BitmapImage(new Uri("pack://application:,,,/EcoQuest;component/Resources/Images/eco_sprout_in_mossy_pot.png"));
            empty.Title = "Judul";
            empty.ActionText = "Isi Evening Review";
            empty.ActionCommand = new RelayCommand(() => taps++);

            Assert.Equal(Visibility.Visible, image.Visibility);
            Assert.Equal(Visibility.Visible, title.Visibility);
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.Equal(85d, image.Width);
            Assert.Equal(IsOffscreenBehavior.Offscreen, AutomationProperties.GetIsOffscreenBehavior(image)); // decorative
            Invoke(button);
            Assert.Equal(1, taps);
            return 0;
        });
    }

    [Fact]
    public void Skeleton_takes_size_and_radius_from_the_caller_and_is_hidden_from_automation()
    {
        WpfTestHost.Run(() =>
        {
            var skeleton = new Skeleton { Width = 220, Height = 16, CornerRadius = new CornerRadius(14) };
            skeleton.Measure(new Size(400, 100));
            skeleton.Arrange(new Rect(0, 0, 400, 100));
            skeleton.UpdateLayout();

            var border = (Border)skeleton.Content;
            Assert.Equal(220, border.ActualWidth);
            Assert.Equal(16, border.ActualHeight);
            Assert.Equal(new CornerRadius(14), border.CornerRadius);
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(skeleton));
            Assert.Equal(new Skeleton().CornerRadius, new CornerRadius(8)); // default = Radius.Input
            return 0;
        });
    }

    [Fact]
    public void BusyOverlay_is_hidden_until_active_and_swallows_pointer_input_when_active()
    {
        WpfTestHost.Run(() =>
        {
            var under = new Button { Content = "under" };
            var overlay = new BusyOverlay();
            var grid = new Grid { Width = 300, Height = 200 };
            grid.Children.Add(under);
            grid.Children.Add(overlay);
            grid.Measure(new Size(300, 200));
            grid.Arrange(new Rect(0, 0, 300, 200));
            grid.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
            var hit = VisualTreeHelper.HitTest(grid, new Point(150, 100));
            Assert.True(hit?.VisualHit == under || ((DependencyObject?)hit?.VisualHit)?.GetVisualAncestors().Contains(under) == true);

            overlay.IsActive = true;
            overlay.Text = "Memproses";
            grid.UpdateLayout();

            Assert.Equal(Visibility.Visible, overlay.Visibility);
            var blocked = VisualTreeHelper.HitTest(grid, new Point(150, 100));
            Assert.NotNull(blocked);
            Assert.True(IsInside(blocked!.VisualHit, overlay), "pointer input must land on the overlay, not on the content below");
            return 0;
        });
    }

    private static bool IsInside(DependencyObject node, DependencyObject ancestor)
    {
        for (var current = node; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }
}

internal static class VisualAncestorExtensions
{
    public static IEnumerable<DependencyObject> GetVisualAncestors(this DependencyObject node)
    {
        for (var current = VisualTreeHelper.GetParent(node); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            yield return current;
        }
    }
}
