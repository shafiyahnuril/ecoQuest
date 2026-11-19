using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Infrastructure;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>Measures the real ConnectionErrorView with the real theme at several window sizes (DIP).</summary>
[Collection(WpfCollection.Name)]
public class ConnectionErrorLayoutTests(ITestOutputHelper output)
{
    private sealed record Metrics(
        double Width, double Height, double ImageWidth, double ImageHeight, double CardWidth, double CardLeft, double CardTop, double CardBottom,
        double TitleWidth, double TitleHeight, double MessageWidth, double ContentWidth, int VisibleButtons, bool HintVisible, bool TitleIsHeading);

    private static readonly AppConfig NetworkConfig = new(null, null, null, UseFakes: true);
    private static readonly AppConfig MissingKeysConfig = new(null, null, null, UseFakes: false);

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var deeper in FindAll<T>(child))
            {
                yield return deeper;
            }
        }
    }

    private static Metrics Measure(AppConfig config, double width, double height)
    {
        var view = new ConnectionErrorView
        {
            DataContext = new ConnectionErrorViewModel(config, Substitute.For<INavigationService>(), new ResourceUiText()),
        };
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();

        var image = (Image)view.FindName("Illustration");
        var card = (Border)view.FindName("Card");
        var title = (TextBlock)view.FindName("TitleText");
        var message = (TextBlock)view.FindName("MessageText");
        var hint = (TextBlock)view.FindName("HintText");
        var content = (StackPanel)view.FindName("CardContent");
        var cardOrigin = card.TranslatePoint(new Point(0, 0), view);

        return new Metrics(
            width, height, image.ActualWidth, image.ActualHeight, card.ActualWidth, cardOrigin.X, cardOrigin.Y, cardOrigin.Y + card.ActualHeight,
            title.ActualWidth, title.ActualHeight, message.ActualWidth, content.ActualWidth,
            FindAll<Button>(view).Count(b => b.Visibility == Visibility.Visible && b.ActualHeight > 0), hint.ActualHeight > 0,
            AutomationProperties.GetHeadingLevel(title) == AutomationHeadingLevel.Level1);
    }

    private static readonly (double W, double H)[] Sizes = [(1100, 700), (1280, 800), (1536, 864), (1920, 1080)];

    private List<Metrics> MeasureAll(AppConfig config)
    {
        var all = WpfTestHost.Run(() => Sizes.Select(s => Measure(config, s.W, s.H)).ToList());
        foreach (var m in all)
        {
            output.WriteLine(
                $"{m.Width}x{m.Height}: image {m.ImageWidth:F1}x{m.ImageHeight:F1}, card {m.CardWidth:F1} at ({m.CardLeft:F1},{m.CardTop:F1}) bottom {m.CardBottom:F1}, " +
                $"title {m.TitleWidth:F1}x{m.TitleHeight:F1}, message {m.MessageWidth:F1}, content {m.ContentWidth:F1}, buttons {m.VisibleButtons}, hint {m.HintVisible}");
        }

        return all;
    }

    [Fact]
    public void Network_variant_is_a_centred_card_with_a_320_image_and_one_primary_button()
    {
        foreach (var m in MeasureAll(NetworkConfig))
        {
            Assert.True(m.ImageWidth <= 320 + 0.5, $"image {m.ImageWidth}");
            Assert.Equal(300, m.ImageHeight, 1);
            Assert.True(m.CardWidth <= 606 + 0.5);
            Assert.Equal((m.Width - m.CardWidth) / 2, m.CardLeft, 1);
            Assert.Equal(m.Height / 2, (m.CardTop + m.CardBottom) / 2, 1);
            Assert.Equal(1, m.VisibleButtons);
            Assert.False(m.HintVisible);
            Assert.True(m.TitleIsHeading);
        }
    }

    [Fact]
    public void Text_is_not_clipped_at_any_size()
    {
        foreach (var m in MeasureAll(NetworkConfig).Concat(MeasureAll(MissingKeysConfig)))
        {
            Assert.True(m.CardTop >= 0 && m.CardBottom <= m.Height, $"card {m.CardTop}-{m.CardBottom} in {m.Height}");
            Assert.True(m.TitleWidth <= m.ContentWidth + 0.5);
            Assert.True(m.MessageWidth <= m.ContentWidth + 0.5);
            Assert.True(m.TitleHeight < 50, $"title wrapped: {m.TitleHeight}");
        }
    }

    [Fact]
    public void Configuration_variant_has_no_button_and_shows_the_hint()
    {
        foreach (var m in MeasureAll(MissingKeysConfig))
        {
            Assert.Equal(0, m.VisibleButtons);
            Assert.True(m.HintVisible);
        }
    }

    [Fact]
    public void Connection_error_screen_is_shown_without_sidebar_while_a_session_is_active()
    {
        WpfTestHost.Run(() =>
        {
            var world = new FakeWorld(FakeScenario.Normal);
            world.Session.Start(new UserSession(Guid.NewGuid(), "dinda", "d@x.y", "Yogyakarta", null));
            var services = new ServiceCollection()
                .AddSingleton(NetworkConfig)
                .AddSingleton<IUiText, ResourceUiText>()
                .AddSingleton<INavigationService>(provider => new NavigationService(provider))
                .AddTransient<ConnectionErrorViewModel>();
            var navigation = services.BuildServiceProvider().GetRequiredService<INavigationService>();
            var shell = new ShellViewModel(navigation, Substitute.For<IDialogService>(), new ResourceUiText(), new ToastViewModel(),
                world.Session, Substitute.For<IAuthService>(), Substitute.For<IEveningReviewService>());
            var window = new ecoQuest.MainWindow { DataContext = shell, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, Width = 1280, Height = 800 };
            window.Show();

            var navigated = navigation.NavigateTo<ConnectionErrorViewModel>();
            window.UpdateLayout();

            Assert.True(navigated.IsCompletedSuccessfully);
            Assert.False(shell.IsAuthenticated);
            Assert.False(((FrameworkElement)window.FindName("MenuList")).IsVisible);
            Assert.IsType<ConnectionErrorViewModel>(((ContentControl)window.FindName("AuthHost")).Content);
            Assert.Null(((ContentControl)window.FindName("MainHost")).Content);
            window.Close();
            return true;
        });
    }
}
