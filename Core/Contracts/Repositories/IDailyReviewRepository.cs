using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

public interface IDailyReviewRepository
{
    Task<DailyReview?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken ct);                               // app_get_review_by_date
    Task<(Guid ReviewId, DateTimeOffset SubmittedAt)> SubmitAsync(ReviewSubmission submission, CancellationToken ct);  // app_submit_evening_review
    Task<IReadOnlyList<DailyTotalRow>> GetDailyTotalsAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<IReadOnlyList<CategoryTotalRow>> GetCategoryBreakdownAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid userId, DateOnly? from, DateOnly? to, IReadOnlyList<int>? categoryIds, CancellationToken ct);
}

public sealed record ReviewSubmission(Guid UserId, DateOnly ReviewDate, IReadOnlyList<ActivityLog> Logs,
                                      decimal TotalCo2, int TotalPoints, int NewStreak, int? NewLevelId, IReadOnlyList<int> NewBadgeIds);
