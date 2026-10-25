using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Services.Fakes;

public sealed class FakeAccount
{
    public FakeAccount(Guid userId, string userName, string email, string city, string password)
    {
        UserId = userId; UserName = userName; Email = email; City = city; Password = password;
    }

    public Guid UserId { get; }
    public string UserName { get; }
    public string Email { get; }
    public string City { get; set; }
    public string Password { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// In-memory "database" shared by the fakes so that submitting a review is reflected on Dashboard, History,
/// Achievements and Leaderboard. History is seeded with Random(42) and dates derived from IClock, so it is
/// deterministic for a given day. There is one journal for the whole app regardless of which account is signed in.
/// </summary>
public sealed class FakeStore
{
    private const int SeedDays = 30;
    private readonly FakeContext _ctx;
    private readonly List<HistoryDayDto> _days = new();   // newest first

    public FakeStore(FakeContext ctx)
    {
        _ctx = ctx;
        Accounts = new Dictionary<string, FakeAccount>(StringComparer.OrdinalIgnoreCase)
        {
            [FakeData.DemoUserName] = new FakeAccount(FakeData.DemoUserId, FakeData.DemoUserName, FakeData.DemoEmail,
                FakeData.DemoCity, FakeData.DemoPassword)
            { CreatedAt = ctx.Clock.UtcNow.AddDays(ctx.Scenario == FakeScenario.Empty ? 0 : -45) }
        };

        if (ctx.Scenario != FakeScenario.Empty) SeedHistory();
    }

    public Dictionary<string, FakeAccount> Accounts { get; }
    public IReadOnlyList<HistoryDayDto> Days => _days;
    public ReviewSummaryDto? TodaySummary { get; private set; }

    public int TotalPoints => _days.Sum(d => d.TotalPoints);
    public decimal TotalCo2Kg => _days.Sum(d => d.TotalCo2Kg);
    public int TotalLogCount => _days.Sum(d => d.Items.Count);
    public int TotalReviewCount => _days.Count;
    public DateOnly? LastReviewDate => _days.Count == 0 ? null : _days.Max(d => d.ReviewDate);

    /// <summary>Effective streak: 0 when neither today nor yesterday has a review.</summary>
    public int CurrentStreak
    {
        get
        {
            var dates = _days.Select(d => d.ReviewDate).ToHashSet();
            var day = _ctx.Clock.Today;
            if (!dates.Contains(day)) day = day.AddDays(-1);
            var streak = 0;
            while (dates.Contains(day)) { streak++; day = day.AddDays(-1); }
            return streak;
        }
    }

    public FakeLevel LevelFor(int points) => FakeData.Levels.Last(l => points >= l.MinPoints);

    public LevelProgressDto LevelProgress()
    {
        var points = TotalPoints;
        var level = LevelFor(points);
        var next = FakeData.Levels.FirstOrDefault(l => l.MinPoints > level.MinPoints);
        var ratio = next is null ? 1.0 : Math.Clamp((double)(points - level.MinPoints) / (next.MinPoints - level.MinPoints), 0, 1);
        return new LevelProgressDto(level.LevelId, level.LevelName, level.MinPoints, level.MaxPoints, points,
            next?.LevelName, next is null ? null : next.MinPoints - points, ratio);
    }

    public IReadOnlyList<BadgeDto> Badges()
    {
        var logs = TotalLogCount; var points = TotalPoints; var co2 = TotalCo2Kg; var streak = CurrentStreak;
        var earnedAt = LastReviewDate is { } last ? SubmittedAtFor(last) : (DateTimeOffset?)null;

        return FakeData.Badges.Select(b =>
        {
            var current = b.CriteriaType switch
            {
                CriteriaType.ActivityCount => (decimal)logs,
                CriteriaType.PointsThreshold => points,
                CriteriaType.StreakDays => streak,
                _ => co2
            };
            var earned = current >= b.CriteriaValue;
            return new BadgeDto(b.BadgeId, b.Name, b.Description, b.CriteriaType, b.CriteriaValue,
                earned, earned ? earnedAt : null, current);
        }).ToList();
    }

    /// <summary>Stores today's review (also used by the Errors scenario to simulate "already submitted").</summary>
    public ReviewSummaryDto SubmitToday(IReadOnlyList<ActivityImpactDto> items)
    {
        var today = _ctx.Clock.Today;
        var levelBefore = LevelFor(TotalPoints).LevelName;
        var earnedBefore = Badges().Where(b => b.IsEarned).Select(b => b.BadgeId).ToHashSet();

        var co2 = items.Sum(i => i.Co2SavedKg);
        var points = items.Sum(i => i.PointsEarned);
        var submittedAt = _ctx.Clock.UtcNow;
        var reviewId = Guid.NewGuid();
        _days.Insert(0, new HistoryDayDto(reviewId, today, co2, points, items));

        var levelAfter = LevelFor(TotalPoints).LevelName;
        var newBadges = Badges().Where(b => b.IsEarned && !earnedBefore.Contains(b.BadgeId))
                                .Select(b => b with { EarnedAt = submittedAt }).ToList();

        var insight = items.Count == 0
            ? "Refleksi kosong tetap menjaga streak-mu. Besok catat aktivitas nyatamu."
            : $"{items.OrderByDescending(i => i.Co2SavedKg).First().TypeName} menyumbang dampak terbesar hari ini: " +
              $"{FakeImpact.Num(items.Max(i => i.Co2SavedKg))} kg CO₂.";

        TodaySummary = new ReviewSummaryDto(reviewId, today, submittedAt, co2, points, items, insight, CurrentStreak,
            levelAfter != levelBefore ? new LevelChangeDto(levelBefore, levelAfter) : null, newBadges);
        return TodaySummary;
    }

    public DateTimeOffset SubmittedAtFor(DateOnly date)
    {
        var local = date.ToDateTime(new TimeOnly(21, 30));
        return new DateTimeOffset(local, _ctx.Clock.TimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private void SeedHistory()
    {
        var rng = new Random(42);
        var today = _ctx.Clock.Today;

        for (var back = 1; back <= SeedDays; back++)
        {
            var hasReview = rng.NextDouble() < 0.75;
            if (back <= 4) hasReview = true;        // current streak of 4 (badge "7 hari" stays locked at 4/7)
            else if (back == 5) hasReview = false;

            // Draw every random value unconditionally so the sequence never depends on earlier outcomes.
            var drawn = FakeData.ActivityTypes
                .Select(t => new Draw(t, rng.NextDouble() < 0.5, RandomInput(rng, t), RandomGuid(rng)))
                .ToList();
            var reviewId = RandomGuid(rng);

            if (!hasReview) continue;
            if (!drawn.Any(d => d.Done)) drawn[0] = drawn[0] with { Done = true };

            var items = drawn.Where(d => d.Done).Select(d => FakeImpact.Build(d.Input, d.Type, d.LogId)).ToList();
            _days.Add(new HistoryDayDto(reviewId, today.AddDays(-back),
                items.Sum(i => i.Co2SavedKg), items.Sum(i => i.PointsEarned), items));
        }
    }

    private sealed record Draw(ActivityTypeDto Type, bool Done, ActivityInput Input, Guid LogId);

    private static Guid RandomGuid(Random rng)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return new Guid(bytes);
    }

    private static ActivityInput RandomInput(Random rng, ActivityTypeDto type)
    {
        var quantity = rng.Next(2, 13) * 0.5m;                // 1.0 .. 6.5 (used by Numeric)
        var count = rng.Next(1, 4);                           // 1 .. 3 (used by Count)
        var start = new TimeOnly(20 + rng.Next(0, 3), rng.Next(0, 12) * 5);
        var end = start.AddHours(rng.Next(1, 4));             // may cross midnight

        return type.InputControlType switch
        {
            InputControlType.Numeric => ActivityInput.Empty(type.ActivityTypeId) with { Quantity = quantity },
            InputControlType.Count => ActivityInput.Empty(type.ActivityTypeId) with { Quantity = count },
            InputControlType.TimeRange => ActivityInput.Empty(type.ActivityTypeId) with { StartTime = start, EndTime = end },
            _ => ActivityInput.Empty(type.ActivityTypeId) with { IsYes = true }
        };
    }
}
