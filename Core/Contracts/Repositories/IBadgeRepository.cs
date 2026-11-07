using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

public interface IBadgeRepository
{
    Task<IReadOnlyList<Badge>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<UserBadge>> GetUserBadgesAsync(Guid userId, CancellationToken ct); // app_get_user_badges
}
