using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

/// <summary>Photo column of the Evening Review (UIR-38) with the real theme: a row with a thumbnail and a row with a photo error, at the four supported sizes (DIP).</summary>
[Collection(WpfCollection.Name)]
public class EveningReviewPhotoLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private sealed record Metrics(
        double Width, double RowRight, List<double> PhotoButtonRights, double Thumb, bool ErrorVisible, bool ErrorIsLive,
        List<string> RemoveNames, List<string> ClippedText, int ThumbnailsWithImage);

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

    private static string WritePng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ecoquest-layout-{Guid.NewGuid():N}.png");
        var pixels = new byte[64 * 64 * 4];
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4)));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static Metrics Measure(double width, double height, string png)
    {
        var world = new FakeWorld(FakeScenario.Normal);
        world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var nav = Substitute.For<INavigationService>();
        var dialogs = Substitute.For<IDialogService>();
        var text = new ResourceUiText();
        var shell = new ShellViewModel(nav, dialogs, text, new ToastViewModel(), world.Session, world.Auth, world.Review);
        var weather = new WeatherSuggestionViewModel(world.Weather, world.Session, world.Clock, text);
        var vm = new EveningReviewViewModel(world.Review, world.Reference, dialogs, new FakePhotoStorageService(), text, world.Clock, nav, shell, weather, TimeSpan.Zero);
        vm.OnNavigatedToAsync(null).GetAwaiter().GetResult();

        var items = vm.Groups.SelectMany(g => g.Items).Cast<QuantityItemViewModel>().Take(2).ToList();
        dialogs.PickImageFile().Returns(png, @"C:\fotos\animasi.gif");
        foreach (var item in items)
        {
            item.Quantity = 5m;
            item.IsExtrasExpanded = true;
        }

        items[0].PickPhotoCommand.Execute(null);
        items[1].PickPhotoCommand.Execute(null);   // .gif is rejected by the real validator rules of the fake service

        var host = new ContentControl
        {
            Content = vm,
            ContentTemplate = new DataTemplate(typeof(EveningReviewViewModel)) { VisualTree = new FrameworkElementFactory(typeof(EveningReviewView)) },
        };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = host };
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000,
            Width = width, Height = height, Content = scroll,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            WpfTestHost.Flush();
            window.UpdateLayout();

            var view = Descendants(host).OfType<EveningReviewView>().Single();
            var all = Descendants(view).ToList();
            var rowStyle = (Style)view.Resources["Border.ReviewRow"];
            var rows = all.OfType<Border>().Where(b => b.Style == rowStyle).Take(2).ToList();

            double Right(FrameworkElement e) => e.TranslatePoint(new Point(e.ActualWidth, 0), scroll).X;
            var firstRow = Descendants(rows[0]).ToList();
            var photoButtons = firstRow.OfType<Button>().Where(b => b.IsVisible && b.Content is not TextBlock { Text.Length: 0 }
                && (b.Command == items[0].PickPhotoCommand || b.Command == items[0].RemovePhotoCommand || b.Command == items[0].PreviewPhotoCommand)).ToList();
            var thumb = firstRow.OfType<Border>().Single(b => b.IsVisible && b.Child is Image);
            var error = Descendants(rows[1]).OfType<TextBlock>().Single(t => t.Text == items[1].PhotoMessageText);
            var thumbnails = firstRow.OfType<Image>().Count(i => i.IsVisible && i.Source is not null);

            var clipped = all.OfType<TextBlock>()
                .Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None && t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.ActualWidth + 0.5)
                .Select(t => t.Text)
                .ToList();

            return new Metrics(
                width, Right(rows[0]), photoButtons.Select(Right).ToList(), thumb.ActualWidth, error.IsVisible,
                AutomationProperties.GetLiveSetting(error) == AutomationLiveSetting.Assertive,
                firstRow.OfType<Button>().Select(AutomationProperties.GetName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList(), clipped, thumbnails);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_thumbnail_row_and_the_error_row_fit_without_clipping_at_every_size()
    {
        var png = WritePng();
        try
        {
            var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(s.W, s.H, png)).ToList());

            foreach (var m in all)
            {
                output.WriteLine($"{m.Width}: row right {m.RowRight:F1}; photo buttons {string.Join("/", m.PhotoButtonRights.Select(r => r.ToString("F1")))}; thumb {m.Thumb:F1}");
                Assert.Equal(3, m.PhotoButtonRights.Count);
                Assert.All(m.PhotoButtonRights, r => Assert.True(r <= m.RowRight + 0.5, $"{m.Width}: a photo button ends at {r:F1} past the row edge {m.RowRight:F1}"));
                Assert.Equal(48, m.Thumb, 1);
                Assert.Equal(1, m.ThumbnailsWithImage);
                Assert.True(m.ErrorVisible);
                Assert.True(m.ErrorIsLive);
                Assert.Contains("Hapus foto", m.RemoveNames);
                Assert.Empty(m.ClippedText);
            }
        }
        finally
        {
            File.Delete(png);
        }
    }
}
