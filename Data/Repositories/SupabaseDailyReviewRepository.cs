using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Rpc;

namespace EcoQuest.Data.Repositories;

/// <summary>
/// Implementasi repository refleksi harian dan riwayat aktivitas berbasis 5 fungsi RPC PostgreSQL (RPC 7–11).
/// </summary>
public sealed class SupabaseDailyReviewRepository(RpcClient rpc) : IDailyReviewRepository
{
    public async Task<DailyReview?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var row = await rpc.CallAsync<ReviewJson>(
            RpcNames.GetReviewByDate,
            new()
            {
                ["p_user_id"] = userId,
                ["p_date"] = RpcClient.D(date)
            },
            ct);

        return row is null ? null : ReviewMapper.ToDomain(row, userId);
    }

    public async Task<(Guid ReviewId, DateTimeOffset SubmittedAt)> SubmitAsync(ReviewSubmission s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);

        var items = ReviewMapper.BuildSubmitItems(s.Logs);
        var row = await rpc.CallAsync<SubmitResultJson>(
            RpcNames.SubmitReview,
            new()
            {
                ["p_user_id"] = s.UserId,
                ["p_review_date"] = RpcClient.D(s.ReviewDate),
                ["p_items"] = items,
                ["p_total_co2"] = s.TotalCo2,
                ["p_total_points"] = s.TotalPoints,
                ["p_new_streak"] = s.NewStreak,
                ["p_new_level_id"] = s.NewLevelId,
                ["p_new_badge_ids"] = s.NewBadgeIds.ToArray()
            },
            ct) ?? throw new DataAccessException(ErrorCode.Unknown, RpcNames.SubmitReview);

        return (row.ReviewId, row.SubmittedAt);
    }

    public async Task<IReadOnlyList<DailyTotalRow>> GetDailyTotalsAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var rows = await rpc.CallAsync<List<DailyTotalJson>>(
            RpcNames.GetDailyTotals,
            new()
            {
                ["p_user_id"] = userId,
                ["p_from"] = RpcClient.D(from),
                ["p_to"] = RpcClient.D(to)
            },
            ct);

        return (rows ?? []).Select(ReviewMapper.ToDailyTotalRow).ToList();
    }

    public async Task<IReadOnlyList<CategoryTotalRow>> GetCategoryBreakdownAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var rows = await rpc.CallAsync<List<CategoryTotalJson>>(
            RpcNames.GetCategoryBreakdown,
            new()
            {
                ["p_user_id"] = userId,
                ["p_from"] = RpcClient.D(from),
                ["p_to"] = RpcClient.D(to)
            },
            ct);

        return (rows ?? []).Select(ReviewMapper.ToCategoryTotalRow).ToList();
    }

    public async Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        IReadOnlyList<int>? categoryIds,
        CancellationToken ct)
    {
        var rows = await rpc.CallAsync<List<HistoryJson>>(
            RpcNames.GetHistory,
            new()
            {
                ["p_user_id"] = userId,
                ["p_from"] = RpcClient.D(from),
                ["p_to"] = RpcClient.D(to),
                ["p_category_ids"] = categoryIds is { Count: > 0 } ? categoryIds.ToArray() : null
            },
            ct);

        return (rows ?? []).Select(ReviewMapper.ToHistoryRow).ToList();
    }
}
