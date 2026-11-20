using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace EcoQuest.Services;

/// <summary>
/// Layanan orkestrasi Evening Review: validasi sinkron, live preview, baca status hari ini, dan submit atomik (SDD §5.2–5.3).
/// </summary>
public sealed class EveningReviewService(
    ISessionService session,
    IClock clock,
    IActivityTypeRepository typeRepo,
    IActivityCategoryRepository categoriesRepo,
    ActivityInputValidator validator,
    IImpactCalculationService impact,
    IImpactCalculatorFactory calcs,
    IUserRepository users,
    IDailyReviewRepository reviews,
    ILevelRepository levelsRepo,
    IBadgeRepository badgesRepo,
    PhotoStorageService photos,
    DtoFactory dto) : IEveningReviewService
{
    private static readonly CultureInfo IdCulture = new("id-ID");

    public ReviewValidationResult Validate(IReadOnlyList<ActivityInput> items, IReadOnlyList<ActivityTypeDto> types)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(types);

        var domainTypes = types.Select(dto.ToDomain).ToList();
        return ValidateInternal(items, domainTypes);
    }

    public ActivityImpactDto? Preview(ActivityInput input, ActivityTypeDto type)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(type);

        var domainType = dto.ToDomain(type);
        var calc = calcs.For(domainType.InputControlType);
        if (calc.ValidateHard(input, domainType).Count > 0 || calc.ResolveQuantity(input) <= 0m)
            return null;

        var result = impact.Calculate(input, domainType);
        return dto.Impact(result, domainType, type.CategoryName);
    }

    public async Task<Result<TodayReviewDto>> GetTodayAsync(CancellationToken ct = default)
    {
        var user = session.RequireUser();
        var today = clock.Today;

        try
        {
            var review = await reviews.GetByDateAsync(user.UserId, today, ct);
            if (review is null)
                return Result<TodayReviewDto>.Success(new TodayReviewDto(TodayReviewState.NotSubmitted, today, null));

            var typesTask = typeRepo.GetAllAsync(ct);
            var catsTask = categoriesRepo.GetAllAsync(ct);
            var profileTask = users.GetProfileAsync(user.UserId, ct);
            await Task.WhenAll(typesTask, catsTask, profileTask);

            var typesById = (await typesTask).ToDictionary(t => t.ActivityTypeId);
            var catsById = (await catsTask).ToDictionary(c => c.CategoryId, c => c.CategoryName);
            var profile = await profileTask ?? throw new DataAccessException(ErrorCode.NotFound, "profile");

            var items = review.Logs
                .Select(l =>
                {
                    var type = typesById[l.ActivityTypeId];
                    var categoryName = catsById.GetValueOrDefault(type.CategoryId, string.Empty);
                    return dto.Impact(l, type, categoryName);
                })
                .ToList();

            var effectiveStreak = StreakCalculator.Effective(
                profile.User.CurrentStreak,
                profile.LastReviewDate,
                today);
            var insight = BuildReviewInsight(items, review.TotalCo2SavedKg);

            var summary = new ReviewSummaryDto(
                review.ReviewId,
                review.ReviewDate,
                review.SubmittedAt ?? clock.UtcNow,
                review.TotalCo2SavedKg,
                review.TotalPointsEarned,
                items,
                insight,
                effectiveStreak,
                LevelUp: null,
                NewBadges: []);

            return Result<TodayReviewDto>.Success(new TodayReviewDto(TodayReviewState.Submitted, today, summary));
        }
        catch (DataAccessException ex)
        {
            return Result<TodayReviewDto>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    public async Task<Result<ReviewSummaryDto>> SubmitAsync(SubmitReviewRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = session.RequireUser();
        var today = clock.Today;
        var copiedPhotos = new List<string>();

        try
        {
            var typesList = await typeRepo.GetAllAsync(ct);
            var typesById = typesList.ToDictionary(t => t.ActivityTypeId);

            var validation = ValidateInternal(request.Items, typesList);
            if (validation.HasErrors)
                return Result<ReviewSummaryDto>.Failure(
                    ErrorCode.ValidationFailed,
                    Messages.ValidationFailed,
                    ToFieldErrors(validation));

            if (validation.HasWarnings && !request.WarningsAcknowledged)
                return Result<ReviewSummaryDto>.Failure(
                    ErrorCode.WarningsNotAcknowledged,
                    Messages.WarningsNotAcknowledged);

            var logs = new List<(ActivityLog Log, ActivityInput Input, ActivityType Type)>();
            foreach (var input in request.Items)
            {
                var type = typesById[input.ActivityTypeId];
                if (calcs.For(type.InputControlType).ResolveQuantity(input) <= 0m)
                    continue;

                var calcResult = impact.Calculate(input, type);
                var log = ActivityLog.Create(
                    user.UserId,
                    today,
                    type.ActivityTypeId,
                    calcResult,
                    calcResult.StartTime,
                    calcResult.EndTime,
                    input.Note?.Trim());
                logs.Add((log, input, type));
            }

            var totalCo2 = logs.Sum(x => x.Log.Co2SavedKg);
            var totalPts = logs.Sum(x => x.Log.PointsEarned);

            var profile = await users.GetProfileAsync(user.UserId, ct)
                ?? throw new DataAccessException(ErrorCode.NotFound, "profile");
            if (profile.LastReviewDate == today)
                return Result<ReviewSummaryDto>.Failure(
                    ErrorCode.ReviewAlreadySubmitted,
                    Messages.ReviewAlreadySubmitted);

            var newStreak = StreakCalculator.Next(profile.User.CurrentStreak, profile.LastReviewDate, today);

            var levels = await levelsRepo.GetAllAsync(ct);
            var oldLevel = LevelResolver.Resolve(profile.User.TotalPoints, levels);
            var newTotalPts = profile.User.TotalPoints + totalPts;
            var newLevel = LevelResolver.Resolve(newTotalPts, levels);

            var allBadges = await badgesRepo.GetAllAsync(ct);
            var earnedIds = (await badgesRepo.GetUserBadgesAsync(user.UserId, ct))
                .Select(b => b.BadgeId)
                .ToHashSet();
            var badgeCtx = new BadgeContext(
                profile.TotalLogCount + logs.Count,
                newStreak,
                profile.User.TotalCo2SavedKg + totalCo2,
                newTotalPts);
            var newBadges = BadgeEvaluator.NewlyEarned(allBadges, earnedIds, badgeCtx);

            var catsById = (await categoriesRepo.GetAllAsync(ct))
                .ToDictionary(c => c.CategoryId, c => c.CategoryName);

            foreach (var (log, input, _) in logs)
            {
                if (string.IsNullOrWhiteSpace(input.PhotoSourcePath))
                    continue;

                var relPath = photos.CopyLogPhoto(input.PhotoSourcePath, user.UserId, log.LogId);
                log.AttachPhoto(relPath);
                copiedPhotos.Add(relPath);
            }

            var (reviewId, submittedAt) = await reviews.SubmitAsync(
                new ReviewSubmission(
                    user.UserId,
                    today,
                    logs.Select(x => x.Log).ToList(),
                    totalCo2,
                    totalPts,
                    newStreak,
                    newLevel.LevelId,
                    newBadges.Select(b => b.BadgeId).ToList()),
                ct);

            var itemsDto = logs
                .Select(x => dto.Impact(x.Log, x.Type, catsById.GetValueOrDefault(x.Type.CategoryId, string.Empty)))
                .ToList();
            var insight = BuildReviewInsight(itemsDto, totalCo2);
            var levelUp = newLevel.LevelId != oldLevel.LevelId
                ? new LevelChangeDto(oldLevel.LevelName, newLevel.LevelName)
                : null;
            var badgeDtos = newBadges
                .Select(b => dto.Badge(b, earned: true, submittedAt, badgeCtx))
                .ToList();

            return Result<ReviewSummaryDto>.Success(new ReviewSummaryDto(
                reviewId,
                today,
                submittedAt,
                totalCo2,
                totalPts,
                itemsDto,
                insight,
                newStreak,
                levelUp,
                badgeDtos));
        }
        catch (DataAccessException ex)
        {
            CleanupCopiedPhotos(copiedPhotos);
            return Result<ReviewSummaryDto>.Failure(ex.Code, Messages.For(ex.Code));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CleanupCopiedPhotos(copiedPhotos);
            return Result<ReviewSummaryDto>.Failure(ErrorCode.FileError, Messages.FileError);
        }
    }

    private ReviewValidationResult ValidateInternal(
        IReadOnlyList<ActivityInput> items,
        IReadOnlyList<ActivityType> domainTypes)
    {
        var baseResult = validator.Validate(items, domainTypes);
        var issues = new List<ValidationIssue>(baseResult.Issues);

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.PhotoSourcePath))
                continue;

            var check = photos.ValidatePhoto(item.PhotoSourcePath);
            if (!check.IsSuccess)
                issues.Add(new ValidationIssue(
                    item.ActivityTypeId,
                    "Photo",
                    ValidationSeverity.Error,
                    check.Message ?? Messages.FileError));
        }

        return new ReviewValidationResult(issues) { IsEmpty = baseResult.IsEmpty };
    }

    private void CleanupCopiedPhotos(IEnumerable<string> copiedPhotos)
    {
        foreach (var rel in copiedPhotos)
            photos.TryDelete(rel);
    }

    private static IReadOnlyDictionary<string, string> ToFieldErrors(ReviewValidationResult v) =>
        v.Issues
            .Where(i => i.Severity == ValidationSeverity.Error)
            .GroupBy(i => $"{i.ActivityTypeId}.{i.Field}")
            .ToDictionary(g => g.Key, g => g.First().Message);

    private static string BuildReviewInsight(IReadOnlyList<ActivityImpactDto> items, decimal totalCo2)
    {
        // TODO(BE-17a): delegasikan ke InsightGenerator.ForReview jika ditambahkan Aurel.
        if (totalCo2 <= 0m || items.Count == 0)
            return "Refleksi tetap berharga meski belum ada aktivitas tercatat hari ini. Coba mulai dari satu langkah kecil besok.";

        var top = items
            .OrderByDescending(i => i.Co2SavedKg)
            .ThenBy(i => i.ActivityTypeId)
            .First();

        return $"Hari ini {top.TypeName} menyumbang {top.Co2SavedKg.ToString("0.##", IdCulture)} kg dari total {totalCo2.ToString("0.##", IdCulture)} kg CO₂ yang kamu hindari.";
    }
}
