using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using EcoQuest.Controls;
using EcoQuest.Tests.TestDoubles;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>FormulaToolTip (FR-4.5, UIR-39/50/53/62): blank text attaches nothing, keyboard-opened tooltips close again, long text wraps within 320 DIP.</summary>
[Collection(WpfCollection.Name)]
public class FormulaToolTipTests(ITestOutputHelper output)
{
    private const string Formula = "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂\n0,45 × 20 × 1,0 = 9 poin";

    private static T InWindow<T>(Func<Button, Window, T> act, string? text = Formula) => WpfTestHost.Run(() =>
    {
        var button = new Button { Content = "i" };
        FormulaToolTip.SetText(button, text);
        var window = new Window
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000, Width = 300, Height = 200,
            ShowActivated = false, Content = button,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            return act(button, window);
        }
        finally
        {
            window.Close();
        }
    });

    private static void RaiseLostFocus(UIElement element) =>
        element.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, element, null) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });

    private static KeyEventArgs RaiseKey(UIElement element, Window window, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        element.RaiseEvent(args);
        return args;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_text_attaches_no_tooltip_and_no_help_text(string? text)
    {
        var (tip, help) = InWindow((b, _) => (b.ToolTip, AutomationProperties.GetHelpText(b)), text);

        Assert.Null(tip);
        Assert.Equal("", help);
    }

    [Fact]
    public void Text_becomes_the_shared_tooltip_with_300_ms_delay_and_the_same_help_text()
    {
        var (isShared, content, delay, help) = InWindow((b, _) =>
        {
            var tip = (ToolTip)b.ToolTip;
            return (ReferenceEquals(tip.Style, b.FindResource("ToolTip.Default")), ((TextBlock)tip.Content).Text,
                ToolTipService.GetInitialShowDelay(b), AutomationProperties.GetHelpText(b));
        });

        Assert.True(isShared);
        Assert.Equal(Formula, content);
        Assert.Equal(300, delay);
        Assert.Equal(Formula, help);
    }

    [Fact]
    public void Clearing_the_text_removes_the_tooltip_and_help_text()
    {
        var (tip, help) = InWindow((b, _) =>
        {
            FormulaToolTip.SetText(b, "");
            return (b.ToolTip, AutomationProperties.GetHelpText(b));
        });

        Assert.Null(tip);
        Assert.Equal("", help);
    }

    [Fact]
    public void Changing_the_text_updates_the_same_tooltip()
    {
        var (sameTip, content) = InWindow((b, _) =>
        {
            var before = b.ToolTip;
            FormulaToolTip.SetText(b, "baru");
            return (ReferenceEquals(before, b.ToolTip), ((TextBlock)((ToolTip)b.ToolTip).Content).Text);
        });

        Assert.True(sameTip);
        Assert.Equal("baru", content);
    }

    [Fact]
    public void A_keyboard_opened_tooltip_closes_when_focus_is_lost()
    {
        var (opened, closed) = InWindow((b, _) =>
        {
            FormulaToolTip.Show(b);
            var open = FormulaToolTip.IsOpen(b);
            RaiseLostFocus(b);
            return (open, !FormulaToolTip.IsOpen(b));
        });

        Assert.True(opened);
        Assert.True(closed);
    }

    [Fact]
    public void Escape_closes_an_open_tooltip_and_is_consumed()
    {
        var (closed, handled) = InWindow((b, w) =>
        {
            FormulaToolTip.Show(b);
            var args = RaiseKey(b, w, Key.Escape);
            return (!FormulaToolTip.IsOpen(b), args.Handled);
        });

        Assert.True(closed);
        Assert.True(handled);
    }

    [Fact]
    public void Escape_is_left_alone_when_no_tooltip_is_open()
    {
        var handled = InWindow((b, w) => RaiseKey(b, w, Key.Escape).Handled);

        Assert.False(handled);
    }

    [Fact]
    public void Unloading_closes_an_open_tooltip()
    {
        var closed = InWindow((b, _) =>
        {
            FormulaToolTip.Show(b);
            b.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            return !FormulaToolTip.IsOpen(b);
        });

        Assert.True(closed);
    }

    private static (Size Tip, Size Text) Measured(string? text) => InWindow((b, _) =>
    {
        var tip = (ToolTip)b.ToolTip;
        tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return (tip.DesiredSize, ((TextBlock)tip.Content).DesiredSize);
    }, text);

    [Fact]
    public void Long_text_wraps_within_320_dip_and_grows_taller()
    {
        var (shortTip, _) = Measured(Formula);
        var (longTip, longText) = Measured(string.Concat(Enumerable.Repeat("Setiap aktivitas dihitung dari kuantitas dikali faktor CO₂ per unit. ", 6)));

        output.WriteLine($"short tooltip {shortTip.Width:0.#}x{shortTip.Height:0.#} DIP, long tooltip {longTip.Width:0.#}x{longTip.Height:0.#} DIP, long text {longText.Width:0.#}x{longText.Height:0.#} DIP");
        Assert.True(longTip.Width <= 320.01);
        Assert.True(longTip.Height > shortTip.Height * 2);
        Assert.True(longText.Width <= 320 - 20 + 0.01);
    }
}
