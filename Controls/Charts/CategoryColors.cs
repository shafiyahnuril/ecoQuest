using SkiaSharp;

namespace EcoQuest.Controls.Charts;

/// <summary>Single source of chart colours for SkiaSharp; every value mirrors a token in Resources/Theme/Colors.xaml (a test keeps them in sync).</summary>
public static class CategoryColors
{
    public const string TrendLineHex = "#327A4C";
    public const string AreaTopHex = "#619FCCA0";
    public const string AreaBottomHex = "#009FCCA0";
    public const string GridHex = "#E5EBE2";
    public const string AxisLabelHex = "#5B685F";

    private static readonly Dictionary<int, string> CategoryHex = new()
    {
        [1] = "#6AA9B9",
        [2] = "#E7B35A",
        [3] = "#83AE6A",
        [4] = "#AD9ACE",
    };

    public static SKColor TrendLine { get; } = SKColor.Parse(TrendLineHex);
    public static SKColor AreaTop { get; } = SKColor.Parse(AreaTopHex);
    public static SKColor AreaBottom { get; } = SKColor.Parse(AreaBottomHex);
    public static SKColor Grid { get; } = SKColor.Parse(GridHex);
    public static SKColor AxisLabel { get; } = SKColor.Parse(AxisLabelHex);

    public static string Hex(int categoryId) => CategoryHex[categoryId];

    public static SKColor For(int categoryId) => SKColor.Parse(Hex(categoryId));
}
