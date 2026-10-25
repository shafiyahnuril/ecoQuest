// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface IAuthService
{
    Task<Result<UserSession>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);   // sukses → sesi langsung aktif (PRD §6.1 "login otomatis")
    Task<Result<UserSession>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    void Logout();
    /// Validasi format sisi klien tanpa jaringan (untuk feedback instan per field).
    IReadOnlyDictionary<string, string> ValidateRegisterFormat(RegisterRequest request);
}
