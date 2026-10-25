// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface IEveningReviewService
{
    Task<Result<TodayReviewDto>> GetTodayAsync(CancellationToken ct = default);                       // FR-2.1, AC-2.2
    /// Sinkron & murni (tanpa I/O) — dipanggil ViewModel setiap input berubah.
    ReviewValidationResult Validate(IReadOnlyList<ActivityInput> items, IReadOnlyList<ActivityTypeDto> types);
    /// Live preview satu baris; null bila input kosong/invalid.
    ActivityImpactDto? Preview(ActivityInput input, ActivityTypeDto type);
    /// Validasi ulang (otoritatif) → hitung → simpan atomik → ringkasan. Lihat SDD §5.2.
    Task<Result<ReviewSummaryDto>> SubmitAsync(SubmitReviewRequest request, CancellationToken ct = default);
}
