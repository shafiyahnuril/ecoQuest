using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

public sealed class FakeReferenceDataService : IReferenceDataService
{
    private readonly FakeContext _ctx;

    public FakeReferenceDataService(FakeContext ctx) => _ctx = ctx;

    public async Task<Result<IReadOnlyList<ActivityTypeDto>>> GetActivityTypesAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("types")) return _ctx.NetworkFailure<IReadOnlyList<ActivityTypeDto>>();
        return Result<IReadOnlyList<ActivityTypeDto>>.Success(FakeData.ActivityTypes);
    }

    public async Task<Result<IReadOnlyList<ActivityCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("categories")) return _ctx.NetworkFailure<IReadOnlyList<ActivityCategoryDto>>();
        return Result<IReadOnlyList<ActivityCategoryDto>>.Success(FakeData.Categories);
    }
}
