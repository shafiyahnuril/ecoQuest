using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

public sealed class FakeGamificationService : IGamificationService
{
    private readonly FakeContext _ctx;
    private readonly FakeStore _store;

    public FakeGamificationService(FakeContext ctx, FakeStore store)
    {
        _ctx = ctx; _store = store;
    }

    public async Task<Result<LevelProgressDto>> GetLevelProgressAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("level")) return _ctx.NetworkFailure<LevelProgressDto>();
        return Result<LevelProgressDto>.Success(_store.LevelProgress());
    }

    public async Task<Result<IReadOnlyList<BadgeDto>>> GetBadgesAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("badges")) return _ctx.NetworkFailure<IReadOnlyList<BadgeDto>>();
        return Result<IReadOnlyList<BadgeDto>>.Success(_store.Badges());
    }
}
