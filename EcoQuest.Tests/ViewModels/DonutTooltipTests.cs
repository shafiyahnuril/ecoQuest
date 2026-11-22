using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.WPF;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>The donut hides the LiveCharts tooltip (the 150 DIP canvas clips it); explanations live on the legend rows and the center label (FR-4.5, UIR-53).</summary>
[Collection(WpfCollection.Name)]
public class DonutTooltipTests
{
    private static T WithDashboard<T>(Func<DashboardViewModel, PieChart, StackPanel, T> act) => WpfTestHost.Run(() =>
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var vm = new DashboardViewModel(world.Dashboard, world.Profile, world.Session, world.Clock, Substitute.For<INavigationService>(), new ResourceUiText(),
            new WeatherSuggestionViewModel(world.Weather, world.Session, world.Clock, new ResourceUiText()));
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(DashboardViewModel)) { VisualTree = new FrameworkElementFactory(typeof(DashboardView)) },
        };
        var window = new Window
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000, Width = 1280, Height = 800,
            ShowActivated = false, Content = host,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            WpfTestHost.Flush();
            var pie = Descendants<PieChart>(host).Single();
            var center = Descendants<StackPanel>(((Grid)pie.Parent)).First(p => p.Parent == pie.Parent);
            return act(vm, pie, center);
        }
        finally
        {
            window.Close();
        }
    });

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

    [Fact]
    public void The_donut_hides_the_livecharts_tooltip_and_has_no_wpf_tooltip()
    {
        var (position, tip) = WithDashboard((_, pie, _) => (pie.TooltipPosition, pie.ToolTip));

        Assert.Equal(TooltipPosition.Hidden, position);
        Assert.Null(tip);
    }

    [Fact]
    public void Each_legend_row_shows_its_category_explanation_as_tooltip()
    {
        var (pairs, count) = WithDashboard((vm, pie, _) =>
        {
            var root = (DependencyObject)Window.GetWindow(pie)!;
            var rows = Descendants<Grid>(root).Where(g => g.DataContext is CategoryLegendItem).ToList();
            return (rows.Select(r => (((CategoryLegendItem)r.DataContext).Explanation, Shown: (r.ToolTip as ToolTip)?.Content is TextBlock t ? t.Text : null)).ToList(), vm.BreakdownLegend.Count);
        });

        Assert.Equal(count, pairs.Count);
        Assert.All(pairs, p =>
        {
            Assert.NotEqual("", p.Explanation);
            Assert.Equal(p.Explanation, p.Shown);
        });
    }

    [Fact]
    public void The_center_label_keeps_its_own_tooltip_and_can_be_hovered()
    {
        var (text, expected, hit) = WithDashboard((vm, _, center) =>
            (((ToolTip)center.ToolTip).Content is TextBlock t ? t.Text : "", vm.TotalCo2Explanation, center.IsHitTestVisible));

        Assert.NotEqual("", expected);
        Assert.Equal(expected, text);
        Assert.True(hit);
    }
}
