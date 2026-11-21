using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
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
/// Measures the real Pencapaian screen (S-08) with the real theme in a shell-like window (sidebar 232/200, page padding 32/24): four badge columns,
/// no clipped text and no overlap between the level and streak cards. Sizes are DIP; the window is shown far off-screen.
/// </summary>
[Collection(WpfCollection.Name)]
public class AchievementsLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private sealed record Metrics(
        double Width, Rect View, Rect LevelCard, Rect StreakCard, List<Rect> Cells, List<string> Clipped, int BadgeCount, bool ProgressBarVisible);

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

    private static Metrics Measure(double width, double height, FakeScenario scenario, bool longNames)
    {
        var world = new FakeWorld(scenario);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var gamification = longNames ? LongNames(world) : world.Gamification;
        var vm = new AchievementsViewModel(gamification, world.Profile, world.Clock, new ResourceUiText());

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(AchievementsViewModel)) { VisualTree = new FrameworkElementFactory(typeof(AchievementsView)) },
        };
        var compact = width <= 1160;
        var page = new Border { Padding = compact ? new Thickness(24, 30, 24, 45) : new Thickness(32, 30, 32, 45), Child = host };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = page };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new Border { Width = compact ? 200 : 232 });
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(scroll);

        var window = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000,
            Width = width, Height = height, Content = grid,
        };
        window.Show();
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();
        try
        {
            window.UpdateLayout();
            WpfTestHost.Flush();
            window.UpdateLayout();
            WpfTestHost.Flush();
            window.UpdateLayout();

            var view = Descendants(host).OfType<AchievementsView>().Single();
            var all = Descendants(view).ToList();
            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), scroll), new Size(e.ActualWidth, e.ActualHeight));

            var card = (Style)Application.Current.FindResource("Border.Card");
            var cards = all.OfType<Border>().Where(b => b.Style == card).ToList();
            var cells = cards.Where(b => b.DataContext is BadgeItemViewModel).Select(Box).ToList();
            var summary = cards.Where(b => b.DataContext is AchievementsViewModel).ToList();
            var clipped = all.OfType<TextBlock>()
                .Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None && t.ActualWidth > 0)
                .Where(t => t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => t.Text)
                .ToList();
            var bar = all.OfType<ProgressBar>().FirstOrDefault();

            return new Metrics(width, Box(view), Box(summary[0]), Box(summary[1]), cells, clipped, cells.Count, bar?.IsVisible == true);
        }
        finally
        {
            window.Close();
        }
    }

    private static EcoQuest.Core.Contracts.Services.IGamificationService LongNames(FakeWorld world)
    {
        var service = Substitute.For<EcoQuest.Core.Contracts.Services.IGamificationService>();
        service.GetLevelProgressAsync(Arg.Any<CancellationToken>()).Returns(world.Gamification.GetLevelProgressAsync());
        var badges = Enumerable.Range(1, 8).Select(i => new BadgeDto(
            i, i % 2 == 0 ? "7 Hari Refleksi Berturut-turut Tanpa Putus Sama Sekali" : "Pencatat Rajin",
            "Deskripsi kriteria yang cukup panjang untuk tooltip.", CriteriaType.StreakDays, 7, i % 3 == 0,
            i % 3 == 0 ? new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero) : null, 4)).ToList();
        service.GetBadgesAsync(Arg.Any<CancellationToken>())
            .Returns(EcoQuest.Core.Common.Result<IReadOnlyList<BadgeDto>>.Success(badges));
        return service;
    }

    [Theory]
    [InlineData(FakeScenario.Normal, false)]
    [InlineData(FakeScenario.Empty, false)]
    [InlineData(FakeScenario.Normal, true)]
    public void The_grid_has_four_columns_and_nothing_is_clipped_or_overlapping(FakeScenario scenario, bool longNames)
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, scenario, longNames)).ToList());

        foreach (var m in all)
        {
            output.WriteLine($"{m.Width}: view {m.View.Width:F0}, level {m.LevelCard}, streak {m.StreakCard}, cells {m.Cells.Count} first {m.Cells.FirstOrDefault()} clipped [{string.Join("; ", m.Clipped)}]");

            Assert.Equal(8, m.BadgeCount);
            Assert.Equal(4, m.Cells.Count(c => Math.Abs(c.Top - m.Cells[0].Top) < 0.5));
            Assert.All(m.Cells, c => Assert.True(c.Right <= m.View.Right + 0.5, $"cell {c} leaves the page at {m.Width}"));
            Assert.All(m.Cells, c => Assert.True(c.Width > 150, $"cell {c} is too narrow at {m.Width}"));
            Assert.Empty(m.Clipped);
            Assert.True(m.LevelCard.Right <= m.StreakCard.Left + 0.5, $"level and streak cards overlap at {m.Width}");
            Assert.Equal(m.LevelCard.Top, m.StreakCard.Top, 0.5);
            Assert.True(m.LevelCard.Bottom <= m.Cells.Min(c => c.Top), $"cards overlap the grid at {m.Width}");
            Assert.True(m.ProgressBarVisible);
        }
    }
}
