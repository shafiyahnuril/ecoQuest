using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.ViewModels.Items;

/// <summary>Joins the explanation texts the service returned for one activity; nothing is composed here (FR-4.5).</summary>
public static class ImpactExplanation
{
    public static string Of(ActivityImpactDto? impact, string? description = null) =>
        string.Join(Environment.NewLine, new[] { impact?.FormulaExplanation, impact?.PointsExplanation, description }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
}
