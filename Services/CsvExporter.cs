using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Contracts.Dtos;

namespace EcoQuest.Services;


public static class CsvExporter
{
    private const string Header = "tanggal,kategori,aktivitas,kuantitas,unit,jam_mulai,jam_selesai,co2_kg,poin,catatan";

    
    public static async Task<int> WriteAsync(string path, IEnumerable<HistoryDayDto> days, CancellationToken ct)
    {
        var inv = CultureInfo.InvariantCulture;
        var rows = 0;

        await using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n"
        };
        await writer.WriteLineAsync(Header);

        foreach (var day in days.OrderBy(d => d.ReviewDate))
        {
            foreach (var i in day.Items)
            {
                ct.ThrowIfCancellationRequested();
                var line = string.Join(',',
                    day.ReviewDate.ToString("yyyy-MM-dd", inv),
                    Escape(i.CategoryName),
                    Escape(i.TypeName),
                    i.Quantity.ToString("0.####", inv),
                    Escape(i.UnitLabel),
                    i.StartTime?.ToString("HH:mm", inv) ?? "",
                    i.EndTime?.ToString("HH:mm", inv) ?? "",
                    i.Co2SavedKg.ToString("0.0000", inv),
                    i.PointsEarned.ToString(inv),
                    Escape(i.Note ?? ""));
                await writer.WriteLineAsync(line);
                rows++;
            }
        }

        return rows;
    }

    
    private static string Escape(string value)
        => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
