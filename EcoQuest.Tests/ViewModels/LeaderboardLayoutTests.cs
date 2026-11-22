using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
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
/// Measures the real Leaderboard screen (S-09) with the real theme in a shell-like window (sidebar 232/200, page padding 32/24): columns are not clipped,
/// long names are trimmed with a tooltip, and rows (including the gap row and the own row outside the top 20) do not overlap. Sizes are DIP.
/// </summary>
[Collection(WpfCollection.Name)]
public class LeaderboardLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080), (1280, 720)];
    private const string LongName = "pengguna_dengan_nama_yang_sangat_panjang_sekali";

    public enum Case { Normal, LongNames, OutsideTop20 }

    private sealed record Metrics(
        double Width, Rect View, List<Rect> Rows, List<string> Clipped, List<string> Trimmed, List<string?> TrimmedTips, int RowCount, int GapCount, int PillCount);

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

    private static ILeaderboardService Stub(Case c)
    {
        var entries = Enumerable.Range(1, 20)
            .Select(i => new LeaderboardEntryDto(i, c == Case.LongNames && i % 2 == 0 ? LongName : $"user{i}", 100m - i, "Eco Champion", c == Case.Normal && i == 3))
            .ToList();
        if (c == Case.OutsideTop20)
        {
            entries.Add(new LeaderboardEntryDto(57, "dinda", 1.5m, "Eco Starter", true));
        }

        if (c == Case.LongNames)
        {
            entries[4] = new LeaderboardEntryDto(5, LongName, 95m, "Eco Champion", true);
        }

        var service = Substitute.For<ILeaderboardService>();
        service.GetLeaderboardAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LeaderboardEntryDto>>.Success(entries));
        return service;
    }

    private static Metrics Measure(double width, double height, Case c)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var vm = new LeaderboardViewModel(Stub(c), world.Clock, new ResourceUiText());

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(LeaderboardViewModel)) { VisualTree = new FrameworkElementFactory(typeof(LeaderboardView)) },
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

            var view = Descendants(host).OfType<LeaderboardView>().Single();
            var all = Descendants(view).ToList();
            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), scroll), new Size(e.ActualWidth, e.ActualHeight));

            var rowBorders = all.OfType<Border>().Where(b => b.Name == "Row" && b.DataContext is LeaderboardRowViewModel).ToList();
            var gaps = all.OfType<Border>().Where(b => b.DataContext is LeaderboardGapRowViewModel && b.Child is TextBlock).ToList();
            var pills = all.OfType<Border>().Where(b => b.Name == "YouPill" && b.IsVisible).ToList();
            var visible = all.OfType<TextBlock>().Where(t => t.IsVisible && t.ActualWidth > 0).ToList();
            var clipped = visible
                .Where(t => t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None)
                .Where(t => t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => t.Text).ToList();
            var trimmed = visible
                .Where(t => t.TextTrimming != TextTrimming.None && t.Name == "Name" && t.Text.StartsWith("pengguna_"))
                .ToList();

            var ordered = rowBorders.Concat(gaps).Select(Box).OrderBy(r => r.Top).ToList();
            return new Metrics(width, Box(view), ordered, clipped, trimmed.Select(t => t.Text).ToList(),
                trimmed.Select(t => t.ToolTip as string).ToList(), rowBorders.Count, gaps.Count, pills.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [Theory]
    [InlineData(Case.Normal, 20, 0, 1)]
    [InlineData(Case.OutsideTop20, 21, 1, 1)]
    [InlineData(Case.LongNames, 20, 0, 1)]
    public void Columns_are_not_clipped_and_rows_do_not_overlap(Case c, int rows, int gaps, int pills)
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, c)).ToList());

        foreach (var m in all)
        {
            output.WriteLine($"{m.Width}: view {m.View.Width:F0}, rows {m.RowCount}, gaps {m.GapCount}, clipped [{string.Join("; ", m.Clipped)}]");

            Assert.Equal(rows, m.RowCount);
            Assert.Equal(gaps, m.GapCount);
            Assert.Equal(pills, m.PillCount);
            Assert.Empty(m.Clipped);
            Assert.All(m.Rows, r => Assert.True(r.Right <= m.View.Right + 0.5, $"row {r} leaves the page at {m.Width}"));
            for (var i = 1; i < m.Rows.Count; i++)
            {
                Assert.True(m.Rows[i - 1].Bottom <= m.Rows[i].Top + 0.5, $"rows {i - 1} and {i} overlap at {m.Width}");
            }
        }
    }

    [Fact]
    public void Long_names_are_trimmed_and_keep_the_full_name_in_the_tooltip()
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, Case.LongNames)).ToList());

        foreach (var m in all)
        {
            Assert.NotEmpty(m.Trimmed);
            Assert.All(m.TrimmedTips, tip => Assert.Equal(LongName, tip));
        }
    }
}
