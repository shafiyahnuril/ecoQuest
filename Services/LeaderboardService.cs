using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Errors;

namespace EcoQuest.Services;

public sealed class LeaderboardService(ISessionService session, ILeaderboardRepository repo) : ILeaderboardService
{
    public async Task<Result<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(
        int top = 20, CancellationToken ct = default)
    {
        var user = session.RequireUser();
        try
        {
            var rows = await repo.GetAsync(user.UserId, top, ct);
            var list = rows
                .Select(r => new LeaderboardEntryDto(r.Rank, r.UserName, r.TotalCo2SavedKg, r.LevelName, r.IsCurrentUser))
                .ToList();
            return Result<IReadOnlyList<LeaderboardEntryDto>>.Success(list);
        }
        catch (DataAccessException ex)
        {
            return Result<IReadOnlyList<LeaderboardEntryDto>>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }
}