using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Services;
using Xunit;

namespace EcoQuest.Tests.Services;

public class CsvExporterTests
{
    [Fact]
    public async Task Writes_bom_header_invariant_decimals_and_escapes_quotes()
    {
        var item = new ActivityImpactDto(
            Guid.NewGuid(), 1, "Jalan Kaki / Bersepeda", 1, "Transportasi",
            InputControlType.Numeric, 3m, "km", null, null,
            0.45m, 9, 0.15m, 1.0m, "", "", "Kata \"hebat\", lalu", null);
        var day = new HistoryDayDto(Guid.NewGuid(), new DateOnly(2026, 11, 18), 0.45m, 9, [item]);
        var path = Path.Combine(Path.GetTempPath(), $"ecoquest-{Guid.NewGuid():N}.csv");

        try
        {
            var rows = await CsvExporter.WriteAsync(path, [day], CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal(1, rows);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

            var text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("tanggal,kategori,aktivitas,kuantitas,unit,jam_mulai,jam_selesai,co2_kg,poin,catatan", lines[0]);
            Assert.Equal("2026-11-18,Transportasi,Jalan Kaki / Bersepeda,3,km,,,0.4500,9,\"Kata \"\"hebat\"\", lalu\"", lines[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
