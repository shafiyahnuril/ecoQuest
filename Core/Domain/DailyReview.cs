using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoQuest.Core.Domain;

public sealed class DailyReview
{
    private readonly List<ActivityLog> _logs = new();

    public Guid ReviewId { get; }
    public Guid UserId { get; }
    public DateOnly ReviewDate { get; }
    public DateTimeOffset? SubmittedAt { get; }
    public IReadOnlyList<ActivityLog> Logs => _logs;

    public decimal TotalCo2SavedKg => _logs.Sum(l => l.Co2SavedKg);
    public int TotalPointsEarned => _logs.Sum(l => l.PointsEarned);

    public DailyReview(Guid reviewId, Guid userId, DateOnly reviewDate, DateTimeOffset? submittedAt = null)
    {
        ReviewId = reviewId;
        UserId = userId;
        ReviewDate = reviewDate;
        SubmittedAt = submittedAt;
    }

    public void AddLog(ActivityLog log)
    {
        if (log.UserId != UserId)
            throw new InvalidOperationException("Log milik pengguna lain.");
        if (log.LogDate != ReviewDate)
            throw new InvalidOperationException("Tanggal log berbeda dari tanggal refleksi.");
        if (_logs.Any(l => l.ActivityTypeId == log.ActivityTypeId))
            throw new InvalidOperationException("Jenis aktivitas yang sama sudah ada di refleksi ini.");
        _logs.Add(log);
    }

    /// Refleksi tidak bisa diubah setelah disimpan (ADR-006).
    public bool IsLockedOn(DateOnly today) => true;

    public static DailyReview Rehydrate(Guid reviewId, Guid userId, DateOnly reviewDate,
        DateTimeOffset? submittedAt, IEnumerable<ActivityLog> logs)
    {
        var review = new DailyReview(reviewId, userId, reviewDate, submittedAt);
        foreach (var log in logs) review.AddLog(log);
        return review;
    }
}
