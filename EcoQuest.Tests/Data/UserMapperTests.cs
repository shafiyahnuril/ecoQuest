using System;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Rpc;
using Xunit;

namespace EcoQuest.Tests.Data;

public class UserMapperTests
{
    [Fact]
    public void FromRegistered_maps_all_initial_user_fields_and_avatar()
    {
        var userId = Guid.Parse("6f1c2b0e-0000-0000-0000-000000000001");
        var createdAt = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(7));
        var json = new RegisteredUserJson
        {
            UserId = userId,
            UserName = "dinda",
            Email = "dinda@eco.id",
            City = "Yogyakarta",
            AvatarUrl = "avatars/dinda.png",
            CreatedAt = createdAt
        };

        var user = UserMapper.FromRegistered(json);

        Assert.Equal(userId, user.UserId);
        Assert.Equal("dinda", user.UserName);
        Assert.Equal("dinda@eco.id", user.Email);
        Assert.Equal("Yogyakarta", user.City);
        Assert.Equal("avatars/dinda.png", user.AvatarPath);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal(0, user.TotalPoints);
        Assert.Equal(0m, user.TotalCo2SavedKg);
        Assert.Null(user.CurrentLevelId);
        Assert.Equal(0, user.CurrentStreak);
    }

    [Fact]
    public void FromProfile_handles_null_lastreviewdate_and_null_level()
    {
        var json = new ProfileJson
        {
            UserId = Guid.Parse("6f1c2b0e-0000-0000-0000-000000000001"),
            UserName = "dinda",
            Email = "dinda@eco.id",
            City = "Yogyakarta",
            AvatarUrl = null,
            TotalPoints = 0,
            TotalCo2SavedKg = 0m,
            CurrentLevelId = null,
            CurrentStreak = 0,
            CreatedAt = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(7)),
            LastReviewDate = null,
            TotalReviewCount = 0,
            TotalLogCount = 0
        };

        var snap = UserMapper.FromProfile(json);

        Assert.Null(snap.LastReviewDate);
        Assert.Null(snap.User.AvatarPath);
        Assert.Null(snap.User.CurrentLevelId);
        Assert.Equal(0, snap.TotalReviewCount);
        Assert.Equal(0, snap.TotalLogCount);
    }

    [Fact]
    public void FromProfile_maps_populated_stats_and_parses_lastreviewdate()
    {
        var json = new ProfileJson
        {
            UserId = Guid.Parse("6f1c2b0e-0000-0000-0000-000000000001"),
            UserName = "dinda",
            Email = "dinda@eco.id",
            City = "Sleman",
            AvatarUrl = "avatars/dinda.png",
            TotalPoints = 540,
            TotalCo2SavedKg = 22.5000m,
            CurrentLevelId = 3,
            CurrentStreak = 7,
            CreatedAt = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(7)),
            LastReviewDate = "2026-11-12",
            TotalReviewCount = 12,
            TotalLogCount = 45
        };

        var snap = UserMapper.FromProfile(json);

        Assert.Equal("avatars/dinda.png", snap.User.AvatarPath);
        Assert.Equal(540, snap.User.TotalPoints);
        Assert.Equal(22.5000m, snap.User.TotalCo2SavedKg);
        Assert.Equal(3, snap.User.CurrentLevelId);
        Assert.Equal(7, snap.User.CurrentStreak);
        Assert.Equal(new DateOnly(2026, 11, 12), snap.LastReviewDate);
        Assert.Equal(12, snap.TotalReviewCount);
        Assert.Equal(45, snap.TotalLogCount);
    }

    [Fact]
    public void FromLogin_maps_avatarurl_to_AvatarPath_and_preserves_password_hash()
    {
        var userId = Guid.Parse("6f1c2b0e-0000-0000-0000-000000000001");
        var json = new LoginRecordJson
        {
            UserId = userId,
            UserName = "dinda",
            Email = "dinda@eco.id",
            PasswordHash = "$2a$11$hashvalue",
            City = "Yogyakarta",
            AvatarUrl = "avatars/a.png"
        };

        var login = UserMapper.FromLogin(json);

        Assert.Equal(userId, login.UserId);
        Assert.Equal("dinda", login.UserName);
        Assert.Equal("dinda@eco.id", login.Email);
        Assert.Equal("$2a$11$hashvalue", login.PasswordHash);
        Assert.Equal("Yogyakarta", login.City);
        Assert.Equal("avatars/a.png", login.AvatarPath);
    }
}
