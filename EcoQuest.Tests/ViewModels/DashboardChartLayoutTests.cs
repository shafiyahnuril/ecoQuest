using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using LiveChartsCore.SkiaSharpView.WPF;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Sizes (DIP) of the trend card, donut and legend inside the real DashboardView at the four reference window sizes.
/// Only the boxes are measured: the LiveCharts canvases are never rendered here.
/// </summary>
[Collection(WpfCollection.Name)]
public class DashboardChartLayoutTests(ITestOutputHelper output)
{
    private sealed record ChartMetrics(
        double ChartHeight, Size Donut, Size Pie, int LegendRows, double DotSize, List<double> PercentRightEdges, double LegendRight,
        int Chips, double ChipHeight, bool EmptyShown);

    private static ChartMetrics Measure(double width, double height, FakeScenario scenario)
    {
        var world = new FakeWorld(scenario);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var vm = new DashboardViewModel(world.Dashboard, world.Profile, world.Session, world.Clock, Substitute.For<INavigationService>(), new ResourceUiText());
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(DashboardViewModel)) { VisualTree = new FrameworkElementFactory(typeof(DashboardView)) },
        };
        var compact = width <= 1160;
        var page = new Border { Padding = compact ? new Thickness(24, 30, 24, 45) : new Thickness(32, 30, 32, 45), Child = host };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = page };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 200 : 232) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
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

            var view = Descendants<DashboardView>(host).Single();
            var chart = Descendants<CartesianChart>(view).Single();
            var pie = Descendants<PieChart>(view).Single();
            var donutBox = (Grid)pie.Parent;
            var legend = Descendants<ItemsControl>(view).First(i => i.ItemsSource is IReadOnlyList<CategoryLegendItem>);
            var rows = Descendants<Grid>(legend).Where(g => g.DataContext is CategoryLegendItem).ToList();
            var percents = rows.Select(r => r.Children.OfType<TextBlock>().Last()).ToList();
            var chips = Descendants<RadioButton>(view).ToList();
            var emptyPanel = Descendants<StackPanel>(view).Single(p => p.MinHeight == 165);

            double Right(FrameworkElement e) => e.TranslatePoint(new Point(e.ActualWidth, 0), view).X;
            return new ChartMetrics(
                chart.ActualHeight, new Size(donutBox.ActualWidth, donutBox.ActualHeight), new Size(pie.ActualWidth, pie.ActualHeight),
                rows.Count, rows.Count == 0 ? 0 : rows[0].Children.OfType<Ellipse>().First().ActualWidth,
                percents.Select(Right).ToList(), Right(legend),
                chips.Count, chips.Count == 0 ? 0 : chips[0].ActualHeight, emptyPanel.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    [Fact]
    public void Filled_dashboard_has_a_200_high_trend_chart_a_150_donut_and_a_right_aligned_percentage_column()
    {
        var results = WpfTestHost.Run(() => Sizes.Select(s => (s, m: Measure(s.W, s.H, FakeScenario.Normal))).ToList());

        foreach (var (size, m) in results)
        {
            output.WriteLine($"{size.W}x{size.H}: chart h {m.ChartHeight:F1}; donut {m.Donut.Width:F1}x{m.Donut.Height:F1}; pie {m.Pie.Width:F1}x{m.Pie.Height:F1}; " +
                             $"legend rows {m.LegendRows}, dot {m.DotSize:F1}; percent right edges {string.Join("/", m.PercentRightEdges.Select(e => e.ToString("F1")))}; legend right {m.LegendRight:F1}; " +
                             $"chips {m.Chips} h {m.ChipHeight:F1}");
            Assert.Equal(200, m.ChartHeight, 1);
            Assert.Equal(150, m.Donut.Width, 1);
            Assert.Equal(150, m.Donut.Height, 1);
            Assert.Equal(150, m.Pie.Width, 1);
            Assert.Equal(150, m.Pie.Height, 1);
            Assert.Equal(4, m.LegendRows);
            Assert.Equal(9, m.DotSize, 1);
            Assert.Equal(2, m.Chips);
            Assert.InRange(m.ChipHeight, 28, 32.5);   // .chip: 7+7 padding, 1+1 border, 12 px text
            Assert.All(m.PercentRightEdges, e => Assert.Equal(m.PercentRightEdges[0], e, 1));
            Assert.True(m.PercentRightEdges[0] <= m.LegendRight + 0.5);
            Assert.False(m.EmptyShown);
        }
    }

    [Fact]
    public void Empty_dashboard_shows_the_placeholder_instead_of_the_charts_and_keeps_the_150_ring()
    {
        var results = WpfTestHost.Run(() => Sizes.Select(s => (s, m: Measure(s.W, s.H, FakeScenario.Empty))).ToList());

        foreach (var (size, m) in results)
        {
            output.WriteLine($"{size.W}x{size.H}: empty placeholder shown {m.EmptyShown}; donut box {m.Donut.Width:F1}x{m.Donut.Height:F1}; legend rows {m.LegendRows}");
            Assert.True(m.EmptyShown);
            Assert.Equal(150, m.Donut.Width, 1);
            Assert.Equal(4, m.LegendRows);
        }
    }
}
