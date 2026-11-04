using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Records;
using EcoQuest.Infrastructure;
using static Postgrest.Constants;

namespace EcoQuest.Data.Repositories;

public sealed class SupabaseActivityCategoryRepository(SupabaseClientProvider provider) : IActivityCategoryRepository
{
    public async Task<IReadOnlyList<ActivityCategory>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await provider.Client.From<ActivityCategoryRecord>()
                                .Order("categoryid", Ordering.Ascending)
                                .Get().WaitAsync(ct);
            return resp.Models.Select(RecordMapper.ToDomain).ToList();
        }
        catch (Exception ex) when (ex is not DataAccessException && ex is not OperationCanceledException)
        {
            throw new DataAccessException(SupabaseErrorMapper.Map(ex), "activitycategories.get", ex);
        }
    }
}
