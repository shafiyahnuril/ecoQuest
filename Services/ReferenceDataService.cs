using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Errors;

namespace EcoQuest.Services;

public sealed class ReferenceDataService(IActivityTypeRepository types, IActivityCategoryRepository categories)
    : IReferenceDataService
{
    public async Task<Result<IReadOnlyList<ActivityTypeDto>>> GetActivityTypesAsync(CancellationToken ct = default)
    {
        try
        {
            var cats = (await categories.GetAllAsync(ct)).ToDictionary(c => c.CategoryId, c => c.CategoryName);
            var list = (await types.GetAllAsync(ct))
                .OrderBy(t => t.DisplayOrder)
                .Select(t => new ActivityTypeDto(
                    t.ActivityTypeId,
                    t.TypeName,
                    t.CategoryId,
                    cats.GetValueOrDefault(t.CategoryId, ""),
                    t.InputControlType,
                    t.UnitLabel,
                    t.Co2Coefficient,
                    t.PointFactor,
                    t.PlausibleMaxPerDay,
                    t.HardMaxPerDay,
                    t.Description,
                    t.DisplayOrder))
                .ToList();
            return Result<IReadOnlyList<ActivityTypeDto>>.Success(list);
        }
        catch (DataAccessException ex)
        {
            return Result<IReadOnlyList<ActivityTypeDto>>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    public async Task<Result<IReadOnlyList<ActivityCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = (await categories.GetAllAsync(ct))
                .OrderBy(c => c.CategoryId)
                .Select(c => new ActivityCategoryDto(c.CategoryId, c.CategoryName, c.Description))
                .ToList();
            return Result<IReadOnlyList<ActivityCategoryDto>>.Success(list);
        }
        catch (DataAccessException ex)
        {
            return Result<IReadOnlyList<ActivityCategoryDto>>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }
}
