using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
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
/// Measures the real history screen (S-07) with the real theme in a shell-like window (sidebar 232/200, page padding 32/24), filled from the fake
/// services or from 120 generated days. Sizes are DIP; the window is shown far off-screen because the breakpoint reads the Window width.
/// </summary>
[Collection(WpfCollection.Name)]
public class HistoryLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private enum Data { Fake30Days, Generated120Days, Varied, Empty }

    private sealed record Metrics(
        double Width, double Height, Rect FilterCard, List<Rect> PeriodChips, List<Rect> CategoryChips, List<Rect> DatePickers, List<string> DateTexts,
        Rect List, Rect Detail, int Realized, int TotalDays, double PageScrollable, List<Rect> DayCards, List<double> Co2Rights, double LinePitch, double HeaderToFirstLine, double CardPaddingLeft,
        List<string> ClippedText, double LoadMs, bool FirstSelected, string? FirstAutomationName, Brush? SelectedBackground, bool KeyboardSelected,
        Rect EmptyCard, bool EmptyActionVisible, int PrimaryButtons, int ErrorAdorners, Rect DayHeaderCo2, Rect DayHeaderDate, Columns Columns);

    private sealed record Columns(
        List<double> QuantityRights, List<double> ExtrasLefts, List<double> Co2Rights, List<double> PointsRights,
        List<double> HeaderTotalRights, List<double> HeaderPointsRights, List<string> Overflowing, List<double> NameWidths);

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

    private static IHistoryService Generated120(FakeWorld world)
    {
        var service = Substitute.For<IHistoryService>();
        var days = Enumerable.Range(1, 120).Select(i =>
        {
            var items = Enumerable.Range(1, 5).Select(t => new ActivityImpactDto(
                Guid.NewGuid(), t, $"Aktivitas contoh {t}", 1, "Transportasi", InputControlType.Numeric, 3m, "km", null, null,
                0.45m, 9, 0.15m, 1m, "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", "0,45 × 20 × 1,0 = 9 poin", t == 3 ? "Catatan" : null, null)).ToList();
            return new HistoryDayDto(Guid.NewGuid(), world.Clock.Today.AddDays(-i), 2.25m, 45, items);
        }).ToList();
        service.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HistoryDayDto>>.Success(days));
        return service;
    }

    private static ActivityImpactDto Varied(int typeId, InputControlType kind, decimal quantity, string unit, TimeOnly? start, TimeOnly? end, decimal co2, int points) => new(
        Guid.NewGuid(), typeId, $"Aktivitas contoh {typeId}", 1, "Transportasi", kind, quantity, unit, start, end,
        co2, points, 0.15m, 1m, "rumus", "poin", null, null);

    private static IHistoryService GeneratedVaried(FakeWorld world)
    {
        decimal[] co2 = [0.013m, 0.039m, 12.5m, 99.99m];
        int[] points = [1, 22, 125, 999];
        var days = Enumerable.Range(1, 12).Select(i =>
        {
            ActivityImpactDto Line(int t, InputControlType kind, decimal q, string unit, TimeOnly? a = null, TimeOnly? b = null) =>
                Varied(t, kind, q, unit, a, b, co2[(i + t) % 4], points[(i + t) % 4]);
            var items = new[]
            {
                Line(1, InputControlType.Numeric, 1.5m, "km"),
                Line(2, InputControlType.Count, 3m, "kali"),
                Line(3, InputControlType.Toggle, 1m, ""),
                Line(4, InputControlType.TimeRange, i % 2 == 0 ? 3m : 3.5m, "jam", new TimeOnly(22, 10), i % 2 == 0 ? new TimeOnly(1, 10) : new TimeOnly(1, 40)),
            };
            return new HistoryDayDto(Guid.NewGuid(), world.Clock.Today.AddDays(-i), co2[i % 4], points[i % 4], items.Take(2 + i % 3).ToList());
        }).ToList();
        var service = Substitute.For<IHistoryService>();
        service.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<HistoryDayDto>>.Success(days));
        return service;
    }

    private const double SafeMargin = 4;

    /// <summary>
    /// Natural width of a rendered text (a probe with the same style and language, no fixed width) against the width the layout really gave it.
    /// Null when it fits with the safe margin and is not trimmed.
    /// </summary>
    private static string? Misfit(TextBlock t)
    {
        var probe = new TextBlock { Style = t.Style, Text = t.Text, Language = t.Language };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var natural = probe.DesiredSize.Width;
        return natural + SafeMargin > t.Width || t.ActualWidth < t.Width - 0.01 ? $"{t.Text} needs {natural:F1} (+{SafeMargin} margin) has {t.Width:F1}, rendered {t.ActualWidth:F1}" : null;
    }

    private static Columns CollectColumns(IEnumerable<ListBoxItem> cards, Func<FrameworkElement, Rect> box)
    {
        List<double> quantity = [], extras = [], co2 = [], points = [], headerTotal = [], headerPoints = [], names = [];
        List<string> overflowing = [];
        foreach (var card in cards)
        {
            var grids = Descendants(card).OfType<Grid>().Where(g => g.ColumnDefinitions.Count > 0).ToList();
            var header = grids[0];
            foreach (var t in Descendants(header).OfType<TextBlock>().Where(t => ReferenceEquals(VisualTreeHelper.GetParent(t), header)))
            {
                (Grid.GetColumn(t) == 1 ? headerTotal : Grid.GetColumn(t) == 2 ? headerPoints : []).Add(box(t).Right);
                if (Grid.GetColumn(t) == 2 && Misfit(t) is { } misfit)
                {
                    overflowing.Add(misfit);
                }
            }

            foreach (var row in grids.Skip(1))
            {
                foreach (var t in Descendants(row).OfType<TextBlock>().Where(t => ReferenceEquals(VisualTreeHelper.GetParent(t), row)))
                {
                    var column = Grid.GetColumn(t);
                    (column == 1 ? names : column == 2 ? quantity : column == 4 ? co2 : column == 5 ? points : []).Add(column == 1 ? box(t).Width : box(t).Right);
                    if (column >= 2 && Misfit(t) is { } misfit)
                    {
                        overflowing.Add(misfit);
                    }
                }

                extras.Add(box(Descendants(row).OfType<StackPanel>().First()).Left);
            }
        }

        return new Columns(quantity, extras, co2, points, headerTotal, headerPoints, overflowing, names);
    }

    private static Metrics Measure(double width, double height, Data data)
    {
        var world = new FakeWorld(data == Data.Empty ? FakeScenario.Empty : FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var nav = Substitute.For<INavigationService>();
        IHistoryService history = data switch { Data.Generated120Days => Generated120(world), Data.Varied => GeneratedVaried(world), _ => world.History };
        var vm = new HistoryViewModel(history, world.Reference, new FakePhotoStorageService(), world.Clock, nav, new ResourceUiText(), TimeSpan.Zero);

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(HistoryViewModel)) { VisualTree = new FrameworkElementFactory(typeof(HistoryView)) },
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
        var watch = Stopwatch.StartNew();
        window.Show();
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();
        try
        {
            Settle(window, scroll);
            watch.Stop();
            var view = Descendants(host).OfType<HistoryView>().Single();
            var all = Descendants(view).ToList();

            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), scroll), new Size(e.ActualWidth, e.ActualHeight));

            var card = (Style)Application.Current.FindResource("Border.Card");
            var segmented = (Style)Application.Current.FindResource("RadioButton.Segmented");
            var chip = (Style)Application.Current.FindResource("ToggleButton.Chip");
            var radios = all.OfType<RadioButton>().Where(r => r.Style == segmented).ToList();
            var filterCard = all.OfType<Border>().First(b => b.Style == card);
            var pickers = all.OfType<DatePicker>().ToList();
            var list = all.OfType<ListBox>().FirstOrDefault();
            var detail = all.OfType<Border>().FirstOrDefault(b => b.Style == (Style)view.Resources["Border.HistoryDetail"]);
            var items = all.OfType<ListBoxItem>().Where(i => i.IsVisible).OrderBy(i => Box(i).Top).ToList();
            var impact = (Style)Application.Current.FindResource("Text.Impact");
            var lineStyle = (Style)view.Resources["Text.HistoryLine"];
            var primary = (Style)Application.Current.FindResource("Button.Primary");

            var firstItem = items.FirstOrDefault();
            var firstTexts = firstItem is null ? [] : Descendants(firstItem).OfType<TextBlock>().ToList();
            var firstLineCo2 = firstTexts.Where(t => t.Style == (Style)view.Resources["Text.HistoryAmount"]).ToList();
            var firstName = firstTexts.FirstOrDefault(t => t.Style == lineStyle);
            var headerCo2 = firstTexts.FirstOrDefault(t => t.Style == impact);
            var headerDate = firstTexts.FirstOrDefault();

            var firstSelected = vm.SelectedDay is not null && items.FirstOrDefault()?.IsSelected == true;
            var selectedBackground = items.FirstOrDefault()?.Template.FindName("Bd", items.First()) is Border bd ? bd.Background : null;
            var firstBorder = firstItem is null ? null : Descendants(firstItem).OfType<Border>().First();
            var lineRows = firstItem is null ? [] : Descendants(firstItem).OfType<Grid>().Where(g => g.MinHeight > 0).OrderBy(g => Box(g).Top).ToList();
            var linePitch = lineRows.Count > 1 ? Box(lineRows[1]).Top - Box(lineRows[0]).Top : 0;
            var headerToFirstLine = lineRows.Count > 0 && firstTexts.Count > 0 ? Box(lineRows[0]).Top - Box(firstTexts[0]).Top : 0;
            var keyboardSelected = false;
            if (items.Count > 1 && PresentationSource.FromVisual(items[1]) is { } source)
            {
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
                items[1].RaiseEvent(key);
                keyboardSelected = items[1].IsSelected && key.Handled;
            }

            var clipped = all.OfType<TextBlock>()
                .Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None
                    && t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => $"{t.Text} desired={t.DesiredSize.Width:F1} actual={t.ActualWidth:F1}")
                .ToList();

            var emptyBorder = all.OfType<Border>().Where(b => b.Style == card).Skip(1).FirstOrDefault();
            var emptyButton = all.OfType<Button>().FirstOrDefault(b => b.Command is not null && b.Content is string);

            return new Metrics(
                width, height, Box(filterCard),
                radios.Take(3).Select(Box).ToList(),
                radios.Skip(3).Select(Box).Concat(all.OfType<ToggleButton>().Where(t => t.Style == chip).Select(Box)).ToList(),
                pickers.Select(Box).ToList(), pickers.Select(p => p.Text).ToList(),
                list is null ? default : Box(list), detail is null ? default : Box(detail),
                items.Count, vm.Days.Count, scroll.ScrollableHeight,
                items.Select(Box).ToList(), firstLineCo2.Select(t => Box(t).Right).ToList(),
                linePitch, headerToFirstLine, firstBorder is null || firstName is null ? 0 : Box(firstName).Left - Box(firstBorder).Left,
                clipped, watch.Elapsed.TotalMilliseconds, firstSelected,
                firstItem is null ? null : AutomationProperties.GetName(firstItem),
                selectedBackground,
                keyboardSelected, emptyBorder is null ? default : Box(emptyBorder), emptyButton?.IsVisible == true,
                all.OfType<Button>().Count(b => b.Style == primary),
                all.OfType<Control>().Count(c => Validation.GetErrorTemplate(c) is not null && Validation.GetHasError(c)),
                headerCo2 is null ? default : Box(headerCo2), headerDate is null ? default : Box(headerDate), CollectColumns(items, Box));
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
        window.UpdateLayout();
    }

    private List<Metrics> Run(Data data)
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, data)).ToList());
        all.ForEach(m => output.WriteLine(
            $"{data} {m.Width}x{m.Height}: filter card {m.FilterCard.Width:F1}x{m.FilterCard.Height:F1} at ({m.FilterCard.X:F1},{m.FilterCard.Y:F1}); " +
            $"period chips {string.Join("/", m.PeriodChips.Select(c => $"{c.Width:F1}x{c.Height:F1}"))}; category chips {string.Join("/", m.CategoryChips.Select(c => $"{c.Width:F1}x{c.Height:F1}"))}; " +
            $"date pickers {string.Join("/", m.DatePickers.Select(c => $"{c.Width:F1}x{c.Height:F1}"))} text {string.Join("|", m.DateTexts)}; " +
            $"list {m.List.Width:F1}x{m.List.Height:F1} at ({m.List.X:F1},{m.List.Y:F1}); detail {m.Detail.Width:F1}x{m.Detail.Height:F1} at ({m.Detail.X:F1},{m.Detail.Y:F1}); " +
            $"realized {m.Realized}/{m.TotalDays}; page scrollable {m.PageScrollable:F1}; first card {(m.DayCards.Count > 0 ? $"{m.DayCards[0].Width:F1}x{m.DayCards[0].Height:F1}" : "-")}; " +
            $"name column {(m.Columns.NameWidths.Count > 0 ? m.Columns.NameWidths.Min() : 0):F1}; line pitch {m.LinePitch:F1}; header-to-first-line {m.HeaderToFirstLine:F1}; name inset {m.CardPaddingLeft:F1}; " +
            $"empty card {m.EmptyCard.Width:F1}x{m.EmptyCard.Height:F1}; load+layout {m.LoadMs:F0} ms"));
        return all;
    }

    [Fact]
    public void Filter_card_chips_pickers_and_columns_have_the_reference_sizes()
    {
        foreach (var m in Run(Data.Fake30Days))
        {
            Assert.Equal(3, m.PeriodChips.Count);
            Assert.All(m.PeriodChips, c => Assert.InRange(c.Height, 30, 35));
            Assert.Equal(m.PeriodChips[0].Top, m.PeriodChips[1].Top, 0.5);
            Assert.Equal(7, m.PeriodChips[1].Left - m.PeriodChips[0].Right - 0, 1.1);
            Assert.Equal(5, m.CategoryChips.Count);
            Assert.All(m.CategoryChips, c => Assert.InRange(c.Height, 30, 35));
            Assert.Equal(2, m.DatePickers.Count);
            Assert.All(m.DatePickers, p => Assert.Equal(34, p.Height, 0.5));
            Assert.Equal(330, m.Detail.Width, 0.5);
            Assert.Equal(15, m.Detail.Left - m.List.Right, 0.5);
            Assert.Equal(m.List.Top, m.Detail.Top, 0.5);
            Assert.Equal(15, m.List.Top - m.FilterCard.Bottom, 0.5);
        }
    }

    [Fact]
    public void Right_hand_columns_line_up_across_every_card_and_the_header()
    {
        foreach (var m in Run(Data.Varied))
        {
            var c = m.Columns;
            Assert.True(m.DayCards.Count >= 3, $"cards {m.DayCards.Count}");
            Assert.Empty(c.Overflowing);
            foreach (var (name, values) in new[] { ("quantity", c.QuantityRights), ("extras", c.ExtrasLefts), ("co2", c.Co2Rights), ("points", c.PointsRights), ("header total", c.HeaderTotalRights), ("header points", c.HeaderPointsRights) })
            {
                Assert.True(values.Count >= 3, $"{m.Width}: {name} has {values.Count} values");
                Assert.True(values.All(v => Math.Abs(v - values[0]) < 0.5), $"{m.Width}: {name} edges differ: {string.Join(", ", values.Distinct().Select(v => v.ToString("F1")))}");
            }

            Assert.Equal(c.PointsRights[0], c.HeaderPointsRights[0], 0.5);
            Assert.Equal(c.Co2Rights[0], c.HeaderTotalRights[0], 0.5);
        }
    }

    [Fact]
    public void Dates_use_the_indonesian_day_first_format()
    {
        foreach (var m in Run(Data.Fake30Days))
        {
            Assert.Equal(["26/09/2026", "25/10/2026"], m.DateTexts);
        }
    }

    [Fact]
    public void Day_cards_have_10_gap_13_padding_and_aligned_columns()
    {
        foreach (var m in Run(Data.Fake30Days))
        {
            Assert.True(m.DayCards.Count >= 3, $"cards {m.DayCards.Count}");
            Assert.Equal(10, m.DayCards[1].Top - m.DayCards[0].Bottom, 0.5);
            Assert.Equal(m.List.Left, m.DayCards[0].Left, 0.5);
            Assert.True(m.Co2Rights.Count >= 2, $"co2 texts {m.Co2Rights.Count}");
            Assert.All(m.Co2Rights, r => Assert.Equal(m.Co2Rights[0], r, 0.5));
            Assert.Equal(29, m.LinePitch, 0.5);
            Assert.Equal(1 + 13 + 15 + 6, m.CardPaddingLeft, 0.5);
            Assert.Empty(m.ClippedText);
        }
    }

    [Fact]
    public void The_page_does_not_scroll_and_nothing_is_cut_at_every_size()
    {
        foreach (var m in Run(Data.Fake30Days))
        {
            Assert.Equal(0, m.PageScrollable, 1);
            Assert.True(m.List.Bottom <= m.Height + 0.5, $"list bottom {m.List.Bottom} beyond window {m.Height}");
            Assert.True(m.Detail.Bottom <= m.Height + 0.5);
            Assert.Equal(0, m.ErrorAdorners);
        }
    }

    [Fact]
    public void First_day_is_selected_with_the_reference_highlight_and_a_spoken_name()
    {
        var expected = (Brush)WpfTestHost.Run(() => Application.Current.FindResource("History.DayHoverBg"));
        foreach (var m in Run(Data.Fake30Days))
        {
            Assert.True(m.FirstSelected, "first item not selected");
            Assert.Equal(expected.ToString(), m.SelectedBackground?.ToString());
            Assert.Contains("total", m.FirstAutomationName);
            Assert.True(m.KeyboardSelected, "Enter did not select");
        }
    }

    [Fact]
    public void A_list_of_120_days_virtualizes_and_stays_inside_the_viewport()
    {
        foreach (var m in Run(Data.Generated120Days))
        {
            Assert.Equal(120, m.TotalDays);
            Assert.InRange(m.Realized, 1, 40);
            Assert.Equal(0, m.PageScrollable, 1);
            Assert.True(m.List.Bottom <= m.Height + 0.5);
            Assert.True(m.LoadMs < 2500, $"{m.LoadMs} ms");
        }
    }

    [Fact]
    public void Empty_state_replaces_the_list_with_one_secondary_action_below_the_filters()
    {
        foreach (var m in Run(Data.Empty))
        {
            Assert.Equal(0, m.TotalDays);
            Assert.True(m.EmptyActionVisible);
            Assert.Equal(0, m.PrimaryButtons);
            Assert.Equal(15, m.EmptyCard.Top - m.FilterCard.Bottom, 0.5);
        }
    }
}
