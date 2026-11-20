using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EcoQuest.Tests.Services;

public class HistoryServiceTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IDailyReviewRepository _reviews = Substitute.For<IDailyReviewRepository>();
    private readonly DtoFactory _dtoFactory = new(new ImpactCalculatorFactory(new IImpactCalculator[]
    {
        new DistanceImpactCalculator(),
        new DurationImpactCalculator(),
        new CountImpactCalculator(),
        new ToggleImpactCalculator()
    }));
    private readonly UserSession _activeUser = new(Guid.NewGuid(), "dinda_21", "dinda@mail.ugm.ac.id", "Yogyakarta", null);

    public HistoryServiceTests()
    {
        _session.RequireUser().Returns(_activeUser);
        _session.Current.Returns(_activeUser);
    }

    private HistoryService CreateSut()
        => new(_session, _reviews, _dtoFactory);

    private static HistoryRow MakeRow(
        DateOnly date,
        Guid reviewId,
        int categoryId = 1,
        string categoryName = "Transportasi",
        decimal quantity = 3m,
        decimal co2SavedKg = 0.45m,
        int pointsEarned = 9,
        decimal co2Coeff = 0.15m,
        decimal pointFactor = 1.0m)
        => new(
            Guid.NewGuid(),
            reviewId,
            date,
            categoryId == 2 ? 3 : 1,
            categoryId == 2 ? "Mematikan Lampu" : "Jalan Kaki / Bersepeda",
            categoryId,
            categoryName,
            InputControlType.Numeric,
            "km",
            quantity,
            null,
            null,
            co2SavedKg,
            pointsEarned,
            co2Coeff,
            pointFactor,
            null,
            null);

    [Fact]
    public async Task No_filter_includes_empty_reflection_days()
    {
        var d1 = new DateOnly(2026, 11, 18);
        var d2 = new DateOnly(2026, 11, 19); // refleksi kosong tanpa aktivitas
        var d3 = new DateOnly(2026, 11, 20);
        var r1 = Guid.NewGuid();
        var r3 = Guid.NewGuid();

        _reviews.GetDailyTotalsAsync(_activeUser.UserId, null, null, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new DailyTotalRow(d1, 0.45m, 9),
                    new DailyTotalRow(d2, 0m, 0),
                    new DailyTotalRow(d3, 0.60m, 14)
                ]);
        _reviews.GetHistoryAsync(_activeUser.UserId, null, null, null, Arg.Any<CancellationToken>())
                .Returns([MakeRow(d1, r1), MakeRow(d3, r3, quantity: 4m, co2SavedKg: 0.60m, pointsEarned: 14)]);

        var res = await CreateSut().GetHistoryAsync(new HistoryFilter(null, null, []));

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value!.Count);

        var emptyDay = res.Value[1];
        Assert.Equal(d2, emptyDay.ReviewDate);
        Assert.Equal(Guid.Empty, emptyDay.ReviewId);
        Assert.Empty(emptyDay.Items);
        Assert.Equal(0m, emptyDay.TotalCo2Kg);
    }

    [Fact]
    public async Task Category_filter_hides_days_without_match()
    {
        var d1 = new DateOnly(2026, 11, 18);
        var d2 = new DateOnly(2026, 11, 19);
        var r2 = Guid.NewGuid();

        _reviews.GetDailyTotalsAsync(_activeUser.UserId, null, null, Arg.Any<CancellationToken>())
                .Returns([new DailyTotalRow(d1, 0.45m, 9), new DailyTotalRow(d2, 0.30m, 5)]);
        _reviews.GetHistoryAsync(_activeUser.UserId, null, null, Arg.Is<IReadOnlyList<int>>(c => c != null && c.Count == 1 && c[0] == 2), Arg.Any<CancellationToken>())
                .Returns([MakeRow(d2, r2, categoryId: 2, categoryName: "Energi", quantity: 2m, co2SavedKg: 0.30m, pointsEarned: 5)]);

        var res = await CreateSut().GetHistoryAsync(new HistoryFilter(null, null, [2]));

        Assert.True(res.IsSuccess);
        var onlyDay = Assert.Single(res.Value!);
        Assert.Equal(d2, onlyDay.ReviewDate);
        Assert.Equal(r2, onlyDay.ReviewId);
        Assert.Single(onlyDay.Items);
    }

    [Fact]
    public async Task From_after_To_is_validation_error()
    {
        var filter = new HistoryFilter(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 10), []);

        var res = await CreateSut().GetHistoryAsync(filter);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.Equal("Tanggal awal harus sebelum tanggal akhir", res.Message);
        Assert.Equal("Tanggal awal harus sebelum tanggal akhir", res.FieldErrors["From"]);
        await _reviews.DidNotReceiveWithAnyArgs().GetDailyTotalsAsync(default, default, default, default);
        await _reviews.DidNotReceiveWithAnyArgs().GetHistoryAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task Orders_days_descending_newest_first()
    {
        var dOld = new DateOnly(2026, 11, 10);
        var dMid = new DateOnly(2026, 11, 15);
        var dNew = new DateOnly(2026, 11, 20);

        _reviews.GetDailyTotalsAsync(_activeUser.UserId, null, null, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new DailyTotalRow(dOld, 0.45m, 9),
                    new DailyTotalRow(dNew, 0.45m, 9),
                    new DailyTotalRow(dMid, 0.45m, 9)
                ]);
        _reviews.GetHistoryAsync(_activeUser.UserId, null, null, null, Arg.Any<CancellationToken>())
                .Returns([MakeRow(dOld, Guid.NewGuid()), MakeRow(dMid, Guid.NewGuid()), MakeRow(dNew, Guid.NewGuid())]);

        var res = await CreateSut().GetHistoryAsync(new HistoryFilter(null, null, []));

        Assert.True(res.IsSuccess);
        Assert.Equal([dNew, dMid, dOld], [res.Value![0].ReviewDate, res.Value[1].ReviewDate, res.Value[2].ReviewDate]);
    }

    [Fact]
    public async Task Uses_snapshot_coefficients_in_explanations()
    {
        var date = new DateOnly(2026, 11, 19);
        var reviewId = Guid.NewGuid();
        _reviews.GetDailyTotalsAsync(_activeUser.UserId, null, null, Arg.Any<CancellationToken>())
                .Returns([new DailyTotalRow(date, 0.75m, 18)]);
        _reviews.GetHistoryAsync(_activeUser.UserId, null, null, null, Arg.Any<CancellationToken>())
                .Returns([MakeRow(date, reviewId, quantity: 3m, co2SavedKg: 0.75m, pointsEarned: 18, co2Coeff: 0.25m, pointFactor: 1.2m)]);

        var res = await CreateSut().GetHistoryAsync(new HistoryFilter(null, null, []));

        Assert.True(res.IsSuccess);
        var item = res.Value![0].Items[0];
        Assert.Equal("3,00 km × 0,25 kg CO₂/km = 0,75 kg CO₂", item.FormulaExplanation);
        Assert.Equal("0,75 × 20 × 1,2 = 18 poin", item.PointsExplanation);
    }

    [Fact]
    public async Task ExportCsvAsync_returns_not_available_stub()
    {
        var res = await CreateSut().ExportCsvAsync(new HistoryFilter(null, null, []), "C:\\temp\\export.csv");

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.Unknown, res.Error);
        Assert.Equal("Ekspor CSV belum tersedia", res.Message);
    }

    [Fact]
    public async Task DataAccessException_returns_NetworkError_failure()
    {
        _reviews.GetDailyTotalsAsync(_activeUser.UserId, null, null, Arg.Any<CancellationToken>())
                .Throws(new DataAccessException(ErrorCode.NetworkError, "app_get_daily_totals"));

        var res = await CreateSut().GetHistoryAsync(new HistoryFilter(null, null, []));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, res.Error);
        Assert.Equal(Messages.NetworkError, res.Message);
    }

    [Fact]
    public void BackendRegistration_resolves_IHistoryService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        Assert.IsType<HistoryService>(sp.GetRequiredService<IHistoryService>());
    }
}
