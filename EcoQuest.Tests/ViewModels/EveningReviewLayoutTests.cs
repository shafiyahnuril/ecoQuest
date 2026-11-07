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
using EcoQuest.ViewModels.Items;
using EcoQuest.Views;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Measures the real EveningReviewView with the real theme inside a shell-like window (sidebar 232/200, page padding 32/24, scrolling page),
/// filled from the fake services. The window is shown far off-screen because the breakpoint reads the Window width. Sizes are DIP.
/// </summary>
[Collection(WpfCollection.Name)]
public class EveningReviewLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1160, 800), (1161, 800), (1280, 800), (1536, 864), (1920, 1080)];

    private sealed record Metrics(
        double Width, double Height, bool Compact, List<double> ControlColumns, List<double> PickerWidths, List<double> PickerColumns,
        double ViewportHeight, Rect FooterAtTop, Rect FooterAtEnd, double ContentBottomAtEnd, List<string> ClippedText,
        List<string> DefaultAdorners, int Rows, int PrimaryButtons, List<double> RowHeights, double FooterHeight, Alignment Align, Rect OverlayAtEnd);

    private sealed record Alignment(
        List<double> ControlLefts, List<double> ImpactRights, List<double> InfoLefts, List<string> ImpactTexts,
        double StartWidth, double EndWidth, double EndBoxOverflow, double ClearOverflow, double ColumnWidth);

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

    private static Metrics Measure(double width, double height, bool fill = true)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var nav = Substitute.For<INavigationService>();
        var dialogs = Substitute.For<IDialogService>();
        var text = new ResourceUiText();
        var shell = new ShellViewModel(nav, dialogs, text, new ToastViewModel(), world.Session, world.Auth, world.Review);
        var weather = new WeatherSuggestionViewModel(world.Weather, world.Session, world.Clock, text);
        var vm = new EveningReviewViewModel(world.Review, world.Reference, dialogs, text, world.Clock, nav, shell, weather, TimeSpan.Zero);
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();

        if (fill)
        {
            var items = vm.Groups.SelectMany(g => g.Items).ToList();
            ((QuantityItemViewModel)items[0]).Quantity = 12.34m;
            ((QuantityItemViewModel)items[0]).IsExtrasExpanded = true;
            var lamp = (TimeRangeItemViewModel)items[2];
            lamp.StartTime = new TimeOnly(21, 0);
            lamp.EndTime = new TimeOnly(23, 30);
            ((QuantityItemViewModel)items[1]).Quantity = 40m;
            ((QuantityItemViewModel)items[3]).Quantity = 40m;
            ((ToggleItemViewModel)items[4]).IsYes = true;
            ((QuantityItemViewModel)items[5]).Quantity = 3m;
        }

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
            var view = Descendants(host).OfType<EveningReviewView>().Single();
            var controlStyle = (Style)view.Resources["Border.ActivityControl"];
            var rowStyle = (Style)view.Resources["Border.ReviewRow"];
            var all = Descendants(view).ToList();

            var columns = all.OfType<Border>().Where(b => b.Style == controlStyle).ToList();
            var pickers = all.OfType<TimeRangePicker>().ToList();
            var footer = all.OfType<Border>().Single(b => StickyBottom.GetOffset(b) > 0);

            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), scroll), new Size(e.ActualWidth, e.ActualHeight));
            var atTop = Box(footer);

            scroll.ScrollToEnd();
            Settle(window, scroll);
            var atEnd = Box(footer);
            var contentBottom = page.TranslatePoint(new Point(0, page.ActualHeight), scroll).Y;
            vm.IsBusy = true;
            Settle(window, scroll);
            var overlayAtEnd = Box(all.OfType<BusyOverlay>().Single());
            vm.IsBusy = false;
            scroll.ScrollToTop();
            Settle(window, scroll);

            var clipped = all.OfType<TextBlock>()
                .Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None && t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => $"{t.Text} desired={t.DesiredSize.Width:F1} actual={t.ActualWidth:F1}")
                .ToList();

            var adorners = all.OfType<FrameworkElement>()
                .Where(e => e is TextBox or ToggleButton or NumericStepper or TimeRangePicker && Validation.GetErrorTemplate(e) is not null)
                .Select(e => $"{e.GetType().Name} {e.Name}")
                .ToList();

            var primary = (Style)Application.Current.FindResource("Button.Primary");
            var align = MeasureAlignment(all, columns, pickers, scroll);
            return new Metrics(width, height, compact, columns.Select(c => c.ActualWidth).ToList(), pickers.Select(p => p.DesiredSize.Width).ToList(),
                pickers.Select(p => columns.Single(c => Descendants(c).Contains(p)).ActualWidth).ToList(), scroll.ViewportHeight, atTop, atEnd, contentBottom, clipped, adorners,
                all.OfType<Border>().Count(b => b.Style == rowStyle), all.OfType<Button>().Count(b => b.Style == primary),
                all.OfType<Border>().Where(b => b.Style == rowStyle).Select(b => b.ActualHeight).ToList(), footer.ActualHeight, align, overlayAtEnd);
        }
        finally
        {
            window.Close();
        }
    }

    private static Alignment MeasureAlignment(List<DependencyObject> all, List<Border> columns, List<TimeRangePicker> pickers, ScrollViewer scroll)
    {
        double Left(FrameworkElement e) => e.TranslatePoint(new Point(), scroll).X;
        double Right(FrameworkElement e) => Left(e) + e.ActualWidth;

        var impact = (Style)Application.Current.FindResource("Text.Impact");
        var icon = (Style)Application.Current.FindResource("Button.Icon");
        var impactTexts = all.OfType<TextBlock>().Where(t => t.Style == impact).ToList();
        var picker = Assert.Single(pickers);
        var column = columns.Single(c => Descendants(c).Contains(picker));
        var start = (TextBox)picker.FindName("StartBox");
        var end = (TextBox)picker.FindName("EndBox");
        var clear = (Button)picker.FindName("ClearButton");

        return new Alignment(
            columns.Select(Left).ToList(), impactTexts.Select(Right).ToList(),
            all.OfType<Button>().Where(b => b.Style == icon).Select(Left).ToList(), impactTexts.Select(t => t.Text).ToList(),
            start.ActualWidth, end.ActualWidth, Math.Max(0, Right(end) - Right(column)), Math.Max(0, Right(clear) - Right(column)), column.ActualWidth);
    }

    private static void Settle(Window window, ScrollViewer scroll)
    {
        window.UpdateLayout();
        WpfTestHost.Flush();
        window.UpdateLayout();
        scroll.UpdateLayout();
        WpfTestHost.Flush();
    }

    private List<Metrics> Run(bool fill = true) => WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, fill)).ToList());

    private void Log(Metrics m) =>
        output.WriteLine(
            $"{m.Width}x{m.Height} compact={m.Compact}: rows {m.Rows}; control columns {string.Join("/", m.ControlColumns.Select(c => c.ToString("F1")))}; " +
            $"picker desired {string.Join("/", m.PickerWidths.Select(c => c.ToString("F1")))} in {string.Join("/", m.PickerColumns.Select(c => c.ToString("F1")))}; " +
            $"viewport {m.ViewportHeight:F1}; footer top {m.FooterAtTop.Top:F1}..{m.FooterAtTop.Bottom:F1} end {m.FooterAtEnd.Top:F1}..{m.FooterAtEnd.Bottom:F1} " +
            $"(content bottom {m.ContentBottomAtEnd:F1}); clipped {m.ClippedText.Count}; row heights {string.Join("/", m.RowHeights.Select(h => h.ToString("F1")))}; footer h {m.FooterHeight:F1}");

    [Fact]
    public void Control_column_is_270_above_1160_and_220_at_or_below_and_the_time_picker_fits()
    {
        var all = Run();
        all.ForEach(Log);

        foreach (var m in all)
        {
            Assert.Equal(6, m.Rows);
            Assert.All(m.ControlColumns, c => Assert.Equal(m.Compact ? 220 : 270, c, 1));
            var picker = Assert.Single(m.PickerWidths);
            Assert.True(picker <= m.PickerColumns[0] + 0.5, $"picker {picker:F1} overflows its column {m.PickerColumns[0]:F1}");
            if (m.Compact)
            {
                Assert.Equal(213, picker, 1);
            }
        }
    }

    [Fact]
    public void The_footer_stays_inside_the_viewport_and_settles_at_the_end_of_the_content()
    {
        var all = Run();

        foreach (var m in all)
        {
            Assert.True(m.FooterAtTop.Bottom <= m.ViewportHeight + 0.5, $"{m.Width}x{m.Height}: footer bottom {m.FooterAtTop.Bottom:F1} > viewport {m.ViewportHeight:F1}");
            Assert.True(m.FooterAtEnd.Bottom <= m.ViewportHeight + 0.5);
            Assert.True(m.FooterAtEnd.Bottom <= m.ContentBottomAtEnd + 0.5);
        }

        var shortWindow = all.First(m => m.Height == 700);
        Assert.Equal(shortWindow.ViewportHeight - 14, shortWindow.FooterAtTop.Bottom, 1);   // sticky: 14 above the bottom
    }

    [Fact]
    public void No_text_is_clipped_and_only_one_primary_button_exists()
    {
        var all = Run();

        foreach (var m in all)
        {
            Assert.Empty(m.ClippedText);
            Assert.Equal(1, m.PrimaryButtons);
        }
    }

    [Fact]
    public void No_input_can_show_the_default_validation_adorner()
    {
        var all = Run();

        Assert.All(all, m => Assert.True(m.DefaultAdorners.Count == 0, string.Join(", ", m.DefaultAdorners.Distinct())));
    }

    [Fact]
    public void Empty_rows_are_80_high_except_the_time_row_and_the_footer_is_75_high()
    {
        var all = Run(fill: false);
        all.ForEach(Log);

        foreach (var m in all)
        {
            // padding 16 + bubble 46 + border 1 on both sides; the time row is taller because of the Mulai/Selesai labels.
            Assert.All(m.RowHeights.Where((_, i) => i != 2), h => Assert.Equal(80, h, 1));
            Assert.True(m.RowHeights[2] > 80);
            Assert.Equal(75.2, m.FooterHeight, 1);
        }
    }

    [Fact]
    public void Every_row_has_the_same_control_start_impact_right_edge_and_info_position()
    {
        var all = Run();
        all.ForEach(m => output.WriteLine(
            $"{m.Width}x{m.Height}: control left {string.Join("/", m.Align.ControlLefts.Select(x => x.ToString("F1")))}; impact right " +
            $"{string.Join("/", m.Align.ImpactRights.Select(x => x.ToString("F1")))}; info left {string.Join("/", m.Align.InfoLefts.Select(x => x.ToString("F1")))}; " +
            $"impact {string.Join(" | ", m.Align.ImpactTexts)}"));

        foreach (var m in all.Where(m => m.Width is 1536 or 1100))
        {
            Assert.Equal(6, m.Align.ControlLefts.Count);
            Assert.All(m.Align.ControlLefts, x => Assert.Equal(m.Align.ControlLefts[0], x, 1));
            Assert.All(m.Align.ImpactRights, x => Assert.Equal(m.Align.ImpactRights[0], x, 1));
            Assert.All(m.Align.InfoLefts, x => Assert.Equal(m.Align.InfoLefts[0], x, 1));
            Assert.True(m.Align.ImpactTexts.Distinct().Count() > 3, "the rows must have impact texts of different widths");
        }
    }

    [Fact]
    public void The_start_and_end_time_boxes_are_equally_wide_and_stay_inside_the_control_column()
    {
        var all = Run();

        foreach (var m in all)
        {
            Assert.Equal(m.Align.StartWidth, m.Align.EndWidth, 1);
            Assert.Equal(0, m.Align.EndBoxOverflow, 1);
            Assert.Equal(0, m.Align.ClearOverflow, 1);
        }
    }

    [Fact]
    public void The_busy_overlay_covers_the_visible_area_even_when_the_page_is_scrolled()
    {
        var all = Run();

        foreach (var m in all.Where(m => m.ViewportHeight < 800))
        {
            Assert.Equal(0, m.OverlayAtEnd.Top, 1);
            Assert.Equal(m.ViewportHeight, m.OverlayAtEnd.Height, 1);
        }
    }
}
