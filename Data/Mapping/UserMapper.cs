using System;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Rpc;

namespace EcoQuest.Data.Mapping;

public static class UserMapper
{
    public static User FromRegistered(RegisteredUserJson j) => new(
        j.UserId,
        j.UserName,
        j.Email,
        j.City,
        j.CreatedAt,
        j.AvatarUrl);

    public static LoginRecord FromLogin(LoginRecordJson j) => new(
        j.UserId,
        j.UserName,
        j.Email,
        j.PasswordHash,
        j.City,
        j.AvatarUrl);

    public static UserProfileSnapshot FromProfile(ProfileJson j)
    {
        var user = new User(
            j.UserId,
            j.UserName,
            j.Email,
            j.City,
            j.CreatedAt,
            j.AvatarUrl,
            j.TotalPoints,
            j.TotalCo2SavedKg,
            j.CurrentLevelId,
            j.CurrentStreak);

        DateOnly? lastReview = string.IsNullOrWhiteSpace(j.LastReviewDate)
            ? null
            : RecordMapper.ParseDate(j.LastReviewDate);

        return new UserProfileSnapshot(user, lastReview, j.TotalReviewCount, j.TotalLogCount);
    }
}
