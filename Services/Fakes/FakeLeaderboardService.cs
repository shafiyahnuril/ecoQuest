using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

public sealed class FakeLeaderboardService : ILeaderboardService
{
    // Other participants are sample data (made-up names); the signed-in user is merged in by real totals.
    private static readonly (string Name, decimal Co2Kg, string Level)[] Others =
    [
        ("Rani", 41.2m, "Eco Champion"), ("Bagas", 36.8m, "Eco Champion"), ("Salsa", 33.5m, "Eco Warrior"),
        ("Kevin", 29.9m, "Eco Warrior"), ("Putri", 27.4m, "Eco Warrior"), ("Arya", 22.1m, "Eco Explorer"),
        ("Maya", 18.6m, "Eco Explorer"), ("Fajar", 15.3m, "Eco Explorer"), ("Nadia", 12.8m, "Eco Explorer"),
        ("Rizki", 9.4m, "Eco Starter")
    ];

    private readonly FakeContext _ctx;
    private readonly FakeStore _store;
    private readonly ISessionService _session;

    public FakeLeaderboardService(FakeContext ctx, FakeStore store, ISessionService session)
    {
        _ctx = ctx; _store = store; _session = session;
    }

    public async Task<Result<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(int top = 20, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("leaderboard")) return _ctx.NetworkFailure<IReadOnlyList<LeaderboardEntryDto>>();

        var me = _session.Current?.UserName ?? FakeData.DemoUserName;
        var rows = Others.Select(o => (o.Name, o.Co2Kg, (string?)o.Level, IsMe: false))
            .Append((me, _store.TotalCo2Kg, (string?)_store.LevelProgress().LevelName, IsMe: true))
            .OrderByDescending(r => r.Item2).ThenBy(r => r.Item1, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(top, 0))
            .Select((r, index) => new LeaderboardEntryDto(index + 1, r.Item1, r.Item2, r.Item3, r.IsMe))
            .ToList();
        return Result<IReadOnlyList<LeaderboardEntryDto>>.Success(rows);
    }
}
