using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>One row of the summary's activity list; every text comes from the service's impact (UIR-39).</summary>
public sealed class SummaryActivityItem(ActivityImpactDto impact, IUiText text)
{
    public string IconKey { get; } = ActivityVisual.For(impact.ActivityTypeId).IconKey;

    public string Name { get; } = impact.TypeName;

    public string QuantityText { get; } = ActivityQuantityText.Of(impact, text);

    public string ImpactText { get; } =
        string.Format(CultureInfo.CurrentCulture, text.Get("Summary.Impact"), UiFormat.Kg(impact.Co2SavedKg), UiFormat.Count(impact.PointsEarned));

    public string FormulaText { get; } = impact.FormulaExplanation + Environment.NewLine + impact.PointsExplanation;
}
