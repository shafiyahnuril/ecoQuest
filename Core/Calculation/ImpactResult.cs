namespace EcoQuest.Core.Calculation;

public sealed record ImpactResult(int ActivityTypeId, decimal Quantity, TimeOnly? StartTime, TimeOnly? EndTime,
                                  decimal Co2SavedKg, int PointsEarned, decimal Co2CoefficientUsed, decimal PointFactorUsed);
