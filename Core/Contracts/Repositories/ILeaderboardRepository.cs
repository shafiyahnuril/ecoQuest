using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcoQuest.Core.Contracts.Repositories;

public interface ILeaderboardRepository
{
    Task<IReadOnlyList<LeaderboardRow>> GetAsync(Guid currentUserId, int limit, CancellationToken ct); // app_get_leaderboard
}
