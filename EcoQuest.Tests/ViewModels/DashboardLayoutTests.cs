using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Measures the real DashboardView with the real theme inside a shell-like window (sidebar 232/200, page padding 32/24, scrolling page),
/// loaded from the fake services. The window is shown far off-screen because the breakpoint reads the Window width. Sizes are DIP.
/// </summary>
[Collection(WpfCollection.Name)]
public class DashboardLayoutTests(ITestOutputHelper output)
{
    private sealed record Metrics(
        double Width, double Height, bool Compact, double ViewWidth, double HeroWidth, double HeroHeight,
        List<Rect> Cards, double TrendWidth, double BreakdownWidth, double BannerHeight, double PageHeadHeight, List<string> Overflow,
        Rect ImageBox, Rect DrawnRect, List<Rect> TextBoxes);

    private static Metrics Measure(double width, double height, FakeScenario scenario = FakeScenario.Normal)
    {
        var world = new FakeWorld(scenario);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var vm = new DashboardViewModel(world.Dashboard, world.Profile, world.Session, world.Clock, Substitute.For<INavigationService>(), new ResourceUiText());
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();   // fakes complete synchronously (delay recorder), so no deadlock

        // Created from a DataTemplate exactly like App.xaml does: names are then not reachable with FindName (template name scope),
        // and ElementName bindings inside the view's styles do not resolve. This is the hosting that exposed the hero defect.
        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(DashboardViewModel)) { VisualTree = new FrameworkElementFactory(typeof(DashboardView)) },
        };
        var compact = width <= 1160;
        var sidebar = new Border { Width = compact ? 200 : 232 };
        var page = new Border { Padding = compact ? new Thickness(24, 30, 24, 45) : new Thickness(32, 30, 32, 45), Child = host };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = page };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(sidebar);
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(scroll);

        var window = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000,
            Width = width, Height = height, Content = grid,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            WpfTestHost.Flush();
            window.UpdateLayout();

            var view = FindVisual<DashboardView>(host)!;
            var hero = (Border)ByName(view, "Hero");
            var layer = (Grid)ByName(view, "HeroLayer");
            var image = ByName(view, "HeroImageBox");
            var drawn = (Image)ByName(view, "HeroImage");
            var stats = (UniformGrid)ByName(view, "Stats");
            var title = ByName(view, "HeroTitle");
            var body = ByName(view, "HeroBody");
            var button = ByName(view, "HeroButton");
            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), hero), new Size(e.ActualWidth, e.ActualHeight));
            var textBoxes = new List<Rect> { Box(title), Box(body), Box(button) };
            var drawnRect = Box(drawn);
            var cards = stats.Children.Cast<MetricCard>().Select(c => new Rect(c.TranslatePoint(new Point(), view), new Size(c.ActualWidth, c.ActualHeight))).ToList();

            // The two chart cards are the first two Border.Card of the first row; find them by their column in the parent Grid.
            var stack = (StackPanel)stats.Parent;
            var rowOne = (Grid)stack.Children[stack.Children.IndexOf(stats) + 1];
            var trend = (FrameworkElement)rowOne.Children[0];
            var breakdown = (FrameworkElement)rowOne.Children[1];

            var banner = (FrameworkElement)((StackPanel)hero.Parent).Children[1];
            var head = (FrameworkElement)((StackPanel)hero.Parent).Children[0];

            var overflow = new List<string>();
            CollectOverflow(view, view, layer, stats, overflow);

            return new Metrics(width, height, compact, view.ActualWidth, hero.ActualWidth, hero.ActualHeight,
                cards, trend.ActualWidth, breakdown.ActualWidth, banner.ActualHeight, head.ActualHeight, overflow,
                Box(image), drawnRect, textBoxes);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Visible elements must stay inside the view horizontally (except the clipped hero illustration and the negative-margin stats grid).</summary>
    private static void CollectOverflow(DependencyObject node, FrameworkElement root, DependencyObject skipLayer, DependencyObject skipStats, List<string> found)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child == skipLayer || child == skipStats)
            {
                if (child == skipStats)
                {
                    CollectOverflow(child, root, skipLayer, skipStats, found);
                }

                continue;
            }

            if (child is FrameworkElement fe && fe.ActualWidth > 0 && fe.IsVisible && child is not ToolTip)
            {
                var left = fe.TranslatePoint(new Point(), root).X;
                if (left < -0.5 || left + fe.ActualWidth > root.ActualWidth + 0.5)
                {
                    found.Add($"{fe.GetType().Name} left={left:F1} right={left + fe.ActualWidth:F1} view={root.ActualWidth:F1}");
                }
            }

            CollectOverflow(child, root, skipLayer, skipStats, found);
        }
    }

    private List<Metrics> Run(params (double W, double H)[] sizes) =>
        WpfTestHost.Run(() => sizes.Select(s => Measure(s.W, s.H)).ToList());

    private void Log(Metrics m)
    {
        output.WriteLine(
            $"{m.Width}x{m.Height} compact={m.Compact}: view {m.ViewWidth:F1}; pagehead h {m.PageHeadHeight:F1}; banner h {m.BannerHeight:F1}; hero {m.HeroWidth:F1}x{m.HeroHeight:F1}; " +
            $"image box {m.ImageBox.Width:F1}x{m.ImageBox.Height:F1} (drawn {m.DrawnRect.Width:F1}); " +
            $"cards {string.Join(" | ", m.Cards.Select(c => $"{c.Width:F1}x{c.Height:F1}@({c.X:F1},{c.Y:F1})"))}; trend {m.TrendWidth:F1}, breakdown {m.BreakdownWidth:F1}; overflow {m.Overflow.Count}");
    }

    [Fact]
    public void Four_metric_cards_sit_in_one_row_above_1160_and_in_a_2x2_grid_at_or_below_1160()
    {
        var all = Run((1100, 700), (1160, 800), (1161, 800), (1280, 800), (1536, 864), (1920, 1080));
        all.ForEach(Log);

        foreach (var m in all)
        {
            Assert.Equal(4, m.Cards.Count);
            Assert.All(m.Cards, c => Assert.True(c.Width > 100 && c.Height > 60));
            if (m.Compact)
            {
                Assert.Equal(m.Cards[0].Y, m.Cards[1].Y, 1);
                Assert.Equal(m.Cards[2].Y, m.Cards[3].Y, 1);
                Assert.True(m.Cards[2].Y > m.Cards[0].Y + m.Cards[0].Height);        // second row below the first
                Assert.Equal(12, m.Cards[2].Y - (m.Cards[0].Y + m.Cards[0].Height), 1);   // .stats gap 12
                Assert.Equal(m.Cards[0].X, m.Cards[2].X, 1);
            }
            else
            {
                Assert.All(m.Cards, c => Assert.Equal(m.Cards[0].Y, c.Y, 1));
                Assert.True(m.Cards[3].X > m.Cards[2].X && m.Cards[2].X > m.Cards[1].X && m.Cards[1].X > m.Cards[0].X);
                Assert.Equal(12, m.Cards[1].X - (m.Cards[0].X + m.Cards[0].Width), 1);   // gap 12
                Assert.Equal(m.Cards[0].Width, m.Cards[3].Width, 1);
            }

            // Cards fill the view edge to edge.
            Assert.Equal(0, m.Cards.Min(c => c.X), 1);
            Assert.Equal(m.ViewWidth, m.Cards.Max(c => c.Right), 1);
        }
    }

    [Fact]
    public void Hero_illustration_sits_on_the_right_and_never_overlaps_the_hero_text()
    {
        var all = Run((1100, 700), (1280, 800), (1536, 864), (1920, 1080), (1161, 800), (1160, 800));
        foreach (var m in all)
        {
            Log(m);
            output.WriteLine(
                $"   hero {m.HeroWidth:F1}x{m.HeroHeight:F1} | box x {m.ImageBox.Left:F1}..{m.ImageBox.Right:F1} y {m.ImageBox.Top:F1}..{m.ImageBox.Bottom:F1} | " +
                $"drawn x {m.DrawnRect.Left:F1}..{m.DrawnRect.Right:F1} | title right {m.TextBoxes[0].Right:F1} body right {m.TextBoxes[1].Right:F1} button right {m.TextBoxes[2].Right:F1}");

            Assert.True(m.HeroHeight >= 302 - 0.5);                              // .hero min-height 302
            var layerWidth = m.HeroWidth - 2;                                     // 1 px border each side
            var layerHeight = m.HeroHeight - 2;

            // Size: width min(52%, 540) or 58% when compact, height 116%.
            Assert.Equal(Math.Min(layerWidth * (m.Compact ? 0.58 : 0.52), 540), m.ImageBox.Width, 1);
            Assert.Equal(1.16 * layerHeight, m.ImageBox.Height, 1);

            // Position (from the hero's outer edge, so +1 for its border): right 1% inside / 5% outside, top -7%.
            Assert.Equal(1 - 0.07 * layerHeight, m.ImageBox.Top, 1);
            Assert.Equal(1 + layerWidth - layerWidth * (m.Compact ? -0.05 : 0.01), m.ImageBox.Right, 1);

            // On the right side of the hero, never on the left half.
            Assert.True(m.ImageBox.Left > m.HeroWidth * 0.25, $"image box starts at {m.ImageBox.Left}");
            Assert.True(m.DrawnRect.Left > m.HeroWidth * 0.40, $"drawn image starts at {m.DrawnRect.Left}");
            Assert.True(m.ImageBox.Top < 0 && m.ImageBox.Bottom > m.HeroHeight);   // sticks out above, cut at the base

            // The square illustration is drawn at min(box width, box height), centred in its box.
            Assert.Equal(Math.Min(m.ImageBox.Width, m.ImageBox.Height), m.DrawnRect.Width, 1);

            // Text: at most 48% of the hero width, and no intersection with the box or the drawn island.
            foreach (var text in m.TextBoxes)
            {
                Assert.True(text.Right <= 0.48 * m.HeroWidth + 2, $"text right edge {text.Right} exceeds 48% of {m.HeroWidth}");
                Assert.False(text.IntersectsWith(m.ImageBox), $"text {text} intersects image box {m.ImageBox}");
                Assert.False(text.IntersectsWith(m.DrawnRect), $"text {text} intersects drawn image {m.DrawnRect}");
            }

            Assert.Empty(m.Overflow);
        }
    }

    [Fact]
    public void Trend_and_breakdown_columns_keep_the_155_to_1_ratio()
    {
        var all = Run((1100, 700), (1280, 800), (1920, 1080));
        all.ForEach(Log);

        foreach (var m in all)
        {
            Assert.Equal(1.55, m.TrendWidth / m.BreakdownWidth, 2);
            Assert.Equal(m.ViewWidth, m.TrendWidth + 16 + m.BreakdownWidth, 1);   // gap 16
        }
    }

    [Fact]
    public void Account_without_reviews_uses_the_same_layout()
    {
        var m = WpfTestHost.Run(() => Measure(1280, 800, FakeScenario.Empty));
        Log(m);

        Assert.Equal(4, m.Cards.Count);
        Assert.Empty(m.Overflow);
    }

    [Fact]
    public void Loading_shows_the_skeleton_and_error_shows_the_banner_with_retry()
    {
        var (loadingHeight, errorBanner, retryText) = WpfTestHost.Run(() =>
        {
            var session = new FakeSessionService();
            session.Start(new UserSession(Guid.NewGuid(), "dinda", "d@x.y", "Yogyakarta", null));
            var clock = new FakeClock(FakeWorld.Now);
            var dashboard = Substitute.For<EcoQuest.Core.Contracts.Services.IDashboardService>();
            var profile = Substitute.For<EcoQuest.Core.Contracts.Services.IProfileService>();
            var gate = new TaskCompletionSource<EcoQuest.Core.Common.Result<DashboardDto>>();
            dashboard.GetDashboardAsync(Arg.Any<EcoQuest.Core.Domain.TrendRange>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
            profile.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<EcoQuest.Core.Common.Result<UserProfileDto>>().Task);
            var vm = new DashboardViewModel(dashboard, profile, session, clock, Substitute.For<INavigationService>(), new ResourceUiText());
            _ = vm.OnNavigatedToAsync(null);   // stays in Loading

            var view = new DashboardView { DataContext = vm };
            var window = new Window { WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -32000, Top = -32000, Width = 1280, Height = 800, Content = view };
            window.Show();
            window.UpdateLayout();
            var loading = view.ActualHeight;

            gate.SetResult(EcoQuest.Core.Common.Result<DashboardDto>.Failure(EcoQuest.Core.Common.ErrorCode.NetworkError, "Tidak dapat terhubung ke server."));
            window.UpdateLayout();
            WpfTestHost.Flush();
            window.UpdateLayout();
            var banner = FindVisual<ErrorBanner>(view);
            var text = banner is null ? null : (string?)banner.RetryText;
            var visible = banner is { IsVisible: true, ActualHeight: > 0 };
            window.Close();
            return (loading, visible, text);
        });

        output.WriteLine($"loading height {loadingHeight:F1}; error banner visible {errorBanner}; retry text '{retryText}'");
        Assert.True(loadingHeight > 700);          // skeleton mirrors the page height
        Assert.True(errorBanner);
        Assert.Equal("Coba lagi", retryText);
    }

    private static FrameworkElement ByName(DependencyObject root, string name) =>
        FindNamed(root, name) ?? throw new InvalidOperationException($"Element '{name}' not found in the visual tree");

    private static FrameworkElement? FindNamed(DependencyObject root, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name)
            {
                return fe;
            }

            if (FindNamed(child, name) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindVisual<T>(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }
}
