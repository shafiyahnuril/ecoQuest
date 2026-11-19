using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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

/// <summary>Measures the insight panel and weather card of the real DashboardView (second card row) in a shell-like window. Sizes are DIP.</summary>
[Collection(WpfCollection.Name)]
public class DashboardInsightWeatherLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private sealed record Metrics(
        double Width, Rect InsightCard, Rect WeatherCard, List<double> RowHeights, List<double> BubbleLefts, List<double> BubbleWidths,
        double WeatherIconBox, double ForecastContentHeight, List<string> Clipped, string CardName, int RowCount, bool HasDeveloperText,
        double SubtitleBottom, double FirstRowTop, double LastRowBottom);

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

    private static Metrics Measure(double width, double height, FakeScenario scenario, bool fallback)
    {
        var world = new FakeWorld(scenario);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var text = new ResourceUiText();
        IWeatherSuggestionService weatherService = world.Weather;
        if (fallback)
        {
            weatherService = Substitute.For<IWeatherSuggestionService>();
            weatherService.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(new WeatherSuggestionDto(
                "Yogyakarta", world.Clock.Today.AddDays(1), true, null, null, text.Get("Weather.FallbackAdvice"), null, null));
        }

        var weather = new WeatherSuggestionViewModel(weatherService, world.Session, world.Clock, text);
        var vm = new DashboardViewModel(world.Dashboard, world.Profile, world.Session, world.Clock, Substitute.For<INavigationService>(), text, weather);
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();
        weather.LoadTask.GetAwaiter().GetResult();

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
            scroll.UpdateLayout();

            var view = Descendants(host).OfType<DashboardView>().Single();
            var insight = Descendants(view).OfType<InsightPanel>().Single();
            var card = Descendants(view).OfType<WeatherSuggestionCard>().Single();
            var all = Descendants(view).ToList();

            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), view), new Size(e.ActualWidth, e.ActualHeight));
            var rowStyle = (Style)insight.Resources["Border.InsightRow"];
            var subtitle = Descendants(insight).OfType<TextBlock>().First(t => t.Text == vm.InsightSubtitle);
            var rows = Descendants(insight).OfType<Border>().Where(b => b.Style == rowStyle).ToList();
            var bubbles = rows.Select(r => Descendants(r).OfType<Ellipse>().First()).ToList();
            var contentStyle = (Style)card.Resources["Border.ForecastContent"];
            var forecastContent = Descendants(card).OfType<Border>().First(b => b.Style == contentStyle && b.IsVisible);
            var iconBox = Descendants(card).OfType<Border>().First(b => b.Width == 60);

            var clipped = Descendants(view).OfType<TextBlock>()
                .Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None
                    && t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => $"{t.Text} desired={t.DesiredSize.Width:F1} actual={t.ActualWidth:F1}")
                .ToList();
            var developerText = Descendants(view).OfType<TextBlock>().Any(t => t.Text.Contains("AccuWeather melalui backend") || t.Text.Contains("contoh kartu"));

            return new Metrics(
                width, Box(insight), Box(card), rows.Select(r => r.ActualHeight).ToList(),
                bubbles.Select(b => b.TranslatePoint(new Point(), view).X).ToList(), bubbles.Select(b => b.ActualWidth).ToList(),
                iconBox.ActualWidth, forecastContent.ActualHeight, clipped,
                AutomationProperties.GetName(card), rows.Count, developerText,
                Box(subtitle).Bottom, Box(rows[0]).Top, Box(rows[^1]).Bottom);
        }
        finally
        {
            window.Close();
        }
    }

    private List<Metrics> Run(FakeScenario scenario, bool fallback = false)
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, scenario, fallback)).ToList());
        all.ForEach(m => output.WriteLine(
            $"{m.Width} {scenario} gap subtitle->first row {m.FirstRowTop - m.SubtitleBottom:F1}, last row to card bottom {m.InsightCard.Bottom - m.LastRowBottom:F1}; " +
            $"{m.Width} {scenario} fallback={fallback}: insight {m.InsightCard.Width:F1}x{m.InsightCard.Height:F1}@({m.InsightCard.X:F1},{m.InsightCard.Y:F1}); " +
            $"weather {m.WeatherCard.Width:F1}x{m.WeatherCard.Height:F1}@({m.WeatherCard.X:F1},{m.WeatherCard.Y:F1}); rows {m.RowCount} heights {string.Join("/", m.RowHeights.Select(h => h.ToString("F1")))}; " +
            $"bubble lefts {string.Join("/", m.BubbleLefts.Select(h => h.ToString("F1")))} widths {string.Join("/", m.BubbleWidths.Select(h => h.ToString("F1")))}; " +
            $"icon box {m.WeatherIconBox:F1}; forecast content h {m.ForecastContentHeight:F1}; name '{m.CardName}'"));
        return all;
    }

    [Fact]
    public void Insight_and_weather_cards_sit_side_by_side_with_equal_width_and_a_16_gap()
    {
        foreach (var m in Run(FakeScenario.Normal))
        {
            Assert.Equal(m.InsightCard.Top, m.WeatherCard.Top, 0.5);
            Assert.Equal(m.InsightCard.Height, m.WeatherCard.Height, 0.5);
            Assert.Equal(m.InsightCard.Width, m.WeatherCard.Width, 1);
            Assert.Equal(16, m.WeatherCard.Left - m.InsightCard.Right, 1);
        }
    }

    [Fact]
    public void Insight_rows_are_at_least_75_tall_and_their_icon_bubbles_line_up()
    {
        foreach (var m in Run(FakeScenario.Normal).Concat(Run(FakeScenario.Empty)))
        {
            Assert.InRange(m.RowCount, 1, DashboardViewModel.MaxInsights + 1);
            Assert.All(m.RowHeights, h => Assert.True(h >= 75 - 0.5, $"row height {h}"));
            Assert.All(m.BubbleWidths, w => Assert.Equal(37, w, 0.5));
            Assert.All(m.BubbleLefts, left => Assert.Equal(m.BubbleLefts[0], left, 0.5));
        }
    }

    [Fact]
    public void The_insight_list_is_pushed_to_the_card_bottom_as_in_the_reference_margin_top_auto()
    {
        const double cardPadding = 23;   // padding 22 + border 1
        foreach (var m in Run(FakeScenario.Empty).Concat(Run(FakeScenario.Normal)))
        {
            Assert.Equal(m.InsightCard.Bottom - cardPadding, m.LastRowBottom, 1);
            Assert.True(m.FirstRowTop - m.SubtitleBottom >= 16 - 0.5, $"gap {m.FirstRowTop - m.SubtitleBottom}");
        }
    }

    [Fact]
    public void The_empty_account_shows_the_invitation_and_the_habit_tip_as_two_rows()
    {
        Assert.All(Run(FakeScenario.Empty), m => Assert.Equal(2, m.RowCount));
    }

    [Fact]
    public void The_weather_card_keeps_the_60_icon_box_and_the_96_forecast_row_in_both_states()
    {
        foreach (var m in Run(FakeScenario.Normal).Concat(Run(FakeScenario.Normal, fallback: true)))
        {
            Assert.Equal(60, m.WeatherIconBox, 0.5);
            Assert.True(m.ForecastContentHeight >= 96 - 0.5, $"forecast row {m.ForecastContentHeight}");
        }
    }

    [Fact]
    public void The_weather_card_name_summarises_the_forecast_or_says_Saran_umum()
    {
        Assert.All(Run(FakeScenario.Normal), m => Assert.StartsWith("Besok di Yogyakarta", m.CardName));
        Assert.All(Run(FakeScenario.Normal, fallback: true), m => Assert.Equal("Saran umum", m.CardName));
    }

    [Fact]
    public void Nothing_is_clipped_and_no_developer_text_is_shown()
    {
        foreach (var m in Run(FakeScenario.Normal, fallback: true).Concat(Run(FakeScenario.Empty)))
        {
            Assert.Empty(m.Clipped);
            Assert.False(m.HasDeveloperText);
        }
    }
}
