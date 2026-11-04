using System.IO;
using System.Globalization;
using System.Text;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

public sealed class FakeHistoryService : IHistoryService
{
    private readonly FakeContext _ctx;
    private readonly FakeStore _store;

    public FakeHistoryService(FakeContext ctx, FakeStore store)
    {
        _ctx = ctx; _store = store;
    }

    public async Task<Result<IReadOnlyList<HistoryDayDto>>> GetHistoryAsync(HistoryFilter filter, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("history")) return _ctx.NetworkFailure<IReadOnlyList<HistoryDayDto>>();
        return Result<IReadOnlyList<HistoryDayDto>>.Success(Apply(filter));
    }

    public async Task<Result<ExportResultDto>> ExportCsvAsync(HistoryFilter filter, string targetFilePath, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);

        // Format per SRS FR-5.4 (D-13): UTF-8 BOM, comma separator, invariant numbers.
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("tanggal,kategori,aktivitas,kuantitas,unit,jam_mulai,jam_selesai,co2_kg,poin,catatan\r\n");
        var rows = 0;
        foreach (var day in Apply(filter).OrderBy(d => d.ReviewDate))
            foreach (var i in day.Items)
            {
                rows++;
                csv.Append(string.Join(',',
                    day.ReviewDate.ToString("yyyy-MM-dd", inv), Quote(i.CategoryName), Quote(i.TypeName),
                    i.Quantity.ToString("0.####", inv), i.UnitLabel,
                    i.StartTime?.ToString("HH:mm", inv) ?? "", i.EndTime?.ToString("HH:mm", inv) ?? "",
                    i.Co2SavedKg.ToString("0.0000", inv), i.PointsEarned.ToString(inv), Quote(i.Note ?? ""))).Append("\r\n");
            }

        try
        {
            await File.WriteAllTextAsync(targetFilePath, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Result<ExportResultDto>.Failure(ErrorCode.FileError, FakeMessages.FileError);
        }
        return Result<ExportResultDto>.Success(new ExportResultDto(targetFilePath, rows));
    }

    private IReadOnlyList<HistoryDayDto> Apply(HistoryFilter filter)
    {
        var categories = filter.CategoryIds ?? [];
        var result = new List<HistoryDayDto>();
        foreach (var day in _store.Days.OrderByDescending(d => d.ReviewDate))
        {
            if (filter.From is { } from && day.ReviewDate < from) continue;
            if (filter.To is { } to && day.ReviewDate > to) continue;

            if (categories.Count == 0) { result.Add(day); continue; }

            var items = day.Items.Where(i => categories.Contains(i.CategoryId)).ToList();
            if (items.Count > 0)
                result.Add(day with { Items = items, TotalCo2Kg = items.Sum(i => i.Co2SavedKg), TotalPoints = items.Sum(i => i.PointsEarned) });
        }
        return result;
    }

    private static string Quote(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
