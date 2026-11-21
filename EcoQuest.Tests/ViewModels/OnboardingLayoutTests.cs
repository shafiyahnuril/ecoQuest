using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>Measures the real OnboardingView with the real theme at several window sizes (DIP), for every step.</summary>
[Collection(WpfCollection.Name)]
public class OnboardingLayoutTests(ITestOutputHelper output)
{
    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private sealed record Metrics(
        double Width, double Height, int Step, Rect Card, Rect Title, Rect Body, Rect Dots, Rect Skip, Rect Next, double ImageHeight,
        string SkipText, string NextText, string TitleText, bool TitleIsHeading, string DotsName, List<int> ActiveDots);

    private static Rect Bounds(FrameworkElement element, UIElement relativeTo) =>
        new(element.TranslatePoint(new Point(0, 0), relativeTo), new Size(element.ActualWidth, element.ActualHeight));

    private static Metrics Measure(double width, double height, int step)
    {
        var vm = new OnboardingViewModel(
            Substitute.For<INavigationService>(), Substitute.For<ISessionService>(), Substitute.For<IOnboardingStore>(), new ResourceUiText())
        {
            StepIndex = step,
        };
        var view = new OnboardingView { DataContext = vm };
        for (var i = 0; i < 2; i++)
        {
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
        }

        var title = (TextBlock)view.FindName("TitleText");
        var skip = (Button)view.FindName("SkipButton");
        var next = (Button)view.FindName("NextButton");
        var dots = (ItemsControl)view.FindName("StepDots");
        return new Metrics(
            width, height, step, Bounds((FrameworkElement)view.FindName("Card"), view), Bounds(title, view), Bounds((FrameworkElement)view.FindName("BodyHost"), view),
            Bounds(dots, view), Bounds(skip, view), Bounds(next, view), ((Image)view.FindName("Illustration")).ActualHeight,
            (string)skip.Content, (string)next.Content, title.Text, AutomationProperties.GetHeadingLevel(title) == AutomationHeadingLevel.Level1,
            AutomationProperties.GetName(dots), ActiveDotIndexes(dots));
    }

    private static IEnumerable<Ellipse> Ellipses(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Ellipse ellipse)
            {
                yield return ellipse;
            }

            foreach (var nested in Ellipses(child))
            {
                yield return nested;
            }
        }
    }

    // A dot is "active" when it is really painted with Brand.Primary (a local Fill once hid the trigger).
    private static List<int> ActiveDotIndexes(DependencyObject dots)
    {
        var primary = ((SolidColorBrush)Application.Current.FindResource("Brand.Primary")).Color;
        var inactive = ((SolidColorBrush)Application.Current.FindResource("Onboard.DotInactive")).Color;
        var colors = Ellipses(dots).Select(e => ((SolidColorBrush)e.Fill).Color).ToList();
        Assert.All(colors, c => Assert.True(c == primary || c == inactive));
        return colors.Select((c, i) => (c, i)).Where(x => x.c == primary).Select(x => x.i).ToList();
    }

    private List<Metrics> MeasureAll()
    {
        var all = WpfTestHost.Run(() => Sizes.SelectMany(s => Enumerable.Range(0, 3).Select(step => Measure(s.W, s.H, step))).ToList());
        foreach (var m in all)
        {
            output.WriteLine(
                $"{m.Width}x{m.Height} step {m.Step + 1}: card {m.Card.Width:F1}x{m.Card.Height:F1} at ({m.Card.Left:F1},{m.Card.Top:F1}), title {m.Title.Width:F1}x{m.Title.Height:F1}, " +
                $"body {m.Body.Width:F1}x{m.Body.Height:F1}, skip {m.Skip.Width:F1}x{m.Skip.Height:F1}, next {m.Next.Width:F1}x{m.Next.Height:F1}, image h {m.ImageHeight:F1}");
        }

        return all;
    }

    [Fact]
    public void Card_is_at_most_590_wide_and_centred_in_both_directions()
    {
        foreach (var m in MeasureAll())
        {
            Assert.True(m.Card.Width <= 590 + 0.5, $"card {m.Card.Width}");
            Assert.Equal((m.Width - m.Card.Width) / 2, m.Card.Left, 1);
            Assert.Equal(m.Height / 2, m.Card.Top + (m.Card.Height / 2), 1);
            Assert.True(m.Card.Top >= 0 && m.Card.Bottom <= m.Height, $"card {m.Card.Top}-{m.Card.Bottom} in {m.Height}");
        }
    }

    [Fact]
    public void Text_is_not_clipped_and_stays_inside_the_card()
    {
        foreach (var m in MeasureAll())
        {
            Assert.True(m.Title.Left >= m.Card.Left && m.Title.Right <= m.Card.Right + 0.5, $"title {m.Title}");
            Assert.True(m.Body.Left >= m.Card.Left && m.Body.Right <= m.Card.Right + 0.5, $"body {m.Body}");
            Assert.True(m.Title.Height < 50, $"title wrapped: {m.Title.Height} ({m.TitleText})");
            Assert.True(m.Body.Height < 60, $"body too tall: {m.Body.Height}");
            Assert.True(m.Body.Bottom <= m.Dots.Top + 0.5);
            Assert.True(m.Dots.Bottom <= m.Skip.Top + 0.5 && m.Dots.Bottom <= m.Next.Top + 0.5);
        }
    }

    [Fact]
    public void Both_buttons_are_visible_inside_the_card_and_the_image_is_285_high()
    {
        foreach (var m in MeasureAll())
        {
            Assert.True(m.Skip.Width > 0 && m.Skip.Height >= 43 - 0.5, $"skip {m.Skip}");
            Assert.True(m.Next.Width > 0 && m.Next.Height >= 43 - 0.5, $"next {m.Next}");
            Assert.True(m.Skip.Left >= m.Card.Left && m.Next.Right <= m.Card.Right + 0.5);
            Assert.True(m.Skip.Bottom <= m.Card.Bottom && m.Next.Bottom <= m.Card.Bottom);
            Assert.True(m.Skip.Right <= m.Next.Left, "buttons overlap");
            Assert.Equal(285, m.ImageHeight, 1);
        }
    }

    [Fact]
    public void Labels_follow_the_step_and_the_title_is_a_heading_with_a_step_label_on_the_dots()
    {
        foreach (var m in MeasureAll())
        {
            Assert.Equal("Lewati", m.SkipText);
            Assert.Equal(m.Step == 2 ? "Mulai" : "Lanjut", m.NextText);
            Assert.True(m.TitleIsHeading);
            Assert.Equal($"Langkah {m.Step + 1} dari 3", m.DotsName);
        }
    }

    [Fact]
    public void Exactly_the_dot_of_the_current_step_is_painted_active()
    {
        foreach (var m in MeasureAll())
        {
            Assert.Equal([m.Step], m.ActiveDots);
        }
    }

    [Fact]
    public void Card_title_dots_and_buttons_do_not_move_between_steps()
    {
        var all = MeasureAll();
        foreach (var size in all.GroupBy(m => (m.Width, m.Height)))
        {
            var first = size.First();
            foreach (var m in size)
            {
                Assert.Equal(first.Card, m.Card);
                Assert.Equal(first.Title.Top, m.Title.Top);
                Assert.Equal(first.Body, m.Body);
                Assert.Equal(first.Dots.Top, m.Dots.Top);
                Assert.Equal(first.Skip.Top, m.Skip.Top);
                Assert.Equal(first.Next.Top, m.Next.Top);
            }
        }
    }
}
