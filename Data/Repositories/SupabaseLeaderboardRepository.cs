using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Data.Rpc;

namespace EcoQuest.Data.Repositories;

public sealed class SupabaseLeaderboardRepository(RpcClient rpc) : ILeaderboardRepository
{
    public async Task<IReadOnlyList<LeaderboardRow>> GetAsync(Guid currentUserId, int limit, CancellationToken ct)
    {
        var rows = await rpc.CallAsync<List<LeaderboardJson>>(
            RpcNames.GetLeaderboard,
            new() { ["p_user_id"] = currentUserId, ["p_limit"] = limit },
            ct);

        return rows?.Select(r => new LeaderboardRow(r.Rank, r.UserName, r.TotalCo2SavedKg, r.LevelName, r.IsCurrentUser))
                    .ToList()
               ?? new List<LeaderboardRow>();
    }
}