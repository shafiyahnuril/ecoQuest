// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface ISessionService
{
    UserSession? Current { get; }
    bool IsAuthenticated { get; }
    event EventHandler? SessionChanged;
    void Start(UserSession session);
    void Update(UserSession session);    // setelah edit profil
    void End();
    UserSession RequireUser();           // melempar InvalidOperationException bila belum login (bug, bukan alur normal)
}
