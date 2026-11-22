using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>Ekspor CSV di Riwayat (S-07, UIR-64, TC-47): command, tombol, filter aktif, dan berkas yang ditulis fake.</summary>
public class HistoryExportTests
{
    private static readonly DateTimeOffset Evening = new(2026, 10, 24, 13, 0, 0, TimeSpan.Zero);   // 24 Okt 2026, 20:00 WIB
    private static readonly DateOnly Today = new(2026, 10, 24);

    private static readonly Dictionary<string, string> Strings = new()
    {
        ["History.Export"] = "Ekspor CSV",
        ["History.Exporting"] = "Mengekspor…",
        ["History.ExportDone"] = "Riwayat diekspor ({0} baris)",
        ["History.RangeError"] = "Tanggal awal harus sebelum tanggal akhir",
        ["History.Points"] = "{0} poin",
        ["History.DayAutomation"] = "{0}, total {1}",
        ["History.NoActivities"] = "Refleksi tanpa aktivitas tercatat.",
    };

    private readonly IHistoryService _history = Substitute.For<IHistoryService>();
    private readonly IReferenceDataService _reference = Substitute.For<IReferenceDataService>();
    private readonly IPhotoStorageService _photos = Substitute.For<IPhotoStorageService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IUiText _text = Substitute.For<IUiText>();

    private static HistoryDayDto Day(DateOnly date) => new(Guid.NewGuid(), date, 0m, 0, []);

