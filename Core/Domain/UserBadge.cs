using System;

namespace EcoQuest.Core.Domain;

public sealed record UserBadge(Guid UserId, int BadgeId, DateTimeOffset EarnedAt);
