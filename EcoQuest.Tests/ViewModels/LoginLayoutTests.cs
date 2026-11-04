using System.Windows;
using System.Windows.Controls;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>
/// Measures the real LoginView with the real theme at several window sizes (DIP). Runs on the shared WPF host (one Application per process).
/// </summary>
[Collection(WpfCollection.Name)]
public class LoginLayoutTests(ITestOutputHelper output)
{
    private sealed record Metrics(double Width, double Height, double Page, double VisualColumn, double FormColumn, double Card, double Input, double PageLeft);

    private static Metrics Measure(double width, double height)
    {
        var view = new LoginView
        {
            DataContext = new LoginViewModel(Substitute.For<IAuthService>(), Substitute.For<INavigationService>()),
        };
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();

        var page = (Grid)view.FindName("PageRoot");
        var card = (StackPanel)view.FindName("FormCard");
        var input = (TextBox)view.FindName("IdentifierBox");
        return new Metrics(
            width, height, page.ActualWidth,
            page.ColumnDefinitions[0].ActualWidth, page.ColumnDefinitions[1].ActualWidth,
            card.ActualWidth, input.ActualWidth,
            page.TranslatePoint(new Point(0, 0), view).X);
    }

    private static List<Metrics> RunOnSta(params (double W, double H)[] sizes) =>
        WpfTestHost.Run(() => sizes.Select(s => Measure(s.W, s.H)).ToList());
    [Fact]
    public void Page_fills_the_window_up_to_1560_and_the_card_reaches_490()
    {
        var all = RunOnSta((1100, 700), (1280, 800), (1536, 864), (1920, 1080));
        foreach (var m in all)
        {
            output.WriteLine($"{m.Width}x{m.Height}: page {m.Page:F1} (left {m.PageLeft:F1}), visual col {m.VisualColumn:F1}, form col {m.FormColumn:F1}, card {m.Card:F1}, input {m.Input:F1}");
        }

        var small = all[0];
        var normal = all[1];
        var wide = all[2];
        var huge = all[3];

        // No pale bands: the page takes the whole width while it is <= 1560.
        Assert.Equal(1100, small.Page, 1);
        Assert.Equal(1280, normal.Page, 1);
        Assert.Equal(1536, wide.Page, 1);

        // Beyond 1560 it stops growing and is centred.
        Assert.Equal(1560, huge.Page, 1);
        Assert.Equal(180, huge.PageLeft, 1);

        // Columns 1* / 0.94*, the form column never below 460.
        Assert.Equal(1280 * 0.94 / 1.94, normal.FormColumn, 1);
        Assert.Equal(1536 * 0.94 / 1.94, wide.FormColumn, 1);
        Assert.True(small.FormColumn >= 460);

        // The card is 490 whenever the column minus its 2 x 51 padding allows it.
        Assert.Equal(490, normal.Card, 1);
        Assert.Equal(490, wide.Card, 1);
        Assert.Equal(490, huge.Card, 1);
        Assert.True(small.Card < 490 && small.Card > 400);
        Assert.Equal(normal.Card, normal.Input, 1);
    }
}
