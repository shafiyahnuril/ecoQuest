using System;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

// Record polos, tanpa atribut JSON. Kelas JSON di Data/Rpc berakhiran "Json".
public sealed record DailyTotalRow(DateOnly ReviewDate, decimal TotalCo2SavedKg, int TotalPointsEarned);

public sealed record CategoryTotalRow(int CategoryId, string CategoryName, decimal TotalCo2SavedKg);

public sealed record HistoryRow(Guid LogId, Guid ReviewId, DateOnly LogDate, int ActivityTypeId, string TypeName,
    int CategoryId, string CategoryName, InputControlType InputControlType, string UnitLabel, decimal Quantity,
    TimeOnly? StartTime, TimeOnly? EndTime, decimal Co2SavedKg, int PointsEarned,
    decimal Co2CoefficientUsed, decimal PointFactorUsed, string? Note, string? PhotoPath); // 18 kolom app_get_history

public sealed record LeaderboardRow(long Rank, string UserName, decimal TotalCo2SavedKg, string? LevelName, bool IsCurrentUser);
