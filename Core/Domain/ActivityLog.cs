using System;
using EcoQuest.Core.Calculation;

namespace EcoQuest.Core.Domain;

public sealed class ActivityLog
{
    public Guid LogId { get; }
    public Guid UserId { get; }
    public Guid? ReviewId { get; }
    public int ActivityTypeId { get; }
    public DateOnly LogDate { get; }
    public decimal Quantity { get; }
    public TimeOnly? StartTime { get; }
    public TimeOnly? EndTime { get; }
    public string? Note { get; }
    public string? PhotoPath { get; private set; }
    public decimal Co2SavedKg { get; }
    public int PointsEarned { get; }
    public decimal Co2CoefficientUsed { get; }
    public decimal PointFactorUsed { get; }

    private ActivityLog(Guid logId, Guid userId, Guid? reviewId, int activityTypeId, DateOnly logDate,
        decimal quantity, TimeOnly? startTime, TimeOnly? endTime, string? note, string? photoPath,
        decimal co2SavedKg, int pointsEarned, decimal co2CoefficientUsed, decimal pointFactorUsed)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (co2SavedKg < 0) throw new ArgumentOutOfRangeException(nameof(co2SavedKg));
        if (pointsEarned < 0) throw new ArgumentOutOfRangeException(nameof(pointsEarned));

        LogId = logId;
        UserId = userId;
        ReviewId = reviewId;
        ActivityTypeId = activityTypeId;
        LogDate = logDate;
        Quantity = quantity;
        StartTime = startTime;
        EndTime = endTime;
        Note = note;
        PhotoPath = photoPath;
        Co2SavedKg = co2SavedKg;
        PointsEarned = pointsEarned;
        Co2CoefficientUsed = co2CoefficientUsed;
        PointFactorUsed = pointFactorUsed;
    }

    /// Log baru (belum disimpan, ReviewId masih null). Angka dampak datang dari kalkulator.
    public static ActivityLog Create(Guid userId, DateOnly logDate, int activityTypeId,
        ImpactResult impact, TimeOnly? startTime = null, TimeOnly? endTime = null, string? note = null)
        => new(Guid.NewGuid(), userId, null, activityTypeId, logDate, impact.Quantity, startTime, endTime,
               note, null, impact.Co2SavedKg, impact.PointsEarned, impact.Co2CoefficientUsed, impact.PointFactorUsed);

    /// Membentuk ulang log dari database.
    public static ActivityLog Rehydrate(Guid logId, Guid userId, Guid? reviewId, int activityTypeId,
        DateOnly logDate, decimal quantity, TimeOnly? startTime, TimeOnly? endTime, string? note,
        string? photoPath, decimal co2SavedKg, int pointsEarned, decimal co2CoefficientUsed, decimal pointFactorUsed)
        => new(logId, userId, reviewId, activityTypeId, logDate, quantity, startTime, endTime, note,
               photoPath, co2SavedKg, pointsEarned, co2CoefficientUsed, pointFactorUsed);

    public void AttachPhoto(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path foto tidak boleh kosong.", nameof(path));
        PhotoPath = path;
    }

    /// Durasi dalam jam, 4 desimal. Selesai lebih awal dari mulai berarti melewati tengah malam (+24 jam).
    public decimal GetDurationInHours()
    {
        if (StartTime is null || EndTime is null) return 0m;
        var span = EndTime.Value - StartTime.Value;     // TimeOnly: selisih otomatis melingkar lewat 00:00
        return Math.Round((decimal)span.TotalMinutes / 60m, 4, MidpointRounding.AwayFromZero);
    }
}
