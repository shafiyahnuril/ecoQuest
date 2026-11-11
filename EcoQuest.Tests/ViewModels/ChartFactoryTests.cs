using EcoQuest.Controls.Charts;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>ChartFactory builds data only (UIR-52, UIR-53); nothing is drawn here.</summary>
public class ChartFactoryTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);   // Saturday

    private static List<TrendPointDto> Week(params decimal?[] kgPerDay) =>
        kgPerDay.Select((kg, i) => new TrendPointDto(Today.AddDays(i - (kgPerDay.Length - 1)), kg ?? 0m, kg is not null)).ToList();

    private static LineSeries<double?> TrendLine(IReadOnlyList<TrendPointDto> points) =>
        Assert.IsType<LineSeries<double?>>(Assert.Single(ChartFactory.Trend(points)));

    private static double?[] Values(LineSeries<double?> line) => ((IEnumerable<double?>)line.Values!).ToArray();

    private static CategoryBreakdownDto Cat(int id, string name, decimal kg, decimal pct) => new(id, name, kg, pct, $"{name}: {kg} kg");

    [Fact]
    public void Trend_has_one_value_per_day_and_a_gap_for_days_without_a_review()
    {
        var points = Week(0.4m, null, 0.9m, null, null, 0.3m, null);

        var values = Values(TrendLine(points));

        Assert.Equal([0.4, null, 0.9, null, null, 0.3, null], values);
    }

    [Fact]
    public void A_review_with_zero_co2_is_a_real_point_not_a_gap()
    {
        var values = Values(TrendLine(Week(0m, null)));

        Assert.Equal(0d, values[0]);
        Assert.Null(values[1]);
    }

    [Fact]
    public void Trend_line_uses_the_brand_colour_three_pixels_wide_with_round_dots()
    {
        var line = TrendLine(Week(0.4m, 0.5m));

        var stroke = Assert.IsType<SolidColorPaint>(line.Stroke);
        Assert.Equal(CategoryColors.TrendLine, stroke.Color);
        Assert.Equal(3f, stroke.StrokeThickness);
        Assert.Equal(CategoryColors.TrendLine, Assert.IsType<SolidColorPaint>(line.GeometryFill).Color);
        Assert.Equal(6d, line.GeometrySize);
        Assert.Equal(0d, line.LineSmoothness);
        Assert.IsType<LinearGradientPaint>(line.Fill);
    }

    [Fact]
    public void Trend_tooltip_shows_the_date_and_the_co2_in_indonesian_format()
    {
        Assert.Equal("7 Nov 2026: 0,90 kg CO₂", ChartFactory.TrendTooltip(Today, 0.9m));
        Assert.Equal("7 Nov 2026: 0,045 kg CO₂", ChartFactory.TrendTooltip(Today, 0.045m));
    }

    [Fact]
    public void Weekly_axis_labels_are_the_day_abbreviations()
    {
        var axis = Assert.Single(ChartFactory.TrendXAxes(Week(0.1m, 0.1m, 0.1m, 0.1m, 0.1m, 0.1m, 0.1m), TrendRange.Weekly));

        Assert.Equal(["Min", "Sen", "Sel", "Rab", "Kam", "Jum", "Sab"], axis.Labels);
    }

    [Fact]
    public void Monthly_axis_labels_show_the_date_every_fifth_day_counted_from_today()
    {
        var points = Week(Enumerable.Repeat<decimal?>(0.1m, 30).ToArray());

        var labels = Assert.Single(ChartFactory.TrendXAxes(points, TrendRange.Monthly)).Labels!;

        Assert.Equal(30, labels.Count);
        Assert.Equal("7 Nov", labels[29]);
        Assert.Equal("2 Nov", labels[24]);
        Assert.Equal("13 Okt", labels[4]);
        Assert.Equal(6, labels.Count(l => l.Length > 0));
    }

    [Fact]
    public void X_axis_keeps_trailing_days_without_a_review_inside_the_limits()
    {
        var axis = Assert.Single(ChartFactory.TrendXAxes(Week(0.4m, 0.5m, null, null), TrendRange.Weekly));

        Assert.True(axis.MaxLimit > 3);
        Assert.True(axis.MinLimit < 0);
    }

    [Fact]
    public void Y_axis_starts_at_zero_and_draws_four_grid_lines_up_to_the_peak()
    {
        var axis = Assert.Single(ChartFactory.TrendYAxes(Week(0.6m, 1.2m, null)));

        Assert.Equal(0d, axis.MinLimit);
        Assert.Equal(0.4, axis.MinStep, 6);
        Assert.True(axis.MaxLimit > 1.2 && axis.MaxLimit < 1.6);
        Assert.Equal(CategoryColors.Grid, Assert.IsType<SolidColorPaint>(axis.SeparatorsPaint).Color);
    }

    [Fact]
    public void Trend_data_may_be_empty()
    {
        var points = Week();

        Assert.Empty(Values(TrendLine(points)));
        Assert.True(Assert.Single(ChartFactory.TrendYAxes(points)).MaxLimit > 0);
        Assert.Empty(Assert.Single(ChartFactory.TrendXAxes(points, TrendRange.Weekly)).Labels!);
    }

    [Fact]
    public void Breakdown_has_one_slice_per_category_in_its_category_colour()
    {
        var cats = new[] { Cat(1, "Transportasi", 4.5m, 47.6m), Cat(2, "Energi", 1m, 10.6m), Cat(3, "Sampah/Daur Ulang", 2.75m, 29.1m), Cat(4, "Konsumsi", 1.2m, 12.7m) };

        var series = ChartFactory.Breakdown(cats).Cast<PieSeries<double>>().ToList();

        Assert.Equal(4, series.Count);
        Assert.Equal(cats.Select(c => c.CategoryName), series.Select(s => s.Name));
        Assert.Equal([4.5, 1, 2.75, 1.2], series.Select(s => ((IEnumerable<double>)s.Values!).Single()));
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(CategoryColors.For(cats[i].CategoryId), Assert.IsType<SolidColorPaint>(series[i].Fill).Color);
        }
    }

    [Fact]
    public void Donut_ring_is_29_thick_inside_a_150_box()
    {
        var slice = Assert.IsType<PieSeries<double>>(Assert.Single(ChartFactory.Breakdown([Cat(1, "Transportasi", 1m, 100m)])));

        Assert.Equal(46d, slice.InnerRadius);
        Assert.Equal(150d / 2 - 29d, ChartFactory.DonutInnerRadius);
    }

    [Fact]
    public void Slice_tooltip_is_the_explanation_from_the_service()
    {
        var cat = new CategoryBreakdownDto(1, "Transportasi", 1m, 100m, "Transportasi: 1 kg CO₂ dari 2 aktivitas");

        var slice = Assert.IsType<PieSeries<double>>(Assert.Single(ChartFactory.Breakdown([cat])));

        Assert.NotNull(slice.ToolTipLabelFormatter);
    }

    [Fact]
    public void The_four_categories_have_distinct_colours_and_an_unknown_id_is_a_bug()
    {
        Assert.Equal(4, new[] { 1, 2, 3, 4 }.Select(CategoryColors.Hex).Distinct().Count());
        Assert.Throws<KeyNotFoundException>(() => CategoryColors.Hex(99));
    }
}
