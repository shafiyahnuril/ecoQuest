using System;

namespace EcoQuest.Core.Domain;

public sealed class ActivityType
{
    public int ActivityTypeId { get; }
    public string TypeName { get; }
    public int CategoryId { get; }
    public InputControlType InputControlType { get; }
    public string UnitLabel { get; }
    public decimal Co2Coefficient { get; }
    public decimal PointFactor { get; }
    public decimal? PlausibleMaxPerDay { get; }
    public decimal HardMaxPerDay { get; }
    public string? Description { get; }
    public int DisplayOrder { get; }

    public ActivityType(
        int activityTypeId,
        string typeName,
        int categoryId,
        InputControlType inputControlType,
        string unitLabel,
        decimal co2Coefficient,
        decimal pointFactor,
        decimal? plausibleMaxPerDay,
        decimal hardMaxPerDay,
        string? description,
        int displayOrder)
    {
        if (pointFactor < 0.8m || pointFactor > 1.5m)
            throw new ArgumentOutOfRangeException(nameof(pointFactor), "PointFactor harus antara 0,8 dan 1,5.");

        ActivityTypeId = activityTypeId;
        TypeName = typeName;
        CategoryId = categoryId;
        InputControlType = inputControlType;
        UnitLabel = unitLabel;
        Co2Coefficient = co2Coefficient;
        PointFactor = pointFactor;
        PlausibleMaxPerDay = plausibleMaxPerDay;
        HardMaxPerDay = hardMaxPerDay;
        Description = description;
        DisplayOrder = displayOrder;
    }

    public bool BelongsToCategory(ActivityCategory category) => CategoryId == category.CategoryId;
    public bool ExceedsHard(decimal qty) => qty > HardMaxPerDay;
    public bool ExceedsPlausible(decimal qty) => PlausibleMaxPerDay.HasValue && qty > PlausibleMaxPerDay.Value;
}
