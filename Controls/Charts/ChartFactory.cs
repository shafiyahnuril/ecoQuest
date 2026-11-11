using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

namespace EcoQuest.Controls.Charts;

/// <summary>Builds the Dashboard series and axes (UI-UX-Flow-Frontend 2.8). No drawing happens here, so it can be unit-tested.</summary>
public static class ChartFactory
{
    public const double DonutDiameter = 150;
    public const double DonutRingThickness = 29;
    public const double DonutInnerRadius = DonutDiameter / 2 - DonutRingThickness;

    private const float LineThickness = 3;
    private const float DotDiameter = 6;
    private const float GridThickness = 1;
    private const float AxisTextSize = 11;
    private const double MinimumPeakKg = 0.1;
    private const double HeadroomFactor = 1.1;
    private const int GridSteps = 3;
    private const int MonthlyLabelEvery = 5;
    private const double XPaddingFraction = 0.04;
    private const double MinXPadding = 0.25;

    /// <summary>The donut fills its whole 150 DIP box; LiveCharts otherwise reserves an automatic margin around it (with HoverPushout = 0 on the series).</summary>
    public static Margin NoDrawMargin { get; } = new(0);

    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public static ISeries[] Trend(IReadOnlyList<TrendPointDto> points) =>
    [
        new LineSeries<double?>
        {
            Name = string.Empty,
            Values = points.Select(p => p.HasReview ? (double?)p.Co2Kg : null).ToArray(),
            LineSmoothness = 0,
            GeometrySize = DotDiameter,
            Stroke = new SolidColorPaint(CategoryColors.TrendLine, LineThickness),
            Fill = new LinearGradientPaint(CategoryColors.AreaTop, CategoryColors.AreaBottom),
            GeometryFill = new SolidColorPaint(CategoryColors.TrendLine),
            GeometryStroke = null,
            YToolTipLabelFormatter = point => TrendTooltip(points[point.Index].Date, (decimal)point.Coordinate.PrimaryValue),
        },
    ];

    public static string TrendTooltip(DateOnly date, decimal co2Kg) => $"{UiFormat.Date(date)}: {UiFormat.Co2(co2Kg)}";

    public static Axis[] TrendXAxes(IReadOnlyList<TrendPointDto> points, TrendRange range) =>
    [
        new Axis
        {
            MinLimit = -XPadding(points.Count),
            MaxLimit = points.Count - 1 + XPadding(points.Count),
            Labels = points.Select((p, i) => AxisLabel(p.Date, range, points.Count - 1 - i)).ToArray(),
            LabelsPaint = new SolidColorPaint(CategoryColors.AxisLabel),
            TextSize = AxisTextSize,
            MinStep = 1,
            ForceStepToMin = true,
            ShowSeparatorLines = false,
        },
    ];

    public static Axis[] TrendYAxes(IReadOnlyList<TrendPointDto> points)
    {
        var peak = Math.Max(MinimumPeakKg, points.Where(p => p.HasReview).Select(p => (double)p.Co2Kg).DefaultIfEmpty(0).Max());
        return
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = peak * HeadroomFactor,
                MinStep = peak / GridSteps,
                ForceStepToMin = true,
                Labeler = _ => string.Empty,
                SeparatorsPaint = new SolidColorPaint(CategoryColors.Grid, GridThickness),
            },
        ];
    }

    public static ISeries[] Breakdown(IReadOnlyList<CategoryBreakdownDto> categories) =>
        categories.Select(c => (ISeries)new PieSeries<double>
        {
            Name = c.CategoryName,
            Values = new[] { (double)c.Co2Kg },
            InnerRadius = DonutInnerRadius,
            HoverPushout = 0,
            Fill = new SolidColorPaint(CategoryColors.For(c.CategoryId)),
            Stroke = null,
            ToolTipLabelFormatter = _ => c.Explanation,
        }).ToArray();

    // Explicit limits keep trailing days without a review on the axis; LiveCharts would otherwise stop at the last real point.
    private static double XPadding(int pointCount) => Math.Max(MinXPadding, (pointCount - 1) * XPaddingFraction);

    private static string AxisLabel(DateOnly date, TrendRange range, int daysFromToday) => range switch
    {
        TrendRange.Weekly => date.ToString("ddd", Id),
        _ => daysFromToday % MonthlyLabelEvery == 0 ? date.ToString("d MMM", Id) : string.Empty,
    };
}
