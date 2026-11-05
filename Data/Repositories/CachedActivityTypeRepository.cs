using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;

namespace EcoQuest.Data.Repositories;

public sealed class CachedActivityTypeRepository(IActivityTypeRepository inner) : IActivityTypeRepository
{
    private Task<IReadOnlyList<ActivityType>>? _cache;
    private readonly object _gate = new();

    public Task<IReadOnlyList<ActivityType>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_cache is null || _cache.IsFaulted || _cache.IsCanceled)
                _cache = inner.GetAllAsync(CancellationToken.None);
            return _cache.WaitAsync(ct);
        }
    }
}
