using System.Windows;
using System.Windows.Media;
using EcoQuest.Controls.Charts;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>CategoryColors (SkiaSharp) must mirror the tokens in Resources/Theme/Colors.xaml and Spacing.xaml.</summary>
[Collection(WpfCollection.Name)]
public class ChartThemeSyncTests
{
    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string ThemeColor(string key) => WpfTestHost.Run(() => Hex((Color)Application.Current.Resources[key]));

    private static string RgbHex(string argb) => argb.Length == 7 ? "#FF" + argb[1..] : argb;

    [Theory]
    [InlineData(1, "Color.Cat.Transportasi")]
    [InlineData(2, "Color.Cat.Energi")]
    [InlineData(3, "Color.Cat.Sampah")]
    [InlineData(4, "Color.Cat.Konsumsi")]
    public void Category_colours_match_the_theme(int categoryId, string key) =>
        Assert.Equal(ThemeColor(key), RgbHex(CategoryColors.Hex(categoryId)));

    [Fact]
    public void Chart_line_grid_and_axis_label_colours_match_the_theme()
    {
        Assert.Equal(ThemeColor("Color.Brand.Primary"), RgbHex(CategoryColors.TrendLineHex));
        Assert.Equal(ThemeColor("Color.Line.Chart"), RgbHex(CategoryColors.GridHex));
        Assert.Equal(ThemeColor("Color.Text.Secondary"), RgbHex(CategoryColors.AxisLabelHex));
    }

    [Fact]
    public void Area_gradient_matches_the_Chart_Area_token_at_038_opacity_fading_to_zero()
    {
        var stops = WpfTestHost.Run(() => ((LinearGradientBrush)Application.Current.Resources["Chart.Area"]).GradientStops.Select(s => Hex(s.Color)).ToList());

        Assert.Equal([CategoryColors.AreaTopHex, CategoryColors.AreaBottomHex], stops);
        Assert.Equal(0x61, (int)Math.Round(0.38 * 255));
    }

    [Fact]
    public void Donut_geometry_matches_the_spacing_tokens()
    {
        var (size, ring) = WpfTestHost.Run(() => ((double)Application.Current.Resources["Size.Donut"], (double)Application.Current.Resources["Thickness.DonutRing"]));

        Assert.Equal(ChartFactory.DonutDiameter, size);
        Assert.Equal(ChartFactory.DonutRingThickness, ring);
    }
}
