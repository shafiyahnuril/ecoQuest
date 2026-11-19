using System;
using System.Collections.Generic;
using System.Linq;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Rpc;

namespace EcoQuest.Data.Mapping;

/// <summary>
/// Mapper murni antara model JSON RPC refleksi/riwayat dan entitas domain atau baris kontrak repository.
/// </summary>
public static class ReviewMapper
{
    public static List<Dictionary<string, object?>> BuildSubmitItems(IReadOnlyList<ActivityLog> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        return logs.Select(l => new Dictionary<string, object?>
        {
            ["logid"] = l.LogId,
            ["activitytypeid"] = l.ActivityTypeId,
            ["quantity"] = l.Quantity,
            ["starttime"] = RpcClient.T(l.StartTime),
            ["endtime"] = RpcClient.T(l.EndTime),
            ["note"] = l.Note,
            ["photopath"] = l.PhotoPath,
            ["co2savedkg"] = l.Co2SavedKg,
            ["pointsearned"] = l.PointsEarned,
            ["co2coefficientused"] = l.Co2CoefficientUsed,
            ["pointfactorused"] = l.PointFactorUsed
        }).ToList();
    }

    public static DailyReview ToDomain(ReviewJson j, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(j);

        var reviewDate = RecordMapper.ParseDate(j.ReviewDate);
        var logs = (j.Logs ?? []).Select(l => ActivityLog.Rehydrate(
            l.LogId,
            userId,
            j.ReviewId,
            l.ActivityTypeId,
            reviewDate,
            l.Quantity,
            RecordMapper.ParseTime(l.StartTime),
            RecordMapper.ParseTime(l.EndTime),
            l.Note,
            l.PhotoPath,
            l.Co2SavedKg,
            l.PointsEarned,
            l.Co2CoefficientUsed,
            l.PointFactorUsed));

        return DailyReview.Rehydrate(j.ReviewId, userId, reviewDate, j.SubmittedAt, logs);
    }

    public static DailyTotalRow ToDailyTotalRow(DailyTotalJson j)
    {
        ArgumentNullException.ThrowIfNull(j);
        return new DailyTotalRow(RecordMapper.ParseDate(j.ReviewDate), j.TotalCo2SavedKg, j.TotalPointsEarned);
    }

    public static CategoryTotalRow ToCategoryTotalRow(CategoryTotalJson j)
    {
        ArgumentNullException.ThrowIfNull(j);
        return new CategoryTotalRow(j.CategoryId, j.CategoryName, j.TotalCo2SavedKg);
    }

    public static HistoryRow ToHistoryRow(HistoryJson j)
    {
        ArgumentNullException.ThrowIfNull(j);
        return new HistoryRow(
            j.LogId,
            j.ReviewId,
            RecordMapper.ParseDate(j.LogDate),
            j.ActivityTypeId,
            j.TypeName,
            j.CategoryId,
            j.CategoryName,
            RecordMapper.ParseControl(j.InputControlType),
            j.UnitLabel,
            j.Quantity,
            RecordMapper.ParseTime(j.StartTime),
            RecordMapper.ParseTime(j.EndTime),
            j.Co2SavedKg,
            j.PointsEarned,
            j.Co2CoefficientUsed,
            j.PointFactorUsed,
            j.Note,
            j.PhotoPath);
    }
}
