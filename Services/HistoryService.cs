using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Errors;
using System.IO;

namespace EcoQuest.Services;

/// <summary>
/// Layanan riwayat refleksi dan log aktivitas dengan filter rentang tanggal serta kategori (SDD §6.7).
/// </summary>
public sealed class HistoryService(
    ISessionService session,
    IDailyReviewRepository reviews,
    DtoFactory dto) : IHistoryService
{
    public async Task<Result<IReadOnlyList<HistoryDayDto>>> GetHistoryAsync(
        HistoryFilter filter,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.From is { } from && filter.To is { } to && from > to)
        {
            const string msg = "Tanggal awal harus sebelum tanggal akhir";
            return Result<IReadOnlyList<HistoryDayDto>>.Failure(
                ErrorCode.ValidationFailed,
                msg,
                new Dictionary<string, string> { ["From"] = msg });
        }

        var user = session.RequireUser();
        var hasCategoryFilter = filter.CategoryIds is { Count: > 0 };
        var categoryFilter = hasCategoryFilter ? filter.CategoryIds : null;

        try
        {
            var totalsT = reviews.GetDailyTotalsAsync(user.UserId, filter.From, filter.To, ct);
            var rowsT = reviews.GetHistoryAsync(user.UserId, filter.From, filter.To, categoryFilter, ct);
            await Task.WhenAll(totalsT, rowsT);

            var totals = await totalsT;
            var rows = await rowsT;
            var rowsByDate = rows
                .GroupBy(r => r.LogDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<HistoryDayDto>();
            foreach (var total in totals.OrderByDescending(t => t.ReviewDate))
            {
                var hasRows = rowsByDate.TryGetValue(total.ReviewDate, out var dayRows) && dayRows.Count > 0;
                if (hasCategoryFilter && !hasRows)
                    continue;

                var items = hasRows
                    ? dayRows!.Select(dto.Impact).ToList()
                    : new List<ActivityImpactDto>();

                // TODO(ARC-02): ganti dengan total.ReviewId jika migrasi 002_daily_totals_reviewid.sql diterapkan.
                var reviewId = hasRows ? dayRows![0].ReviewId : Guid.Empty;

                result.Add(new HistoryDayDto(
                    reviewId,
                    total.ReviewDate,
                    total.TotalCo2SavedKg,
                    total.TotalPointsEarned,
                    items));
            }

            return Result<IReadOnlyList<HistoryDayDto>>.Success(result);
        }
        catch (DataAccessException ex)
        {
            return Result<IReadOnlyList<HistoryDayDto>>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    public async Task<Result<ExportResultDto>> ExportCsvAsync(
        HistoryFilter filter,
        string targetFilePath,
        CancellationToken ct = default)
    {
        var history = await GetHistoryAsync(filter, ct);
        if (!history.IsSuccess)
            return Result<ExportResultDto>.Failure(history.Error, history.Message ?? Messages.For(history.Error));

        try
        {
            var rows = await CsvExporter.WriteAsync(targetFilePath, history.Value!, ct);
            return Result<ExportResultDto>.Success(new ExportResultDto(targetFilePath, rows));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<ExportResultDto>.Failure(ErrorCode.FileError, Messages.FileError);
        }
    }
}
