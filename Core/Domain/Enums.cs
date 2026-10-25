// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
namespace EcoQuest.Core.Domain;

/// Nilai string identik dengan kolom activitytypes.inputcontroltype (check constraint DB).
public enum InputControlType { Numeric, TimeRange, Count, Toggle }

/// Nilai string identik dengan kolom badges.criteriatype.
public enum CriteriaType { ActivityCount, PointsThreshold, StreakDays, CO2SavedThreshold }

public enum TrendRange { Weekly, Monthly }          // FR-4.1: 7 hari / 30 hari terakhir, titik harian

public enum ValidationSeverity { Error, Warning }   // ADR-005: Error = blocking, Warning = konfirmasi

public enum InsightKind { TopCategory, WeakestCategory, WeekOverWeek, Equivalence, Streak, EmptyState }
