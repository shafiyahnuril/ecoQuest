using System;
using EcoQuest.Core.Rules;

namespace EcoQuest.Core.Domain;

public sealed class Badge
{
    public int BadgeId { get; }
    public string Name { get; }
    public string Description { get; }
    public CriteriaType CriteriaType { get; }
    public decimal CriteriaValue { get; }

    public Badge(int badgeId, string name, string description, CriteriaType criteriaType, decimal criteriaValue)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nama badge tidak boleh kosong.", nameof(name));
        if (criteriaValue < 0)
            throw new ArgumentOutOfRangeException(nameof(criteriaValue));

        BadgeId = badgeId;
        Name = name;
        Description = description;
        CriteriaType = criteriaType;
        CriteriaValue = criteriaValue;
    }

    public bool IsSatisfiedBy(BadgeContext ctx) => CriteriaType switch
    {
        CriteriaType.ActivityCount     => ctx.LogCount    >= CriteriaValue,
        CriteriaType.StreakDays        => ctx.Streak      >= CriteriaValue,
        CriteriaType.CO2SavedThreshold => ctx.TotalCo2    >= CriteriaValue,
        CriteriaType.PointsThreshold   => ctx.TotalPoints >= CriteriaValue,
        _ => false
    };
}
