// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Dtos;

public sealed record ActivityCategoryDto(int CategoryId, string CategoryName, string? Description);

public sealed record ActivityTypeDto(
    int ActivityTypeId, string TypeName, int CategoryId, string CategoryName,
    InputControlType InputControlType, string UnitLabel,
    decimal Co2Coefficient, decimal PointFactor,
    decimal? PlausibleMaxPerDay, decimal HardMaxPerDay,
    string? Description, int DisplayOrder);
