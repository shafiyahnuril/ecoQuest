// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface IDashboardService
{
    Task<Result<DashboardDto>> GetDashboardAsync(TrendRange range, CancellationToken ct = default);
}
