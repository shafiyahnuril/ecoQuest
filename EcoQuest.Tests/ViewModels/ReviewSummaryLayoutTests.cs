using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using EcoQuest.Controls;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;
using EcoQuest.Views;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Measures the real summary (S-06) with the real theme in a shell-like window (sidebar 232/200, page padding 32/24), filled from the fake services.
/// Sizes are DIP; the window is shown far off-screen because the breakpoint reads the Window width.
/// </summary>
[Collection(WpfCollection.Name)]
public class ReviewSummaryLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private sealed record Metrics(
        double Width, double Height, Rect TotalCard, Rect ContributionCard, Rect ActivitiesCard, Rect WeatherCard, double HeroImageWidth,
        int PrimaryButtons, Rect ReturnButton, double PageRight, List<string> ClippedText, int InputControls, int Rows,
        List<double> IconLefts, List<double> NameLefts, List<double> ImpactRights, List<double> InfoLefts, List<double> RowHeights, int DateCount);

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

    private static Metrics Measure(double width, double height)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var nav = Substitute.For<INavigationService>();
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(true);
        var text = new ResourceUiText();
        var shell = new ShellViewModel(nav, dialogs, text, new ToastViewModel(), world.Session, world.Auth, world.Review);
        var weather = new WeatherSuggestionViewModel(world.Weather, world.Session, world.Clock, text);
        var vm = new EveningReviewViewModel(world.Review, world.Reference, dialogs, text, world.Clock, nav, shell, weather, TimeSpan.Zero);
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();

        var items = vm.Groups.SelectMany(g => g.Items).ToList();
        ((QuantityItemViewModel)items[0]).Quantity = 3m;
        var lamp = (TimeRangeItemViewModel)items[2];
        lamp.StartTime = new TimeOnly(21, 0);
        lamp.EndTime = new TimeOnly(23, 30);
        ((QuantityItemViewModel)items[3]).Quantity = 2m;
        ((ToggleItemViewModel)items[4]).IsYes = true;
        vm.SubmitCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(ReviewMode.Summary, vm.Mode);

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(EveningReviewViewModel)) { VisualTree = new FrameworkElementFactory(typeof(EveningReviewView)) },
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
            Settle(window, scroll);
            var view = Descendants(host).OfType<ReviewSummaryView>().Single();
            var all = Descendants(view).ToList();

            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), scroll), new Size(e.ActualWidth, e.ActualHeight));
            double Left(FrameworkElement e) => e.TranslatePoint(new Point(), scroll).X;
            double Right(FrameworkElement e) => Left(e) + e.ActualWidth;

            var card = (Style)Application.Current.FindResource("Border.Card");
            var cards = all.OfType<Border>().Where(b => b.Style == card).ToList();
            var rowStyle = (Style)view.Resources["Border.SummaryRow"];
            var rows = all.OfType<Border>().Where(b => b.Style == rowStyle).ToList();
            var impact = (Style)Application.Current.FindResource("Text.Impact");
            var icon = (Style)Application.Current.FindResource("Button.Icon");
            var primary = (Style)Application.Current.FindResource("Button.Primary");
            var returnButton = all.OfType<Button>().Single(b => b.Style == primary);
            var image = all.OfType<Image>().Single(i => ReferenceEquals(i.Source, Application.Current.FindResource("Image.SummarySuccess")));

            var clipped = all.OfType<TextBlock>()
                .Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None
                    && t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => $"{t.Text} desired={t.DesiredSize.Width:F1} actual={t.ActualWidth:F1}")
                .ToList();

            return new Metrics(
                width, height, Box(cards[0]), Box(cards[1]), Box(cards[2]), Box(cards[3]), image.ActualWidth,
                all.OfType<Button>().Count(b => b.Style == primary), Box(returnButton), Right(page),
                clipped, all.Count(e => e is TextBox or ToggleButton && e is not Button), rows.Count,
                rows.Select(r => Left(Descendants(r).OfType<IconView>().First())).ToList(),
                rows.Select(r => Left(Descendants(r).OfType<StackPanel>().First())).ToList(),
                all.OfType<TextBlock>().Where(t => t.Style == impact).Select(Right).ToList(),
                all.OfType<Button>().Where(b => b.Style == icon).Select(Left).ToList(),
                rows.Select(r => r.ActualHeight).ToList(),
                Descendants(host).OfType<TextBlock>().Count(t => t.Text == UiFormat.LongDate(world.Clock.Today)));
        }
        finally
        {
            window.Close();
        }
    }

    private static void Settle(Window window, ScrollViewer scroll)
    {
        window.UpdateLayout();
        WpfTestHost.Flush();
        window.UpdateLayout();
        scroll.UpdateLayout();
        WpfTestHost.Flush();
    }

    private List<Metrics> Run()
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H)).ToList());
        all.ForEach(m => output.WriteLine(
            $"{m.Width}x{m.Height}: total card {m.TotalCard.Width:F1}x{m.TotalCard.Height:F1} at ({m.TotalCard.X:F1},{m.TotalCard.Y:F1}); " +
            $"contribution {m.ContributionCard.Width:F1}x{m.ContributionCard.Height:F1} at ({m.ContributionCard.X:F1},{m.ContributionCard.Y:F1}); " +
            $"activities {m.ActivitiesCard.Width:F1}x{m.ActivitiesCard.Height:F1}; weather {m.WeatherCard.Width:F1}x{m.WeatherCard.Height:F1}; " +
            $"hero image {m.HeroImageWidth:F1}; return button {m.ReturnButton.Width:F1}x{m.ReturnButton.Height:F1} right {m.ReturnButton.Right:F1} (page right {m.PageRight:F1}); " +
            $"rows {m.Rows} heights {string.Join("/", m.RowHeights.Select(h => h.ToString("F1")))}; impact rights {string.Join("/", m.ImpactRights.Select(h => h.ToString("F1")))}"));
        return all;
    }

    [Fact]
    public void Total_and_contribution_cards_sit_side_by_side_with_equal_width()
    {
        foreach (var m in Run())
        {
            Assert.Equal(m.TotalCard.Top, m.ContributionCard.Top, 0.5);
            Assert.Equal(m.TotalCard.Height, m.ContributionCard.Height, 0.5);
            Assert.Equal(m.TotalCard.Width, m.ContributionCard.Width, 1);
            Assert.Equal(16, m.ContributionCard.Left - m.TotalCard.Right, 1);
        }
    }

    [Fact]
    public void The_activities_card_is_16_below_the_total_and_contribution_cards()
    {
        foreach (var m in Run())
        {
            Assert.Equal(16, m.ActivitiesCard.Top - m.TotalCard.Bottom, 0.5);
            Assert.Equal(16, m.ActivitiesCard.Top - m.ContributionCard.Bottom, 0.5);
        }
    }

    [Fact]
    public void The_date_appears_once_in_the_page_head_and_not_again_in_the_pill_row()
    {
        Assert.All(Run(), m => Assert.Equal(1, m.DateCount));
    }

    [Fact]
    public void The_hero_illustration_keeps_between_110_and_205_wide()
    {
        foreach (var m in Run())
        {
            Assert.InRange(m.HeroImageWidth, 110, 205.5);
        }
    }

    [Fact]
    public void Nothing_is_clipped_there_is_one_primary_button_and_no_input_controls()
    {
        foreach (var m in Run())
        {
            Assert.Empty(m.ClippedText);
            Assert.Equal(1, m.PrimaryButtons);
            Assert.Equal(0, m.InputControls);
        }
    }

    [Fact]
    public void Activity_rows_line_up_on_icon_name_impact_right_edge_and_info_button()
    {
        foreach (var m in Run())
        {
            Assert.Equal(4, m.Rows);
            Assert.All(m.IconLefts, v => Assert.Equal(m.IconLefts[0], v, 0.5));
            Assert.All(m.NameLefts, v => Assert.Equal(m.NameLefts[0], v, 0.5));
            Assert.All(m.ImpactRights, v => Assert.Equal(m.ImpactRights[0], v, 0.5));
            Assert.All(m.InfoLefts, v => Assert.Equal(m.InfoLefts[0], v, 0.5));
            Assert.True(m.InfoLefts[0] >= m.ImpactRights[0] - 0.5);
        }
    }

    [Fact]
    public void The_return_button_is_at_the_bottom_right_and_meets_the_minimum_size()
    {
        foreach (var m in Run())
        {
            Assert.True(m.ReturnButton.Width >= 205 - 0.5);
            Assert.True(m.ReturnButton.Height >= 46 - 0.5);
            Assert.Equal(m.PageRight - (m.Width <= 1160 ? 24 : 32) - 2, m.ReturnButton.Right, 1);
            Assert.True(m.ReturnButton.Top >= m.WeatherCard.Bottom);
        }
    }
}
