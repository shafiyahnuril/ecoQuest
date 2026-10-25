// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface ILeaderboardService
{
    Task<Result<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(int top = 20, CancellationToken ct = default);
}
