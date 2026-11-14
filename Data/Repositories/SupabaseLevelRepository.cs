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

public sealed class SupabaseLevelRepository(SupabaseClientProvider provider) : ILevelRepository
{
    public async Task<IReadOnlyList<Level>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await provider.Client.From<LevelRecord>()
                                .Order("minpoints", Ordering.Ascending)
                                .Get().WaitAsync(ct);
            return resp.Models.Select(RecordMapper.ToDomain).ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(SupabaseErrorMapper.Map(ex), "levels.get", ex);
        }
    }
}
