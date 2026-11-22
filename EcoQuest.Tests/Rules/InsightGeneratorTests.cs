
using System;
using System.Collections.Generic;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using Xunit;

namespace EcoQuest.Tests.Rules;

public class InsightGeneratorTests
{
    [Fact]
    public void Empty_period_returns_empty_state()
    {
        var insight = InsightGenerator.EmptyPeriod();

        Assert.Equal(InsightKind.EmptyState, insight.Kind);
        Assert.Contains("Belum ada refleksi", insight.Text);
    }

    [Fact]
    public void Top_category_text_and_percentage()
    {
        var breakdown = new List<CategoryBreakdownDto>
        {
            new(1, "Transportasi", 3m, 75m, "Transportasi"),
            new(2, "Energi", 1m, 25m, "Energi")
        };

        var insight = InsightGenerator.TopCategory(
            breakdown,
            "minggu ini");

        Assert.NotNull(insight);
        Assert.Equal(InsightKind.TopCategory, insight.Kind);
        Assert.Contains("Transportasi", insight.Text);
        Assert.Contains("75%", insight.Text);
        Assert.Contains("3 kg CO₂", insight.Text);
    }

    [Fact]
    public void Week_over_week_up_and_down()
    {
        var increased = InsightGenerator.WeekOverWeek(6m, 4m);
        var decreased = InsightGenerator.WeekOverWeek(3m, 4m);

        Assert.NotNull(increased);
        Assert.NotNull(decreased);

        Assert.Equal(InsightKind.WeekOverWeek, increased.Kind);
        Assert.Equal(InsightKind.WeekOverWeek, decreased.Kind);

        Assert.Contains("naik 50%", increased.Text);
        Assert.Contains("turun 25%", decreased.Text);
    }

    [Fact]
    public void Tree_equivalence_uses_constant()
    {
        Assert.Equal(0.06m, InsightConstants.TreeKgPerDay);

        var insight = InsightGenerator.Equivalence(4.2m);

        Assert.NotNull(insight);
        Assert.Equal(InsightKind.Equivalence, insight.Kind);
        Assert.Contains("70 hari", insight.Text);

        Assert.Null(InsightGenerator.Equivalence(0.4m));
    }

    [Fact]
    public void Week_over_week_handles_extreme_values_without_overflow()
    {
        var exception = Record.Exception(() =>
            InsightGenerator.WeekOverWeek(
             decimal.MaxValue,
             0.0000000000000000000000000001m
            )
        );

        Assert.Null(exception);
    }
}
