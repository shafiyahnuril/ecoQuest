using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Controls;
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
/// Measures the detail panel of the history screen (S-07) in DIP: where the info icon sits, how the title row is sized and the vertical rhythm
/// between the parts. The first day has a wrapping title with a note, then lines without note or photo, with and without a description.
/// </summary>
[Collection(WpfCollection.Name)]
public class HistoryDetailLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private const string LongTitle = "Menggunakan transportasi umum atau berjalan kaki untuk seluruh perjalanan harian";

    private sealed record Item(
        Rect Title, Rect TitleRow, Rect? Icon, double FirstLineHeight, Rect Quantity, Rect Impact, Rect Formula, Rect Border,
        bool HasNote, bool HasPhoto, Rect? Note, Rect? Photo, double ContentRight);

    private sealed record Panel(double Width, Rect Date, Rect Total, Rect Points, Rect FirstBorder, List<Item> Items);

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

    private static ActivityImpactDto Line(int typeId, string name, string? note = null) => new(
        Guid.NewGuid(), typeId, name, 1, "Transportasi", InputControlType.Numeric, 3m, "km", null, null,
        0.45m, 9, 0.15m, 1m, "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", "0,45 × 20 × 1,0 = 9 poin", note, null);

    private static Panel Measure(double width, double height)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));

        var day = new HistoryDayDto(Guid.NewGuid(), world.Clock.Today.AddDays(-1), 1.8m, 36,
            [Line(1, LongTitle, "Catatan singkat"), Line(2, "Naik sepeda"), Line(3, "Memilah sampah"), Line(4, "Hemat listrik")]);
        var history = Substitute.For<IHistoryService>();
        history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<HistoryDayDto>>.Success([day]));

        var types = new[] { 1, 2, 4 }.Select(id => new ActivityTypeDto(
            id, $"Tipe {id}", 1, "Transportasi", InputControlType.Numeric, "km", 0.15m, 1m, null, 50m, $"Deskripsi {id}.", id)).ToList();
        var reference = Substitute.For<IReferenceDataService>();
        reference.GetCategoriesAsync(Arg.Any<CancellationToken>()).Returns(_ => world.Reference.GetCategoriesAsync());
        reference.GetActivityTypesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<ActivityTypeDto>>.Success(types));

        var vm = new HistoryViewModel(history, reference, new FakePhotoStorageService(), Substitute.For<IDialogService>(), world.Clock, Substitute.For<INavigationService>(), new ResourceUiText(), TimeSpan.Zero);
        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(HistoryViewModel)) { VisualTree = new FrameworkElementFactory(typeof(HistoryView)) },
        };
        var compact = width <= 1160;
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Padding = compact ? new Thickness(24, 30, 24, 45) : new Thickness(32, 30, 32, 45), Child = host },
        };
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

            var view = Descendants(host).OfType<HistoryView>().Single();
            var detail = Descendants(view).OfType<Border>().Single(b => b.Style == (Style)view.Resources["Border.HistoryDetail"]);
            var outer = Descendants(detail).OfType<StackPanel>().First();
            Rect Box(FrameworkElement e) => new(e.TranslatePoint(new Point(), outer), new Size(e.ActualWidth, e.ActualHeight));
            var texts = outer.Children.OfType<TextBlock>().ToList();
            var list = outer.Children.OfType<ItemsControl>().Single();
            var titleStyle = (Style)view.Resources["Text.HistoryDetailTitle"];

            var items = new List<Item>();
            foreach (var border in Descendants(list).OfType<Border>().Where(b => b.BorderThickness.Top == 1))
            {
                var stack = (StackPanel)border.Child;
                var row = (Grid)stack.Children[0];
                var title = row.Children.OfType<TextBlock>().Single();
                Assert.Same(titleStyle, title.Style);
                var button = row.Children.OfType<Button>().Single();
                var icon = (IconView)button.Content;
                var singleLine = new TextBlock { Text = "X", Style = titleStyle };
                singleLine.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var note = stack.Children[4] as FrameworkElement;
                var photo = stack.Children[5] as FrameworkElement;
                items.Add(new Item(
                    Box(title), Box(row), button.IsVisible ? Box(icon) : null, singleLine.DesiredSize.Height,
                    Box((FrameworkElement)stack.Children[1]), Box((FrameworkElement)stack.Children[2]), Box((FrameworkElement)stack.Children[3]), Box(border),
                    note!.IsVisible, photo!.IsVisible, note.IsVisible ? Box(note) : null, photo.IsVisible ? Box(photo) : null, outer.ActualWidth));
            }

            return new Panel(detail.ActualWidth, Box(texts[1]), Box(texts[2]), Box(texts[3]), items[0].Border, items);
        }
        finally
        {
            window.Close();
        }
    }

    private List<Panel> Run()
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H)).ToList());
        for (var i = 0; i < all.Count; i++)
        {
            var p = all[i];
            output.WriteLine($"{Sizes[i].W}x{Sizes[i].H}: panel {p.Width:F1}; date {p.Date.Height:F1}; total {p.Total.Height:F1} (date-to-total {p.Total.Top - p.Date.Bottom:F1}); " +
                $"points {p.Points.Height:F1} (total-to-points {p.Points.Top - p.Total.Bottom:F1}, points-to-line {p.FirstBorder.Top - p.Points.Bottom:F1}); " +
                string.Join("; ", p.Items.Select((it, n) =>
                    $"item{n}: title {it.Title.Height:F1} row {it.TitleRow.Height:F1} icon {(it.Icon is { } r ? $"right {r.Right:F1} centerY {r.Top + r.Height / 2:F1}" : "-")} " +
                    $"gaps title-qty {it.Quantity.Top - it.TitleRow.Bottom:F1} qty-kg {it.Impact.Top - it.Quantity.Bottom:F1} kg-formula {it.Formula.Top - it.Impact.Bottom:F1} formula {it.Formula.Height:F1} formula-end {it.Border.Bottom - it.Formula.Bottom:F1}")));
        }

        return all;
    }

    private static double Token(string key, Func<Thickness, double> side) => WpfTestHost.Run(() => side((Thickness)Application.Current.FindResource(key)));

    [Fact]
    public void Info_icon_sits_on_the_right_edge_and_the_center_of_the_first_title_line()
    {
        foreach (var p in Run())
        {
            var withIcon = p.Items.Where(i => i.Icon is not null).ToList();
            Assert.Equal(3, withIcon.Count);
            Assert.Contains(p.Items, i => i.Title.Height > i.FirstLineHeight * 1.5);
            foreach (var i in withIcon)
            {
                Assert.Equal(i.ContentRight, i.Icon!.Value.Right, 0.5);
                Assert.Equal(withIcon[0].Icon!.Value.Right, i.Icon!.Value.Right, 0.5);
                Assert.Equal(i.Title.Top + i.FirstLineHeight / 2, i.Icon.Value.Top + i.Icon.Value.Height / 2, 1);
            }
        }
    }

    [Fact]
    public void Title_row_is_exactly_as_tall_as_the_title_text()
    {
        foreach (var p in Run())
        {
            Assert.All(p.Items, i =>
            {
                Assert.Equal(i.Title.Height, i.TitleRow.Height, 0.5);
                Assert.Equal(i.Title.Height / i.FirstLineHeight, Math.Round(i.Title.Height / i.FirstLineHeight), 0.02);
            });
        }
    }

    [Fact]
    public void Parts_are_spaced_by_the_theme_tokens()
    {
        var quantity = Token("Margin.HistoryDetailQuantity", t => t.Top);
        var impact = Token("Margin.HistoryDetailImpact", t => t.Top);
        var formula = Token("Margin.HistoryDetailFormula", t => t.Top);
        var lineHeight = WpfTestHost.Run(() => (double)Application.Current.FindResource("Height.HistoryDetailFormulaLine"));
        var itemEnd = Token("Padding.HistoryDetailItem", t => t.Bottom);
        var totalTop = Token("Margin.HistoryDetailTotal", t => t.Top);
        var pointsTop = Token("Margin.HistoryDetailPoints", t => t.Top);
        var pointsBottom = Token("Margin.HistoryDetailPoints", t => t.Bottom);

        foreach (var p in Run())
        {
            Assert.Equal(totalTop, p.Total.Top - p.Date.Bottom, 0.5);
            Assert.Equal(pointsTop, p.Points.Top - p.Total.Bottom, 0.5);
            Assert.Equal(pointsBottom, p.FirstBorder.Top - p.Points.Bottom, 0.5);
            Assert.All(p.Items, i =>
            {
                Assert.Equal(quantity, i.Quantity.Top - i.TitleRow.Bottom, 0.5);
                Assert.Equal(impact, i.Impact.Top - i.Quantity.Bottom, 0.5);
                Assert.Equal(formula, i.Formula.Top - i.Impact.Bottom, 0.5);
                Assert.Equal(2 * lineHeight, i.Formula.Height, 0.5);
            });
        }
    }

    [Fact]
    public void Missing_note_and_photo_leave_no_space()
    {
        var itemEnd = Token("Padding.HistoryDetailItem", t => t.Bottom);
        foreach (var p in Run())
        {
            var bare = p.Items.Where(i => !i.HasNote && !i.HasPhoto).ToList();
            Assert.Equal(3, bare.Count);
            Assert.All(bare, i => Assert.Equal(itemEnd, i.Border.Bottom - i.Formula.Bottom, 0.5));
        }
    }
}
