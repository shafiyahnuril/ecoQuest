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
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using static Postgrest.Constants;

namespace EcoQuest.Data.Repositories;

public sealed class SupabaseBadgeRepository(SupabaseClientProvider provider, RpcClient rpc) : IBadgeRepository
{
    public async Task<IReadOnlyList<Badge>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await provider.Client.From<BadgeRecord>()
                                .Order("badgeid", Ordering.Ascending)
                                .Get().WaitAsync(ct);
            return resp.Models.Select(RecordMapper.ToDomain).ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(SupabaseErrorMapper.Map(ex), "badges.get", ex);
        }
    }

    public async Task<IReadOnlyList<UserBadge>> GetUserBadgesAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await rpc.CallAsync<List<UserBadgeJson>>(
            RpcNames.GetUserBadges,
            new() { ["p_user_id"] = userId },
            ct);
        return (rows ?? []).Select(j => RecordMapper.ToDomain(j, userId)).ToList();
    }
}
