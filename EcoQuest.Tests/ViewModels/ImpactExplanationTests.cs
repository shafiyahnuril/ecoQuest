using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.ViewModels.Items;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class ImpactExplanationTests
{
    private static ActivityImpactDto Impact(string formula, string points) =>
        new(null, 1, "Jalan Kaki", 1, "Transportasi", InputControlType.Numeric, 3m, "km", null, null, 0.45m, 9, 0.15m, 1m, formula, points, null, null);

    [Fact]
    public void Joins_formula_points_and_description_in_that_order()
    {
        var text = ImpactExplanation.Of(Impact("rumus", "poin"), "deskripsi");

        Assert.Equal(string.Join(Environment.NewLine, "rumus", "poin", "deskripsi"), text);
    }

    [Fact]
    public void Skips_blank_parts()
    {
        Assert.Equal("rumus", ImpactExplanation.Of(Impact("rumus", " "), ""));
        Assert.Equal("deskripsi", ImpactExplanation.Of(null, "deskripsi"));
        Assert.Equal("", ImpactExplanation.Of(null));
    }

    [Fact]
    public void History_and_summary_rows_share_the_same_text()
    {
        var impact = Impact("rumus", "poin");
        var text = Substitute.For<IUiText>();
        text.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());

        Assert.Equal(ImpactExplanation.Of(impact), new HistoryLineItemViewModel(impact, null, text, _ => null).FormulaText);
        Assert.Equal(ImpactExplanation.Of(impact), new SummaryActivityItem(impact, text).FormulaText);
    }
}
