using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Sidebar focus markers: the menu list is only a container (no default dotted rectangle, not focusable, not a Tab stop) while every menu item
/// and "Keluar" keep the keyboard ring <c>Focus.Visual.Nav</c> (outline 2, offset 2) inside the sidebar, and Ctrl+1..6 still navigate.
/// </summary>
[Collection(WpfCollection.Name)]
public class SidebarFocusTests
{
    private static (ecoQuest.MainWindow Window, ShellViewModel Shell) Host(double width)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "d@x.y", "Yogyakarta", null));
        var shell = new ShellViewModel(Substitute.For<INavigationService>(), Substitute.For<IDialogService>(), new ResourceUiText(), new ToastViewModel(),
            world.Session, Substitute.For<IAuthService>(), Substitute.For<IEveningReviewService>());
        var window = new ecoQuest.MainWindow { DataContext = shell, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, Width = width, Height = 800 };
        shell.IsAuthenticated = true;
        window.Show();
        window.UpdateLayout();
        return (window, shell);
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

    private static Border SidebarOf(DependencyObject menu)
    {
        DependencyObject? current = menu;
        while (current is not null and not Border)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        return (Border)current!;
    }

    [Fact]
    public void Menu_list_is_a_plain_container_and_items_use_the_token_ring()
    {
        WpfTestHost.Run(() =>
        {
            var (window, _) = Host(1280);
            try
            {
                var menu = (ItemsControl)window.FindName("MenuList");
                var logout = (Button)window.FindName("LogoutButton");
                var ring = Application.Current.FindResource("Focus.Visual.Nav");
                var global = Application.Current.FindResource("Focus.Visual");
                Assert.NotSame(global, ring);

                Assert.Null(menu.FocusVisualStyle);
                Assert.False(menu.Focusable);
                Assert.False(menu.IsTabStop);

                var items = FindAll<Button>(menu).ToList();
                Assert.Equal(6, items.Count);
                Assert.All(items, b =>
                {
                    Assert.Same(ring, b.FocusVisualStyle);
                    Assert.True(b.Focusable);
                    Assert.True(b.IsTabStop);
                });

                Assert.Same(ring, logout.FocusVisualStyle);
                Assert.True(logout.Focusable);
                Assert.True(logout.IsTabStop);

                // Nothing focusable inside the sidebar may fall back to WPF's default dotted rectangle.
                var sidebar = SidebarOf(menu);
                Assert.All(FindAll<Control>(sidebar).Where(c => c.Focusable),
                    c => Assert.True(c.FocusVisualStyle is null || ReferenceEquals(c.FocusVisualStyle, ring), $"{c.GetType().Name} keeps the default focus style"));
            }
            finally
            {
                window.Close();
            }

            return 0;
        });
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(1100)]
    public void Focus_ring_stays_inside_the_sidebar(double width)
    {
        WpfTestHost.Run(() =>
        {
            var (window, _) = Host(width);
            try
            {
                var menu = (ItemsControl)window.FindName("MenuList");
                var logout = (Button)window.FindName("LogoutButton");
                var sidebar = SidebarOf(menu);
                var outer = 3 + Application.Current.FindResource("Stroke.Focus") switch { double d => d / 2, _ => 0 };   // rectangle margin + half stroke

                var bounds = new Rect(0, 0, sidebar.ActualWidth, sidebar.ActualHeight);
                foreach (var button in FindAll<Button>(menu).Append(logout))
                {
                    var box = button.TransformToAncestor(sidebar).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                    box.Inflate(outer, outer);
                    Assert.True(bounds.Contains(box), $"ring of '{button.GetValue(System.Windows.Automation.AutomationProperties.NameProperty)}' leaves the sidebar: {box} vs {bounds}");
                }
            }
            finally
            {
                window.Close();
            }

            return 0;
        });
    }

    [Fact]
    public void Ctrl_1_to_6_navigate_to_the_six_menus_in_order()
    {
        WpfTestHost.Run(() =>
        {
            var (window, shell) = Host(1280);
            try
            {
                var bindings = window.InputBindings.OfType<KeyBinding>().Where(b => b.Key is >= Key.D1 and <= Key.D6).OrderBy(b => b.Key).ToList();
                Assert.Equal(6, bindings.Count);
                Assert.All(bindings, b =>
                {
                    Assert.Equal(ModifierKeys.Control, b.Modifiers);
                    Assert.Same(shell.NavigateCommand, b.Command);
                    Assert.True(shell.NavigateCommand.CanExecute(b.CommandParameter));
                });
                Assert.Equal(
                    [MainMenu.Dashboard, MainMenu.EveningReview, MainMenu.History, MainMenu.Achievements, MainMenu.Leaderboard, MainMenu.Profile],
                    bindings.Select(b => (MainMenu)b.CommandParameter!).ToArray());
            }
            finally
            {
                window.Close();
            }

            return 0;
        });
    }
}
