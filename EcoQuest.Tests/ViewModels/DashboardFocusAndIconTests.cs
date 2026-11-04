using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Controls;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
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
/// Focus markers and banner icon of the real MainWindow hosting the real DashboardView (created from a DataTemplate like App.xaml does).
/// Scroll containers must not draw the default dotted focus rectangle nor be Tab stops, yet stay focusable so the keyboard can scroll them;
/// interactive controls keep the Brand.Primary ring.
/// </summary>
[Collection(WpfCollection.Name)]
public class DashboardFocusAndIconTests(ITestOutputHelper output)
{
    private static (ecoQuest.MainWindow Window, DashboardView View) Host(bool submitted)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "d@x.y", "Yogyakarta", null));
        if (submitted)
        {
            world.Store.SubmitToday([]);
        }

        var dashboard = new DashboardViewModel(world.Dashboard, world.Profile, world.Session, world.Clock, Substitute.For<INavigationService>(), new ResourceUiText());
        dashboard.OnNavigatedToAsync(null).GetAwaiter().GetResult();
        var shell = new ShellViewModel(Substitute.For<INavigationService>(), Substitute.For<IDialogService>(), new ResourceUiText(), new ToastViewModel(),
            world.Session, Substitute.For<IAuthService>(), Substitute.For<IEveningReviewService>());
        var window = new ecoQuest.MainWindow { DataContext = shell, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, Width = 1280, Height = 800 };
        window.Resources.Add(new DataTemplateKey(typeof(DashboardViewModel)),
            new DataTemplate(typeof(DashboardViewModel)) { VisualTree = new FrameworkElementFactory(typeof(DashboardView)) });
        shell.IsAuthenticated = true;
        shell.MainViewModel = dashboard;
        window.Show();
        window.UpdateLayout();
        return (window, Find<DashboardView>(window)!);
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (Find<T>(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var deeper in FindAll<T>(child))
            {
                yield return deeper;
            }
        }
    }

    [Fact]
    public void Scroll_and_content_containers_have_no_default_focus_rectangle_and_are_no_tab_stops()
    {
        WpfTestHost.Run(() =>
        {
            var (window, view) = Host(submitted: false);
            try
            {
                var scroll = (ScrollViewer)window.FindName("PageScroll");
                var main = (ContentControl)window.FindName("MainHost");
                var auth = (ContentControl)window.FindName("AuthHost");
                var stateHost = Find<StateHost>(view)!;
                output.WriteLine($"PageScroll: FVS null={scroll.FocusVisualStyle is null}, tabstop={scroll.IsTabStop}, focusable={scroll.Focusable}; " +
                                 $"MainHost FVS null={main.FocusVisualStyle is null}; view FVS null={view.FocusVisualStyle is null}, tabstop={view.IsTabStop}");

                foreach (var container in new Control[] { scroll, main, auth, view })
                {
                    Assert.Null(container.FocusVisualStyle);
                    Assert.False(container.IsTabStop);
                }

                Assert.True(scroll.Focusable);                 // the keyboard (arrows, PageDown) must keep scrolling it
                Assert.False(stateHost.Focusable);
                Assert.False(stateHost.IsTabStop);

                // Every ScrollViewer reached by the theme (implicit style) behaves the same, wherever it sits.
                Assert.All(FindAll<ScrollViewer>(window), s => Assert.Null(s.FocusVisualStyle));

                // Interactive controls keep the Brand.Primary ring.
                var ring = Application.Current.FindResource("Focus.Visual");
                var buttons = FindAll<Button>(view).Where(b => b.Style is not null && b.Visibility == Visibility.Visible).ToList();
                Assert.NotEmpty(buttons);
                Assert.All(buttons, b => Assert.Same(ring, b.FocusVisualStyle));
            }
            finally
            {
                window.Close();
            }

            return 0;
        });
    }

    [Fact]
    public void Banner_icon_is_the_open_book_while_pending_and_the_check_mark_once_saved()
    {
        var (pending, saved) = WpfTestHost.Run(() =>
        {
            var (w1, v1) = Host(submitted: false);
            var pendingIcon = ((IconView)FindAll<IconView>(v1).First(i => i.Name == "BannerIcon")).Data;
            w1.Close();

            var (w2, v2) = Host(submitted: true);
            var savedIcon = ((IconView)FindAll<IconView>(v2).First(i => i.Name == "BannerIcon")).Data;
            w2.Close();
            return (pendingIcon, savedIcon);
        });

        Assert.Same(WpfTestHost.Run(() => Application.Current.FindResource("Icon.BookOpen")), pending);
        Assert.Same(WpfTestHost.Run(() => Application.Current.FindResource("Icon.Check")), saved);
    }
}
