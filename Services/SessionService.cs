using System;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services;

/// <summary>
/// Menyimpan state sesi autentikasi aktif di memori selama aplikasi berjalan.
/// </summary>
public sealed class SessionService : ISessionService
{
    public UserSession? Current { get; private set; }
    public bool IsAuthenticated => Current is not null;
    public event EventHandler? SessionChanged;

    public void Start(UserSession session) => SetSession(session);

    public void Update(UserSession session) => SetSession(session);

    public void End() => ApplySession(null);

    public UserSession RequireUser() =>
        Current ?? throw new InvalidOperationException("Tidak ada sesi aktif.");

    private void SetSession(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ApplySession(session);
    }

    private void ApplySession(UserSession? session)
    {
        Current = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
