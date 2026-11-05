namespace EcoQuest.Core.Calculation;

public sealed record ImpactResult(decimal Quantity, decimal Co2SavedKg, int PointsEarned,
                                  decimal Co2CoefficientUsed, decimal PointFactorUsed);
