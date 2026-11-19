using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetEnv;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.Integration;

[Trait("Category", "Integration")]
public class SupabaseDailyReviewRepositoryIT
{
    private static async Task<(SupabaseUserRepository Users, SupabaseLevelRepository Levels, SupabaseDailyReviewRepository Reviews)> CreateReposAsync()
    {
        Env.TraversePath().Load();
        var config = AppConfig.FromEnvironment();
        Assert.False(string.IsNullOrWhiteSpace(config.SupabaseUrl));
        Assert.False(string.IsNullOrWhiteSpace(config.SupabaseKey));

        var provider = new SupabaseClientProvider(config);
        await provider.InitializeAsync();

        var logger = new AppLogger(new AppPaths(), new SystemClock());
        var rpc = new RpcClient(provider, logger);

        return (
            new SupabaseUserRepository(rpc),
            new SupabaseLevelRepository(provider),
            new SupabaseDailyReviewRepository(rpc));
    }

    [IntegrationFact]
    public async Task Submit_get_by_date_duplicate_rejection_and_queries_work_against_real_supabase()
    {
        var (users, levelsRepo, reviews) = await CreateReposAsync();
        var hasher = new BCryptPasswordHasher();
        var today = new SystemClock().Today;
        var ct = CancellationToken.None;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"it_{suffix}";
        var user = await users.RegisterAsync(username, $"{username}@test.local", hasher.Hash("Rahasia123"), "Yogyakarta", ct);

        var levels = await levelsRepo.GetAllAsync(ct);
        var starterLevelId = levels.OrderBy(l => l.MinPoints).First().LevelId;

        var before = await reviews.GetByDateAsync(user.UserId, today, ct);
        Assert.Null(before);

        // Jalan kaki 3 km: CO2 = 0.45, poin = 9, koefisien = 0.15, pointFactor = 1.0
        var impact = new ImpactResult(1, 3m, null, null, 0.45m, 9, 0.15m, 1.0m);
        var log = ActivityLog.Create(user.UserId, today, 1, impact, note: "Uji integrasi BE-12");
        var submission = new ReviewSubmission(user.UserId, today, [log], 0.45m, 9, 1, starterLevelId, []);

        var (reviewId, submittedAt) = await reviews.SubmitAsync(submission, ct);
        Assert.NotEqual(Guid.Empty, reviewId);
        Assert.NotEqual(default, submittedAt);

        var fetched = await reviews.GetByDateAsync(user.UserId, today, ct);
        Assert.NotNull(fetched);
        Assert.Equal(reviewId, fetched!.ReviewId);
        Assert.Equal(today, fetched.ReviewDate);
        Assert.Single(fetched.Logs);
        Assert.Equal(0.45m, fetched.Logs[0].Co2SavedKg);
        Assert.Equal(9, fetched.Logs[0].PointsEarned);
        Assert.Equal("Uji integrasi BE-12", fetched.Logs[0].Note);

        // Submit kedua pada hari yang sama harus ditolak oleh DB dengan REVIEW_ALREADY_SUBMITTED.
        var dupLog = ActivityLog.Create(user.UserId, today, 1, impact);
        var dupSubmission = new ReviewSubmission(user.UserId, today, [dupLog], 0.45m, 9, 1, starterLevelId, []);
        var dupEx = await Assert.ThrowsAsync<DataAccessException>(() => reviews.SubmitAsync(dupSubmission, ct));
        Assert.Equal(ErrorCode.ReviewAlreadySubmitted, dupEx.Code);

        // Verifikasi RPC 9, 10, 11.
        var dailyTotals = await reviews.GetDailyTotalsAsync(user.UserId, today, today, ct);
        Assert.Single(dailyTotals);
        Assert.Equal(today, dailyTotals[0].ReviewDate);
        Assert.Equal(0.45m, dailyTotals[0].TotalCo2SavedKg);
        Assert.Equal(9, dailyTotals[0].TotalPointsEarned);

        var breakdown = await reviews.GetCategoryBreakdownAsync(user.UserId, today, today, ct);
        Assert.Equal(4, breakdown.Count);
        Assert.Equal(0.45m, breakdown.Single(c => c.CategoryId == 1).TotalCo2SavedKg);

        var allHistory = await reviews.GetHistoryAsync(user.UserId, today, today, null, ct);
        Assert.Single(allHistory);
        Assert.Equal(log.LogId, allHistory[0].LogId);
        Assert.Equal(0.45m, allHistory[0].Co2SavedKg);

        var filteredTransport = await reviews.GetHistoryAsync(user.UserId, today, today, [1], ct);
        Assert.Single(filteredTransport);

        var filteredEnergy = await reviews.GetHistoryAsync(user.UserId, today, today, [2], ct);
        Assert.Empty(filteredEnergy);
    }

    [IntegrationFact]
    public async Task Submit_with_mismatched_total_co2_throws_ValidationFailed_against_real_supabase()
    {
        var (users, levelsRepo, reviews) = await CreateReposAsync();
        var hasher = new BCryptPasswordHasher();
        var today = new SystemClock().Today;
        var ct = CancellationToken.None;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"it_{suffix}";
        var user = await users.RegisterAsync(username, $"{username}@test.local", hasher.Hash("Rahasia123"), "Yogyakarta", ct);

        var levels = await levelsRepo.GetAllAsync(ct);
        var starterLevelId = levels.OrderBy(l => l.MinPoints).First().LevelId;

        var impact = new ImpactResult(1, 3m, null, null, 0.45m, 9, 0.15m, 1.0m);
        var log = ActivityLog.Create(user.UserId, today, 1, impact);
        var invalidSubmission = new ReviewSubmission(user.UserId, today, [log], 0.50m, 9, 1, starterLevelId, []);

        var ex = await Assert.ThrowsAsync<DataAccessException>(() => reviews.SubmitAsync(invalidSubmission, ct));
        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }
}
