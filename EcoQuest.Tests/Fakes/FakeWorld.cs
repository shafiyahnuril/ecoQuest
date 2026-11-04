using EcoQuest.Services.Fakes;
using EcoQuest.Tests.TestDoubles;

namespace EcoQuest.Tests.Fakes;

/// <summary>Wires the fake services by hand with a fixed clock and a delay recorder (tests never actually wait).</summary>
public sealed class FakeWorld
{
    public static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);   // 19:00 WIB, 25 Oct 2026

    public FakeWorld(FakeScenario scenario)
    {
        Clock = new FakeClock(Now);
        Context = new FakeContext(scenario, Clock, (delay, _) => { Delays.Add(delay); return Task.CompletedTask; });
        Store = new FakeStore(Context);
        Session = new FakeSessionService();
        Auth = new FakeAuthService(Context, Store, Session);
        Profile = new FakeProfileService(Context, Store, Session);
        Reference = new FakeReferenceDataService(Context);
        Review = new FakeEveningReviewService(Context, Store);
        Dashboard = new FakeDashboardService(Context, Store);
        History = new FakeHistoryService(Context, Store);
        Weather = new FakeWeatherSuggestionService(Context, Session);
        Gamification = new FakeGamificationService(Context, Store);
        Leaderboard = new FakeLeaderboardService(Context, Store, Session);
    }

    public List<TimeSpan> Delays { get; } = new();
    public FakeClock Clock { get; }
    public FakeContext Context { get; }
    public FakeStore Store { get; }
    public FakeSessionService Session { get; }
    public FakeAuthService Auth { get; }
    public FakeProfileService Profile { get; }
    public FakeReferenceDataService Reference { get; }
    public FakeEveningReviewService Review { get; }
    public FakeDashboardService Dashboard { get; }
    public FakeHistoryService History { get; }
    public FakeWeatherSuggestionService Weather { get; }
    public FakeGamificationService Gamification { get; }
    public FakeLeaderboardService Leaderboard { get; }
}
