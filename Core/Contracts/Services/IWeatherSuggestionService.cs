// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface IWeatherSuggestionService
{
    /// TIDAK PERNAH gagal: bila API/cache gagal → IsFallback = true (FR-6.3, AC-4.2). Timeout total ≤ 6 detik.
    Task<WeatherSuggestionDto> GetTomorrowSuggestionAsync(CancellationToken ct = default);
}
