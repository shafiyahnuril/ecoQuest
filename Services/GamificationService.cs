
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Rules;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;

namespace EcoQuest.Services;

public sealed class GamificationService : IGamificationService
{
    private readonly ISessionService _session;
    private readonly IUserRepository _users;
    private readonly ILevelRepository _levels;
    private readonly IBadgeRepository _badges;
    private readonly IClock _clock;

    public GamificationService(
        ISessionService session,
        IUserRepository users,
        ILevelRepository levels,
        IBadgeRepository badges,
        IClock clock)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _levels = levels ?? throw new ArgumentNullException(nameof(levels));
        _badges = badges ?? throw new ArgumentNullException(nameof(badges));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<LevelProgressDto>> GetLevelProgressAsync(
        CancellationToken ct = default)
    {
        var current = _session.RequireUser();

        try
        {
            var profile = await _users.GetProfileAsync(current.UserId, ct);

            if (profile is null)
            {
                return Result<LevelProgressDto>.Failure(
                    ErrorCode.NotFound,
                    Messages.Unknown);
            }

            var levels = await _levels.GetAllAsync(ct);

            var progress = LevelResolver.Progress(
                profile.User.TotalPoints,
                levels);

            var dto = LevelProgressFactory.ToDto(progress);

            return Result<LevelProgressDto>.Success(dto);
        }
        catch (DataAccessException ex)
        {
            return Result<LevelProgressDto>.Failure(
                ex.Code,
                Messages.For(ex.Code));
        }
    }

    public async Task<Result<IReadOnlyList<BadgeDto>>> GetBadgesAsync(
        CancellationToken ct = default)
    {
        var current = _session.RequireUser();

        try
        {
            var profile = await _users.GetProfileAsync(current.UserId, ct);

            if (profile is null)
            {
                return Result<IReadOnlyList<BadgeDto>>.Failure(
                    ErrorCode.NotFound,
                    Messages.Unknown);
            }

            var allBadges = await _badges.GetAllAsync(ct);
            var userBadges = await _badges.GetUserBadgesAsync(
                current.UserId,
                ct);

            var effectiveStreak = StreakCalculator.Effective(
                profile.User.CurrentStreak,
                profile.LastReviewDate,
                _clock.Today);

            var context = new BadgeContext(
                profile.TotalLogCount,
                effectiveStreak,
                profile.User.TotalCo2SavedKg,
                profile.User.TotalPoints);

            var earnedById = userBadges
                .GroupBy(badge => badge.BadgeId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(badge => badge.EarnedAt));

            var result = allBadges
                .Select(badge =>
                {
                    var isEarned = earnedById.TryGetValue(
                        badge.BadgeId,
                        out var earnedAt);

                    decimal currentValue = badge.CriteriaType switch
                    {
                        CriteriaType.ActivityCount =>
                            context.LogCount,

                        CriteriaType.PointsThreshold =>
                            context.TotalPoints,

                        CriteriaType.StreakDays =>
                            context.Streak,

                        CriteriaType.CO2SavedThreshold =>
                            context.TotalCo2,

                        _ => 0m
                    };

                    return new BadgeDto(
                        badge.BadgeId,
                        badge.Name,
                        badge.Description,
                        badge.CriteriaType,
                        badge.CriteriaValue,
                        isEarned,
                        isEarned ? earnedAt : null,
                        currentValue);
                })
                .ToList();

            return Result<IReadOnlyList<BadgeDto>>.Success(result);
        }
        catch (DataAccessException ex)
        {
            return Result<IReadOnlyList<BadgeDto>>.Failure(
                ex.Code,
                Messages.For(ex.Code));
        }
    }
}
