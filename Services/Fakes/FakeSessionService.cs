using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

public sealed class FakeSessionService : ISessionService
{
    public UserSession? Current { get; private set; }
    public bool IsAuthenticated => Current is not null;
    public event EventHandler? SessionChanged;

    public void Start(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Current = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Current = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void End()
    {
        Current = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public UserSession RequireUser() => Current ?? throw new InvalidOperationException("No active session.");
}
