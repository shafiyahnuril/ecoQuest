// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface IHistoryService
{
    Task<Result<IReadOnlyList<HistoryDayDto>>> GetHistoryAsync(HistoryFilter filter, CancellationToken ct = default);
    Task<Result<ExportResultDto>> ExportCsvAsync(HistoryFilter filter, string targetFilePath, CancellationToken ct = default);
}
