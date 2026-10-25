// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System;
namespace EcoQuest.Core.Common;

/// Satu-satunya sumber waktu. DILARANG memanggil DateTime.Now / DateTime.Today di luar implementasi ini (ADR-013).
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }              // tanggal Asia/Jakarta
    TimeZoneInfo TimeZone { get; }       // "SE Asia Standard Time"
}
