using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;
using Newtonsoft.Json;

namespace EcoQuest.Data.Rpc;

/// <summary>
/// Pembungkus panggilan RPC PostgreSQL via Supabase dengan pembatalan dan pemetaan error seragam.
/// </summary>
public sealed class RpcClient(SupabaseClientProvider provider)
{
    public async Task<T?> CallAsync<T>(string fn, Dictionary<string, object?> args, CancellationToken ct)
    {
        try
        {
            var resp = await provider.Client.Rpc(fn, args).WaitAsync(ct);
            return Deserialize<T>(resp.Content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // pembatalan oleh pemanggil diteruskan tanpa dibungkus
        }
        catch (Exception ex)
        {
            throw new DataAccessException(SupabaseErrorMapper.Map(ex), fn, ex);
        }
    }

    public Task CallAsync(string fn, Dictionary<string, object?> args, CancellationToken ct)
        => CallAsync<object>(fn, args, ct);

    public static T? Deserialize<T>(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return default;
        if (content.Trim().Equals("null", StringComparison.Ordinal)) return default;
        return JsonConvert.DeserializeObject<T>(content);
    }

    public static string D(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? D(DateOnly? d) => d is null ? null : D(d.Value);

    public static string? T(TimeOnly? t) => t?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

