// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
namespace EcoQuest.Core.Common;

public enum ErrorCode
{
    None = 0,
    ValidationFailed,          // input tidak lolos hard validation
    UsernameTaken,             // FR-1.2 / AC-1.2
    EmailTaken,                // FR-1.2
    InvalidCredentials,        // FR-1.3 — pesan generik
    WrongOldPassword,          // FR-1.4
    ReviewAlreadySubmitted,    // FR-2.1 / AC-2.2
    ReviewDateMismatch,        // FR-2.6 — hari sudah berganti / jam perangkat tidak sesuai
    WarningsNotAcknowledged,   // ada soft warning yang belum dikonfirmasi pengguna
    NotFound,
    NotAuthenticated,          // tidak ada sesi aktif
    NetworkError,              // Supabase tidak terjangkau
    ExternalServiceUnavailable,// AccuWeather gagal (dipakai internal; UI menerima fallback)
    FileError,                 // foto/CSV gagal dibaca/ditulis
    Unknown
}
