using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Records;
using EcoQuest.Infrastructure;
using static Postgrest.Constants;

namespace EcoQuest.Data.Repositories;

public sealed class SupabaseWeatherCacheRepository(SupabaseClientProvider provider) : IWeatherCacheRepository
{
    public async Task<WeatherCache?> GetAsync(string city, DateOnly forDate, CancellationToken ct)
    {
        try
        {
            var resp = await provider.Client.From<WeatherCacheRecord>()
                .Filter("city", Operator.Equals, city)
                .Filter("fordate", Operator.Equals, forDate.ToString("yyyy-MM-dd"))
                .Get().WaitAsync(ct);
            var row = resp.Models.FirstOrDefault();
            return row is null ? null : RecordMapper.ToDomain(row);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(SupabaseErrorMapper.Map(ex), "weathercache.get", ex);
        }
    }

    public async Task UpsertAsync(WeatherCache entry, CancellationToken ct)
    {
        try
        {
            var record = new WeatherCacheRecord
            {
                City = entry.City,
                ForDate = entry.ForDate.ToDateTime(TimeOnly.MinValue),
                ForecastSummary = entry.ForecastSummary,
                RawResponse = entry.RawResponse,
                RetrievedAt = entry.RetrievedAt
            };
            await provider.Client.From<WeatherCacheRecord>()
                .Upsert(record, new Postgrest.QueryOptions { OnConflict = "city,fordate" })
                .WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(SupabaseErrorMapper.Map(ex), "weathercache.upsert", ex);
        }
    }
}