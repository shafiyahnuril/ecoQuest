using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Calculation;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Core.Validation;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using EcoQuest.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Services;

public sealed class EveningReviewServiceTests : IDisposable
{
    private static readonly IReadOnlyList<ActivityType> DefaultTypes =
        [Walk(), Transit(), Lamp(), Sorting(), Tumbler(), Bags()];

    private static readonly IReadOnlyList<ActivityCategory> DefaultCategories =
    [
        new(1, "Transportasi", null),
        new(2, "Energi", null),
        new(3, "Sampah", null),
        new(4, "Konsumsi", null)
    ];

    private static readonly IReadOnlyList<Level> DefaultLevels =
    [
        new(1, "Eco Starter", 0, 99),
        new(2, "Eco Explorer", 100, 499),
        new(3, "Eco Warrior", 500, 1499),
        new(4, "Eco Champion", 1500, null)
    ];

    private static readonly IReadOnlyList<Badge> DefaultBadges =
    [
        new(1, "Langkah Pertama", "Mencatat aktivitas pertama.", CriteriaType.ActivityCount, 1m),
        new(3, "3 Hari Beruntun", "Refleksi 3 hari berturut-turut.", CriteriaType.StreakDays, 3m)
    ];

    private readonly string _tempDir;
    private readonly AppPaths _paths;
    private readonly PhotoStorageService _photos;
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly IActivityTypeRepository _typeRepo = Substitute.For<IActivityTypeRepository>();
    private readonly IActivityCategoryRepository _categoriesRepo = Substitute.For<IActivityCategoryRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDailyReviewRepository _reviews = Substitute.For<IDailyReviewRepository>();
    private readonly ILevelRepository _levelsRepo = Substitute.For<ILevelRepository>();
    private readonly IBadgeRepository _badgesRepo = Substitute.For<IBadgeRepository>();
    private readonly IImpactCalculatorFactory _calcFactory;
    private readonly IImpactCalculationService _impactService;
    private readonly ActivityInputValidator _validator;
    private readonly DtoFactory _dtoFactory;
    private readonly UserSession _activeUser;

    public EveningReviewServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ecoquest-er-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _paths = new AppPaths(_tempDir);
        _photos = new PhotoStorageService(_paths);

        _calcFactory = new ImpactCalculatorFactory(new IImpactCalculator[]
        {
            new DistanceImpactCalculator(),
            new DurationImpactCalculator(),
            new CountImpactCalculator(),
            new ToggleImpactCalculator()
        });
        _impactService = new ImpactCalculationService(_calcFactory);
        _validator = new ActivityInputValidator(_calcFactory);
        _dtoFactory = new DtoFactory(_calcFactory);

        _activeUser = new UserSession(Guid.NewGuid(), "dinda_21", "dinda@mail.ugm.ac.id", "Yogyakarta", null);
        _session.RequireUser().Returns(_activeUser);
        _session.Current.Returns(_activeUser);

