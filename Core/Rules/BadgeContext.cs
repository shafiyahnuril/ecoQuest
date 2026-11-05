namespace EcoQuest.Core.Rules;

public sealed record BadgeContext(int LogCount, int Streak, decimal TotalCo2, int TotalPoints);