    private HistoryViewModel Create(IReadOnlyList<HistoryDayDto>? days = null)
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
        _reference.GetCategoriesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<ActivityCategoryDto>>.Success(
            [new ActivityCategoryDto(1, "Transportasi", null), new ActivityCategoryDto(2, "Energi", null)]));
        _reference.GetActivityTypesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<ActivityTypeDto>>.Success([]));
        _photos.ResolveAbsolutePath(Arg.Any<string?>()).Returns((string?)null);
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HistoryDayDto>>.Success(days ?? [Day(Today.AddDays(-1))]));
        return new HistoryViewModel(_history, _reference, _photos, _dialogs, new FakeClock(Evening), Substitute.For<INavigationService>(), _text, TimeSpan.Zero);
    }

    private static async Task<HistoryViewModel> Opened(HistoryViewModel vm)
    {
        await vm.OnNavigatedToAsync(null);
        return vm;
    }

    private void ExportReturns(Result<ExportResultDto> result) =>
        _history.ExportCsvAsync(Arg.Any<HistoryFilter>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task Suggested_file_name_uses_the_clock_date()
    {
        var vm = await Opened(Create());
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns((string?)null);

        await vm.ExportCsvCommand.ExecuteAsync(null);

        _dialogs.Received(1).PickCsvSaveLocation("ecoquest-riwayat-20261024.csv");
    }

    [Fact]
    public async Task Cancelling_the_dialog_does_nothing()
    {
        var vm = await Opened(Create());
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns((string?)null);

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await _history.DidNotReceive().ExportCsvAsync(Arg.Any<HistoryFilter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _dialogs.DidNotReceive().ShowToast(Arg.Any<string>());
        Assert.Null(vm.ExportError);
        Assert.False(vm.IsExporting);
    }

    [Fact]
    public async Task Success_shows_a_toast_with_the_row_count_from_the_service()
    {
        var vm = await Opened(Create());
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns(@"C:\x\out.csv");
        ExportReturns(Result<ExportResultDto>.Success(new ExportResultDto(@"C:\x\out.csv", 1234)));

        await vm.ExportCsvCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowToast("Riwayat diekspor (1.234 baris)");
        Assert.Null(vm.ExportError);
        Assert.False(vm.IsExporting);
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    [Fact]
    public async Task Failure_shows_the_result_message_in_the_banner_and_no_toast()
    {
        var vm = await Opened(Create());
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns(@"C:\x\out.csv");
        ExportReturns(Result<ExportResultDto>.Failure(ErrorCode.FileError, "Berkas tidak dapat dibaca atau disimpan"));

        await vm.ExportCsvCommand.ExecuteAsync(null);

        Assert.Equal("Berkas tidak dapat dibaca atau disimpan", vm.ExportError);
        _dialogs.DidNotReceive().ShowToast(Arg.Any<string>());
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.IsExporting);
    }

    [Fact]
    public async Task A_later_success_clears_the_banner()
    {
        var vm = await Opened(Create());
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns(@"C:\x\out.csv");
        _history.ExportCsvAsync(Arg.Any<HistoryFilter>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            Result<ExportResultDto>.Failure(ErrorCode.NetworkError, "gagal"),
            Result<ExportResultDto>.Success(new ExportResultDto(@"C:\x\out.csv", 3)));

        await vm.ExportCsvCommand.ExecuteAsync(null);
        await vm.ExportCsvCommand.ExecuteAsync(null);

        Assert.Null(vm.ExportError);
        _dialogs.Received(1).ShowToast("Riwayat diekspor (3 baris)");
    }

    [Fact]
    public async Task Export_sends_the_active_filter_not_all_data()
    {
        var vm = await Opened(Create());
        vm.Categories[1].IsSelected = true;
        await vm.PendingLoad;
        vm.Period = HistoryPeriod.Week;
        await vm.PendingLoad;
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns(@"C:\x\out.csv");
        ExportReturns(Result<ExportResultDto>.Success(new ExportResultDto(@"C:\x\out.csv", 1)));

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await _history.Received(1).ExportCsvAsync(
            Arg.Is<HistoryFilter>(f => f.From == Today.AddDays(-6) && f.To == Today && f.CategoryIds.SequenceEqual(new[] { 2 })),
            @"C:\x\out.csv", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Button_is_enabled_only_with_loaded_rows_and_a_valid_range()
    {
        var vm = Create();
        Assert.False(vm.ExportCsvCommand.CanExecute(null));   // idle / loading

        await vm.OnNavigatedToAsync(null);
        Assert.True(vm.ExportCsvCommand.CanExecute(null));

        vm.From = Today;
        vm.To = Today.AddDays(-5);
        Assert.False(vm.ExportCsvCommand.CanExecute(null));   // invalid range
        vm.From = Today.AddDays(-5);
        vm.To = Today;
        await vm.PendingLoad;
        Assert.True(vm.ExportCsvCommand.CanExecute(null));
    }

    [Fact]
    public async Task Button_is_disabled_while_loading_when_empty_and_after_a_load_error()
    {
        var loading = Create();
        var gate = new TaskCompletionSource<Result<IReadOnlyList<HistoryDayDto>>>();
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var pending = loading.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Loading, loading.State);
        Assert.False(loading.ExportCsvCommand.CanExecute(null));
        gate.SetResult(Result<IReadOnlyList<HistoryDayDto>>.Success([]));
        await pending;
        Assert.True(loading.IsEmpty);
        Assert.False(loading.ExportCsvCommand.CanExecute(null));

        var failing = Create();
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HistoryDayDto>>.Failure(ErrorCode.NetworkError, "offline"));
        await failing.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, failing.State);
        Assert.False(failing.ExportCsvCommand.CanExecute(null));
    }

    [Fact]
    public async Task While_exporting_the_button_says_so_and_cannot_be_triggered_again()
    {
        var vm = await Opened(Create());
        var gate = new TaskCompletionSource<Result<ExportResultDto>>();
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns(@"C:\x\out.csv");
        _history.ExportCsvAsync(Arg.Any<HistoryFilter>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        Assert.Equal("Ekspor CSV", vm.ExportButtonText);

        var first = vm.ExportCsvCommand.ExecuteAsync(null);
        Assert.True(vm.IsExporting);
        Assert.Equal("Mengekspor…", vm.ExportButtonText);
        Assert.False(vm.ExportCsvCommand.CanExecute(null));
        var second = vm.ExportCsvCommand.ExecuteAsync(null);   // second trigger must not start another export

        gate.SetResult(Result<ExportResultDto>.Success(new ExportResultDto(@"C:\x\out.csv", 2)));
        await Task.WhenAll(first, second);

        await _history.Received(1).ExportCsvAsync(Arg.Any<HistoryFilter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _dialogs.Received(1).PickCsvSaveLocation(Arg.Any<string>());
        Assert.Equal("Ekspor CSV", vm.ExportButtonText);
        Assert.True(vm.ExportCsvCommand.CanExecute(null));
    }

    // ---- FakeHistoryService ---------------------------------------------------------------------------------------

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"ecoquest-test-{Guid.NewGuid():N}.csv");

    [Fact]
    public async Task Fake_writes_a_bom_csv_with_one_row_per_activity_of_the_filter()
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var filter = new HistoryFilter(world.Clock.Today.AddDays(-6), world.Clock.Today, []);
        var expected = (await world.History.GetHistoryAsync(filter)).Value!.Sum(d => d.Items.Count);
        var path = TempFile();
        try
        {
            var result = await world.History.ExportCsvAsync(filter, path);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value!.RowCount);
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).ToArray());
            var lines = (await File.ReadAllTextAsync(path)).TrimEnd().Split("\r\n");
            Assert.Equal("tanggal,kategori,aktivitas,kuantitas,unit,jam_mulai,jam_selesai,co2_kg,poin,catatan", lines[0]);
            Assert.Equal(expected + 1, lines.Length);
            Assert.True(expected > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Fake_filter_by_category_writes_fewer_rows_than_all()
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var all = new HistoryFilter(null, null, []);
        var one = new HistoryFilter(null, null, [1]);
        var a = TempFile();
        var b = TempFile();
        try
        {
            var everything = (await world.History.ExportCsvAsync(all, a)).Value!.RowCount;
            var filtered = (await world.History.ExportCsvAsync(one, b)).Value!.RowCount;
            Assert.InRange(filtered, 1, everything - 1);
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }

    [Fact]
    public async Task Fake_empty_scenario_writes_only_the_header()
    {
        var world = new FakeWorld(FakeScenario.Empty);
        var path = TempFile();
        try
        {
            var result = await world.History.ExportCsvAsync(new HistoryFilter(null, null, []), path);

            Assert.Equal(0, result.Value!.RowCount);
            Assert.Single((await File.ReadAllTextAsync(path)).TrimEnd().Split("\r\n"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Fake_errors_scenario_fails_the_first_export_then_succeeds()
    {
        var world = new FakeWorld(FakeScenario.Errors);
        var filter = new HistoryFilter(null, null, []);
        var path = TempFile();
        try
        {
            var first = await world.History.ExportCsvAsync(filter, path);
            Assert.Equal(ErrorCode.NetworkError, first.Error);
            Assert.False(File.Exists(path));

            var second = await world.History.ExportCsvAsync(filter, path);
            Assert.True(second.IsSuccess);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