        _typeRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultTypes);
        _categoriesRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultCategories);
        _levelsRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultLevels);
        _badgesRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultBadges);
        _badgesRepo.GetUserBadgesAsync(_activeUser.UserId, Arg.Any<CancellationToken>()).Returns([]);

        var domainUser = new User(
            _activeUser.UserId,
            _activeUser.UserName,
            _activeUser.Email,
            _activeUser.City,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(new UserProfileSnapshot(domainUser, null, 0, 0));

        _reviews.SubmitAsync(Arg.Any<ReviewSubmission>(), Arg.Any<CancellationToken>())
                .Returns((Guid.NewGuid(), _clock.UtcNow));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private EveningReviewService CreateSut()
        => new(
            _session,
            _clock,
            _typeRepo,
            _categoriesRepo,
            _validator,
            _impactService,
            _calcFactory,
            _users,
            _reviews,
            _levelsRepo,
            _badgesRepo,
            _photos,
            _dtoFactory);

    private string CreateTempFile(string fileName, int byteCount = 64)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllBytes(path, new byte[byteCount]);
        return path;
    }

    [Fact]
    public async Task Submit_with_errors_returns_ValidationFailed_and_never_calls_repo()
    {
        var sut = CreateSut();
        var req = new SubmitReviewRequest([Qty(Walk(), -2m)], WarningsAcknowledged: false);

        var res = await sut.SubmitAsync(req);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.Equal(Messages.ValidationFailed, res.Message);
        Assert.True(res.FieldErrors.ContainsKey("1.Quantity"));
        await _reviews.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
    }

    [Fact]
    public async Task Submit_with_unacknowledged_warning_returns_WarningsNotAcknowledged()
    {
        var sut = CreateSut();
        var req = new SubmitReviewRequest([Qty(Walk(), 45m)], WarningsAcknowledged: false);

        var res = await sut.SubmitAsync(req);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.WarningsNotAcknowledged, res.Error);
        Assert.Equal(Messages.WarningsNotAcknowledged, res.Message);
        await _reviews.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
    }

    [Fact]
    public async Task Submit_skips_empty_items()
    {
        var sut = CreateSut();
        ReviewSubmission? captured = null;
        await _reviews.SubmitAsync(Arg.Do<ReviewSubmission>(s => captured = s), Arg.Any<CancellationToken>());

        var req = new SubmitReviewRequest(
        [
            Qty(Walk(), 3m),
            ActivityInput.Empty(2),
            ActivityInput.Empty(3),
            Qty(Sorting(), 2m),
            ActivityInput.Empty(5),
            ActivityInput.Empty(6)
        ], WarningsAcknowledged: false);

        var res = await sut.SubmitAsync(req);

        Assert.True(res.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Logs.Count);
        Assert.Equal(2, res.Value!.Items.Count);
    }

    [Fact]
    public async Task Submit_computes_totals_streak_level_badges()
    {
        // User punya 90 poin sehingga tambahan 26 poin (total 116) memicu naik level ke Eco Explorer.
        var userAlmostLevel2 = new User(
            _activeUser.UserId,
            _activeUser.UserName,
            _activeUser.Email,
            _activeUser.City,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            totalPoints: 90,
            totalCo2SavedKg: 5m,
            currentLevelId: 1,
            currentStreak: 2);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(new UserProfileSnapshot(userAlmostLevel2, _clock.Today.AddDays(-1), 2, 2));

        var sut = CreateSut();
        ReviewSubmission? captured = null;
        await _reviews.SubmitAsync(Arg.Do<ReviewSubmission>(s => captured = s), Arg.Any<CancellationToken>());

        // SRS §4.4 baris Σ: jalan 3 km (0.45 / 9) + lampu 21:00–23:30 (0.0325 / 1) + sampah 2 (0.6 / 14) + tumbler Ya (0.1 / 2) = 1.1825 kg & 26 poin.
        var req = new SubmitReviewRequest(
        [
            Qty(Walk(), 3m),
            ActivityInput.Empty(2),
            Time(Lamp(), new TimeOnly(21, 0), new TimeOnly(23, 30)),
            Qty(Sorting(), 2m),
            Yes(Tumbler(), true),
            ActivityInput.Empty(6)
        ], WarningsAcknowledged: false);

        var res = await sut.SubmitAsync(req);

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
        Assert.Equal(1.1825m, res.Value!.TotalCo2SavedKg);
        Assert.Equal(26, res.Value.TotalPointsEarned);
        Assert.Equal(3, res.Value.CurrentStreak);
        Assert.NotNull(res.Value.LevelUp);
        Assert.Equal("Eco Starter", res.Value.LevelUp!.OldLevelName);
        Assert.Equal("Eco Explorer", res.Value.LevelUp.NewLevelName);
        Assert.Contains(res.Value.NewBadges, b => b.Name == "Langkah Pertama");
        Assert.Contains(res.Value.NewBadges, b => b.Name == "3 Hari Beruntun");
        Assert.Contains("menyumbang", res.Value.Insight);

        Assert.NotNull(captured);
        Assert.Equal(1.1825m, captured!.TotalCo2);
        Assert.Equal(26, captured.TotalPoints);
        Assert.Equal(3, captured.NewStreak);
        Assert.Equal(2, captured.NewLevelId);
    }

    [Fact]
    public async Task Submit_maps_REVIEW_ALREADY_SUBMITTED()
    {
        var sut = CreateSut();
        _reviews.SubmitAsync(Arg.Any<ReviewSubmission>(), Arg.Any<CancellationToken>())
                .Throws(new DataAccessException(ErrorCode.ReviewAlreadySubmitted, "app_submit_evening_review"));

        var res = await sut.SubmitAsync(new SubmitReviewRequest([Qty(Walk(), 3m)], WarningsAcknowledged: false));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ReviewAlreadySubmitted, res.Error);
        Assert.Equal(Messages.ReviewAlreadySubmitted, res.Message);
    }

    [Fact]
    public async Task Submit_deletes_copied_photos_on_failure()
    {
        var srcPhoto = CreateTempFile("walk.png");
        var sut = CreateSut();
        _reviews.SubmitAsync(Arg.Any<ReviewSubmission>(), Arg.Any<CancellationToken>())
                .Throws(new DataAccessException(ErrorCode.NetworkError, "app_submit_evening_review"));

        var req = new SubmitReviewRequest([Qty(Walk(), 3m) with { PhotoSourcePath = srcPhoto }], WarningsAcknowledged: false);

        var res = await sut.SubmitAsync(req);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, res.Error);
        var userPhotoDir = Path.Combine(_paths.PhotosRoot, _activeUser.UserId.ToString());
        var remaining = Directory.Exists(userPhotoDir) ? Directory.GetFiles(userPhotoDir) : [];
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Photo_does_not_change_co2()
    {
        var srcPhoto = CreateTempFile("evidence.jpg");
        var sut = CreateSut();

        var withoutPhoto = await sut.SubmitAsync(new SubmitReviewRequest([Qty(Walk(), 3m)], WarningsAcknowledged: false));
        var withPhoto = await sut.SubmitAsync(new SubmitReviewRequest([Qty(Walk(), 3m) with { PhotoSourcePath = srcPhoto }], WarningsAcknowledged: false));

        Assert.True(withoutPhoto.IsSuccess);
        Assert.True(withPhoto.IsSuccess);
        Assert.Equal(withoutPhoto.Value!.TotalCo2SavedKg, withPhoto.Value!.TotalCo2SavedKg);
        Assert.Equal(withoutPhoto.Value.TotalPointsEarned, withPhoto.Value.TotalPointsEarned);
        Assert.Null(withoutPhoto.Value.Items[0].PhotoPath);
        Assert.NotNull(withPhoto.Value.Items[0].PhotoPath);
    }

    [Fact]
    public void Validate_includes_photo_validation_errors()
    {
        var badPhoto = CreateTempFile("bad.gif");
        var sut = CreateSut();
        var typeDtos = DefaultTypes.Select(t => new ActivityTypeDto(
            t.ActivityTypeId, t.TypeName, t.CategoryId, "Transportasi", t.InputControlType, t.UnitLabel,
            t.Co2Coefficient, t.PointFactor, t.PlausibleMaxPerDay, t.HardMaxPerDay, t.Description, t.DisplayOrder)).ToList();

        var result = sut.Validate([Qty(Walk(), 3m) with { PhotoSourcePath = badPhoto }], typeDtos);

        Assert.True(result.HasErrors);
        Assert.False(result.IsEmpty);
        Assert.Contains(result.Issues, i => i.ActivityTypeId == 1 && i.Field == "Photo" && i.Message == "Foto harus berformat JPG atau PNG");
    }

    [Fact]
    public void Preview_returns_null_when_empty_or_invalid_and_dto_when_valid()
    {
        var sut = CreateSut();
        var t = Walk();
        var typeDto = new ActivityTypeDto(
            t.ActivityTypeId, t.TypeName, t.CategoryId, "Transportasi", t.InputControlType, t.UnitLabel,
            t.Co2Coefficient, t.PointFactor, t.PlausibleMaxPerDay, t.HardMaxPerDay, t.Description, t.DisplayOrder);

        Assert.Null(sut.Preview(ActivityInput.Empty(1), typeDto));
        Assert.Null(sut.Preview(Qty(t, -1m), typeDto));

        var valid = sut.Preview(Qty(t, 3m), typeDto);
        Assert.NotNull(valid);
        Assert.Null(valid!.LogId);
        Assert.Equal("Transportasi", valid.CategoryName);
        Assert.Equal(0.45m, valid.Co2SavedKg);
        Assert.Equal(9, valid.PointsEarned);
    }

    [Fact]
    public async Task GetTodayAsync_returns_NotSubmitted_or_rehydrated_Submitted_summary()
    {
        var sut = CreateSut();
        _reviews.GetByDateAsync(_activeUser.UserId, _clock.Today, Arg.Any<CancellationToken>())
                .Returns((DailyReview?)null);

        var notSubmitted = await sut.GetTodayAsync();

        Assert.True(notSubmitted.IsSuccess);
        Assert.Equal(TodayReviewState.NotSubmitted, notSubmitted.Value!.State);
        Assert.Null(notSubmitted.Value.Summary);

        var reviewId = Guid.NewGuid();
        var log = ActivityLog.Rehydrate(
            Guid.NewGuid(), _activeUser.UserId, reviewId, 1, _clock.Today,
            3m, null, null, "Pagi", null, 0.45m, 9, 0.15m, 1.0m);
        var review = DailyReview.Rehydrate(reviewId, _activeUser.UserId, _clock.Today, _clock.UtcNow, [log]);
        _reviews.GetByDateAsync(_activeUser.UserId, _clock.Today, Arg.Any<CancellationToken>())
                .Returns(review);

        var domainUser = new User(
            _activeUser.UserId, _activeUser.UserName, _activeUser.Email, _activeUser.City,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), currentStreak: 4);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(new UserProfileSnapshot(domainUser, _clock.Today, 1, 1));

        var submitted = await sut.GetTodayAsync();

        Assert.True(submitted.IsSuccess);
        Assert.Equal(TodayReviewState.Submitted, submitted.Value!.State);
        Assert.NotNull(submitted.Value.Summary);
        Assert.Equal(reviewId, submitted.Value.Summary!.ReviewId);
        Assert.Equal(0.45m, submitted.Value.Summary.TotalCo2SavedKg);
        Assert.Equal(4, submitted.Value.Summary.CurrentStreak);
        Assert.Null(submitted.Value.Summary.LevelUp);
        Assert.Empty(submitted.Value.Summary.NewBadges);
        Assert.Equal("Transportasi", submitted.Value.Summary.Items[0].CategoryName);
    }

    [Fact]
    public async Task Submit_empty_review_when_acknowledged_succeeds_with_zero_totals()
    {
        var sut = CreateSut();
        var req = new SubmitReviewRequest([ActivityInput.Empty(1), ActivityInput.Empty(5)], WarningsAcknowledged: true);

        var res = await sut.SubmitAsync(req);

        Assert.True(res.IsSuccess);
        Assert.Equal(0m, res.Value!.TotalCo2SavedKg);
        Assert.Equal(0, res.Value.TotalPointsEarned);
        Assert.Empty(res.Value.Items);
        Assert.Contains("Refleksi tetap berharga", res.Value.Insight);
    }

    [Fact]
    public void BackendRegistration_resolves_DtoFactory_and_IEveningReviewService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<DtoFactory>());
        Assert.IsType<EveningReviewService>(sp.GetRequiredService<IEveningReviewService>());
    }
}
