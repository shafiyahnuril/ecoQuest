using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

public interface IActivityTypeRepository
{
    Task<IReadOnlyList<ActivityType>> GetAllAsync(CancellationToken ct = default);
}

public interface IActivityCategoryRepository
{
    Task<IReadOnlyList<ActivityCategory>> GetAllAsync(CancellationToken ct = default);
}
