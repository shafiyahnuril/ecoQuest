using System.ComponentModel;
using System.Diagnostics;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace EcoQuest.Tests.ViewModels;

/// <summary>HistoryViewModel (S-07): period chips and dates, range validation, debounce, last-load-wins, selection, optional extras, 120-day load.</summary>
public class HistoryViewModelTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset Evening = new(2026, 10, 24, 13, 0, 0, TimeSpan.Zero);   // Saturday 24 Oct 2026, 20:00 WIB
    private static readonly DateOnly Today = new(2026, 10, 24);
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(40);

    private static readonly Dictionary<string, string> Strings = new()
    {
        ["History.Points"] = "{0} poin",
        ["History.NoActivities"] = "Refleksi tanpa aktivitas tercatat.",
        ["History.DayAutomation"] = "{0}, total {1}",
        ["History.RangeError"] = "Tanggal awal harus sebelum tanggal akhir",
        ["Switch.Yes"] = "Ya",
    };

    private readonly IHistoryService _history = Substitute.For<IHistoryService>();
    private readonly IReferenceDataService _reference = Substitute.For<IReferenceDataService>();
    private readonly IPhotoStorageService _photos = Substitute.For<IPhotoStorageService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly List<HistoryFilter> _filters = [];

    private static ActivityImpactDto Impact(int typeId = 1, string? note = null, string? photo = null) => new(
        Guid.NewGuid(), typeId, $"Aktivitas {typeId}", 1, "Transportasi", InputControlType.Numeric, 3m, "km", null, null,
        0.45m, 9, 0.15m, 1m, "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", "0,45 × 20 × 1,0 = 9 poin", note, photo);

    private static HistoryDayDto Day(DateOnly date, params ActivityImpactDto[] items) =>
        new(Guid.NewGuid(), date, items.Sum(i => i.Co2SavedKg), items.Sum(i => i.PointsEarned), items);

    private static HistoryDayDto DayWithId(Guid id, DateOnly date, params ActivityImpactDto[] items) => Day(date, items) with { ReviewId = id };

    private static Result<IReadOnlyList<HistoryDayDto>> Ok(params HistoryDayDto[] days) =>
        Result<IReadOnlyList<HistoryDayDto>>.Success(days);

    private HistoryViewModel Create(TimeSpan? debounce = null)
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
        _reference.GetCategoriesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<ActivityCategoryDto>>.Success(
            [new ActivityCategoryDto(1, "Transportasi", null), new ActivityCategoryDto(2, "Energi", null)]));
        _reference.GetActivityTypesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<ActivityTypeDto>>.Success([]));
        _photos.ResolveAbsolutePath(Arg.Any<string?>()).Returns((string?)null);
        return new HistoryViewModel(_history, _reference, _photos, _dialogs, new FakeClock(Evening), _nav, _text, debounce ?? Debounce);
    }

    private void ReturnsDays(params HistoryDayDto[] days) =>
        _history.GetHistoryAsync(Arg.Do<HistoryFilter>(_filters.Add), Arg.Any<CancellationToken>()).Returns(Ok(days));

    [Fact]
    public async Task Opening_loads_the_last_30_days_and_selects_the_first_day()
    {
        ReturnsDays(Day(Today.AddDays(-1), Impact()), Day(Today.AddDays(-2), Impact()));
        var vm = Create();

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.IsEmpty);
        Assert.Equal(HistoryPeriod.Month, vm.Period);
        Assert.Equal(Today.AddDays(-29), _filters.Single().From);
        Assert.Equal(Today, _filters.Single().To);
        Assert.Equal(2, vm.Days.Count);
        Assert.Same(vm.Days[0], vm.SelectedDay);
        Assert.Equal("Jumat, 23 Oktober 2026", vm.SelectedDay!.DateText);
    }

    [Fact]
    public async Task No_days_means_empty_state_and_no_selection()
    {
        ReturnsDays();
        var vm = Create();

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.SelectedDay);
    }

    [Fact]
    public async Task Failed_load_shows_the_error_and_retry_recovers()
    {
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HistoryDayDto>>.Failure(ErrorCode.NetworkError, "Tidak dapat terhubung"), Ok(Day(Today, Impact())));
        var vm = Create();

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("Tidak dapat terhubung", vm.ErrorMessage);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Days);
    }

    [Fact]
    public async Task Period_chips_fill_the_dates_and_All_clears_them()
    {
        ReturnsDays();
        var vm = Create();
        await vm.OnNavigatedToAsync(null);

        vm.SetPeriodCommand.Execute(HistoryPeriod.Week);
        await vm.PendingLoad;
        Assert.Equal(Today.AddDays(-6), vm.From);
        Assert.Equal(Today, vm.To);
        Assert.Equal(HistoryPeriod.Week, vm.Period);

        vm.SetPeriodCommand.Execute(HistoryPeriod.All);
        await vm.PendingLoad;
        Assert.Null(vm.From);
        Assert.Null(vm.To);
        Assert.Null(_filters[^1].From);
        Assert.Null(_filters[^1].To);
    }

    [Fact]
    public async Task Editing_a_date_releases_the_chip_and_a_chip_overwrites_the_dates_again()
    {
        ReturnsDays();
        var vm = Create();
        await vm.OnNavigatedToAsync(null);

        vm.From = Today.AddDays(-10);
        await vm.PendingLoad;
        Assert.Equal(HistoryPeriod.Custom, vm.Period);
        Assert.Equal(Today.AddDays(-10), _filters[^1].From);

        vm.SetPeriodCommand.Execute(HistoryPeriod.Month);
        await vm.PendingLoad;
        Assert.Equal(HistoryPeriod.Month, vm.Period);
        Assert.Equal(Today.AddDays(-29), vm.From);
    }

    [Fact]
    public async Task Start_after_end_shows_the_range_error_without_calling_the_service()
    {
        ReturnsDays(Day(Today, Impact()));
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        var calls = _filters.Count;

        vm.From = Today.AddDays(1);
        await vm.PendingLoad;

        Assert.Equal("Tanggal awal harus sebelum tanggal akhir", vm.RangeError);
        Assert.True(vm.HasRangeError);
        Assert.Equal(calls, _filters.Count);

        vm.From = Today.AddDays(-3);
        await vm.PendingLoad;
        Assert.Null(vm.RangeError);
        Assert.Equal(calls + 1, _filters.Count);
    }

    [Fact]
    public async Task Service_range_error_is_shown_under_the_dates_and_keeps_the_list()
    {
        ReturnsDays(Day(Today, Impact()));
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<HistoryDayDto>>.Failure(ErrorCode.ValidationFailed, "x", new Dictionary<string, string> { ["From"] = "Rentang tidak valid" }));

        vm.SetPeriodCommand.Execute(HistoryPeriod.Week);
        await vm.PendingLoad;

        Assert.Equal("Rentang tidak valid", vm.RangeError);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Single(vm.Days);
    }

    [Fact]
    public async Task Rapid_changes_are_debounced_into_one_call()
    {
        ReturnsDays();
        var vm = Create(TimeSpan.FromMilliseconds(120));
        await vm.OnNavigatedToAsync(null);
        var calls = _filters.Count;

        vm.SetPeriodCommand.Execute(HistoryPeriod.Week);
        vm.SetPeriodCommand.Execute(HistoryPeriod.All);
        vm.Categories[0].IsSelected = true;
        await vm.PendingLoad;

        Assert.Equal(calls + 1, _filters.Count);
        Assert.Null(_filters[^1].From);
        Assert.Equal([1], _filters[^1].CategoryIds);
    }

    [Fact]
    public async Task Last_change_wins_and_the_list_never_flickers()
    {
        var oldDay = Day(Today.AddDays(-5), Impact());
        var newDay = Day(Today.AddDays(-2), Impact());
        ReturnsDays(Day(Today.AddDays(-1), Impact()));
        var vm = Create(TimeSpan.Zero);
        await vm.OnNavigatedToAsync(null);

        var states = new List<LoadState>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HistoryViewModel.State))
            {
                states.Add(vm.State);
            }
        };

        _history.GetHistoryAsync(Arg.Is<HistoryFilter>(f => f.To == null), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(300, call.Arg<CancellationToken>());
            return Ok(oldDay);
        });
        _history.GetHistoryAsync(Arg.Is<HistoryFilter>(f => f.To != null), Arg.Any<CancellationToken>()).Returns(Ok(newDay));

        vm.SetPeriodCommand.Execute(HistoryPeriod.All);
        await Task.Delay(50);
        vm.SetPeriodCommand.Execute(HistoryPeriod.Week);
        await vm.PendingLoad;
        await Task.Delay(350);

        Assert.Equal(UiFormat.LongDate(Today.AddDays(-2)), vm.Days.Single().DateText);
        Assert.All(states, s => Assert.Equal(LoadState.Loaded, s));
    }

    [Fact]
    public async Task Selecting_a_day_exposes_its_detail_lines()
    {
        ReturnsDays(Day(Today.AddDays(-1), Impact(1, note: "Pagi hari")), Day(Today.AddDays(-2), Impact(2), Impact(3)));
        var vm = Create();
        await vm.OnNavigatedToAsync(null);

        vm.SelectedDay = vm.Days[1];

        var lines = vm.SelectedDay!.Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal("0,45 kg CO₂", lines[0].DetailCo2Text);
        Assert.Equal("3,00 km", lines[0].QuantityText);
        Assert.Contains("0,15 kg CO₂/km", lines[0].FormulaText);
        Assert.Contains("9 poin", lines[0].FormulaText);
        Assert.Equal("18 poin", vm.SelectedDay.PointsText);
        Assert.True(vm.Days[0].Lines[0].HasNote);
        Assert.False(vm.Days[1].Lines[0].HasNote);
    }

    [Fact]
    public async Task Filter_change_keeps_a_selection_that_still_exists()
    {
        var kept = Guid.NewGuid();
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(
            Ok(Day(Today.AddDays(-1), Impact()), DayWithId(kept, Today.AddDays(-2), Impact())),
            Ok(Day(Today.AddDays(-5), Impact()), DayWithId(kept, Today.AddDays(-2), Impact())));
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        vm.SelectedDay = vm.Days[1];

        vm.SetPeriodCommand.Execute(HistoryPeriod.Week);
        await vm.PendingLoad;

        Assert.Equal(kept, vm.SelectedDay?.ReviewId);
    }

    [Fact]
    public async Task Filter_change_selects_the_first_day_when_the_selected_day_is_gone()
    {
        var first = Day(Today.AddDays(-9), Impact());
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(
            Ok(Day(Today.AddDays(-1), Impact()), Day(Today.AddDays(-2), Impact())),
            Ok(first, Day(Today.AddDays(-10), Impact())));
        var vm = Create();
        await vm.OnNavigatedToAsync(null);

        vm.SetPeriodCommand.Execute(HistoryPeriod.All);
        await vm.PendingLoad;

        Assert.Same(vm.Days[0], vm.SelectedDay);
        Assert.Equal(first.ReviewId, vm.SelectedDay!.ReviewId);
    }

    [Fact]
    public async Task Filter_change_with_no_result_clears_the_selection()
    {
        _history.GetHistoryAsync(Arg.Any<HistoryFilter>(), Arg.Any<CancellationToken>()).Returns(Ok(Day(Today.AddDays(-1), Impact())), Ok());
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        Assert.NotNull(vm.SelectedDay);

        vm.SetPeriodCommand.Execute(HistoryPeriod.Week);
        await vm.PendingLoad;

        Assert.True(vm.IsEmpty);
        Assert.Null(vm.SelectedDay);
    }

    [Fact]
    public async Task Failing_to_load_categories_does_not_fail_the_screen()
    {
        ReturnsDays(Day(Today, Impact()));
        var vm = Create();
        _reference.GetCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ActivityCategoryDto>>.Failure(ErrorCode.NetworkError, "gagal"));

        await vm.OnNavigatedToAsync(null);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.HasCategories);
        Assert.Single(vm.Days);
    }

    [Fact]
    public async Task Category_chips_filter_and_All_clears_them()
    {
        ReturnsDays();
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        Assert.True(vm.HasCategories);
        Assert.True(vm.AllCategoriesSelected);

        vm.Categories[1].IsSelected = true;
        await vm.PendingLoad;
        Assert.False(vm.AllCategoriesSelected);
        Assert.Equal([2], _filters[^1].CategoryIds);

        vm.SelectAllCategoriesCommand.Execute(null);
        await vm.PendingLoad;
        Assert.True(vm.AllCategoriesSelected);
        Assert.Empty(_filters[^1].CategoryIds);
    }

    [Fact]
    public async Task Long_activity_description_goes_to_the_line_for_the_tooltip()
    {
        ReturnsDays(Day(Today, Impact(1), Impact(2)));
        var vm = Create();
        _reference.GetActivityTypesAsync(Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<ActivityTypeDto>>.Success(
            [new ActivityTypeDto(1, "Jalan kaki", 1, "Transportasi", InputControlType.Numeric, "km", 0.15m, 1m, null, 50m, "Asumsi: menggantikan motor.", 1)]));

        await vm.OnNavigatedToAsync(null);

        var lines = vm.Days[0].Lines;
        Assert.True(lines[0].HasDescription);
        Assert.Equal("Asumsi: menggantikan motor.", lines[0].Description);
        Assert.False(lines[1].HasDescription);
    }

    [Fact]
    public async Task Photo_is_resolved_lazily_and_a_missing_file_is_not_an_error()
    {
        ReturnsDays(Day(Today, Impact(1, photo: "photos/a.jpg"), Impact(2)));
        var vm = Create();
        await vm.OnNavigatedToAsync(null);
        _photos.ClearReceivedCalls();

        var line = vm.Days[0].Lines[0];
        Assert.True(line.HasPhoto);
        Assert.False(vm.Days[0].Lines[1].HasPhoto);
        Assert.Empty(_photos.ReceivedCalls());

        Assert.Null(line.PhotoFilePath);
        Assert.Null(line.PhotoFilePath);
        _photos.Received(1).ResolveAbsolutePath("photos/a.jpg");
    }

    [Fact]
    public async Task Day_without_activities_has_no_lines_and_its_own_text()
    {
        ReturnsDays(Day(Today.AddDays(-1)));
        var vm = Create();

        await vm.OnNavigatedToAsync(null);

        Assert.False(vm.Days[0].HasLines);
        Assert.Equal("Refleksi tanpa aktivitas tercatat.", vm.Days[0].EmptyText);
        Assert.Equal("Jumat, 23 Oktober 2026, total 0 kg CO₂", vm.Days[0].AutomationName);
    }

    [Fact]
    public async Task Empty_state_action_opens_the_evening_review()
    {
        ReturnsDays();
        var vm = Create();
        await vm.OnNavigatedToAsync(null);

        await vm.GoToReviewCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<EveningReviewViewModel>();
    }

    [Fact]
    public async Task Loading_120_days_builds_quickly()
    {
        var days = Enumerable.Range(1, 120)
            .Select(i => Day(Today.AddDays(-i), Impact(1), Impact(2), Impact(3, note: "x", photo: "p.jpg"), Impact(4), Impact(5)))
            .ToArray();
        ReturnsDays(days);
        var vm = Create();

        var watch = Stopwatch.StartNew();
        await vm.OnNavigatedToAsync(null);
        watch.Stop();

        output.WriteLine($"120 days x 5 lines loaded into the view model in {watch.ElapsedMilliseconds} ms");
        Assert.Equal(120, vm.Days.Count);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds} ms");
        _photos.DidNotReceive().ResolveAbsolutePath(Arg.Any<string?>());
    }
}
