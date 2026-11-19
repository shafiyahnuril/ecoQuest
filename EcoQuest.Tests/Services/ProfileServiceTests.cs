using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using EcoQuest.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EcoQuest.Tests.Services;

public class ProfileServiceTests
{
    private static readonly IReadOnlyList<Level> DefaultLevels =
    [
        new(1, "Eco Starter", 0, 99),
        new(2, "Eco Explorer", 100, 499),
        new(3, "Eco Warrior", 500, 1499),
        new(4, "Eco Champion", 1500, null)
    ];

    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ILevelRepository _levels = Substitute.For<ILevelRepository>();
    private readonly CityCatalog _cities = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly UserSession _activeUser = new(Guid.NewGuid(), "dinda_21", "dinda@mail.ugm.ac.id", "Yogyakarta", "avatars/old.png");

    public ProfileServiceTests()
    {
        _session.RequireUser().Returns(_activeUser);
        _session.Current.Returns(_activeUser);
        _levels.GetAllAsync(Arg.Any<CancellationToken>()).Returns(DefaultLevels);
    }

    private ProfileService CreateSut(IPasswordHasher? hasher = null)
        => new(_session, _users, _levels, _cities, hasher ?? _hasher, _clock);

    [Fact]
    public void GetSupportedCities_returns_catalog_cities()
    {
        var sut = CreateSut();

        var cities = sut.GetSupportedCities();

        Assert.Equal(43, cities.Count);
        Assert.Contains("Yogyakarta", cities);
        Assert.Contains("Semarang", cities);
    }

    [Fact]
    public async Task GetProfileAsync_returns_effective_streak_zero_when_last_review_was_3_days_ago()
    {
        var sut = CreateSut();
        var user = new User(
            _activeUser.UserId,
            _activeUser.UserName,
            _activeUser.Email,
            _activeUser.City,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            _activeUser.AvatarPath,
            totalPoints: 250,
            totalCo2SavedKg: 12.5m,
            currentLevelId: 2,
            currentStreak: 5);
        var threeDaysAgo = _clock.Today.AddDays(-3);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(new UserProfileSnapshot(user, threeDaysAgo, TotalReviewCount: 8, TotalLogCount: 20));

        var res = await sut.GetProfileAsync();

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
        Assert.Equal(0, res.Value!.CurrentStreak);
        Assert.Equal(2, res.Value.Level.LevelId);
        Assert.Equal("Eco Explorer", res.Value.Level.LevelName);
        Assert.Equal("Eco Warrior", res.Value.Level.NextLevelName);
        Assert.Equal(250, res.Value.Level.PointsToNext);
        Assert.Equal(8, res.Value.TotalReviewCount);
        Assert.Equal(20, res.Value.TotalLogCount);
    }

    [Fact]
    public async Task UpdateProfileAsync_rejects_unsupported_city_with_City_field_error()
    {
        var sut = CreateSut();

        var res = await sut.UpdateProfileAsync(new UpdateProfileRequest("Atlantis", null, false));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.Equal("Pilih kota domisili", res.FieldErrors["City"]);
        await _users.DidNotReceiveWithAnyArgs().UpdateProfileAsync(default, default!, default, default);
        _session.DidNotReceiveWithAnyArgs().Update(default!);
    }

    [Fact]
    public async Task UpdateProfileAsync_updates_city_keeps_existing_avatar_and_updates_session()
    {
        var sut = CreateSut();
        var updatedUser = new User(
            _activeUser.UserId,
            _activeUser.UserName,
            _activeUser.Email,
            "Semarang",
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            _activeUser.AvatarPath,
            totalPoints: 50,
            totalCo2SavedKg: 2.5m,
            currentLevelId: 1,
            currentStreak: 2);
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Returns(new UserProfileSnapshot(updatedUser, _clock.Today.AddDays(-1), 2, 5));

        var res = await sut.UpdateProfileAsync(new UpdateProfileRequest("Semarang", "C:\\ignored\\avatar.png", true));

        Assert.True(res.IsSuccess);
        Assert.Equal("Semarang", res.Value!.City);
        Assert.Equal(2, res.Value.CurrentStreak);
        await _users.Received(1).UpdateProfileAsync(_activeUser.UserId, "Semarang", _activeUser.AvatarPath, Arg.Any<CancellationToken>());
        _session.Received(1).Update(Arg.Is<UserSession>(s => s.City == "Semarang" && s.AvatarPath == _activeUser.AvatarPath));
    }

    [Fact]
    public async Task ChangePasswordAsync_wrong_old_password_returns_WrongOldPassword_and_does_not_update()
    {
        var sut = CreateSut();
        var rec = new LoginRecord(_activeUser.UserId, _activeUser.UserName, _activeUser.Email, "$2a$11$stored", _activeUser.City, null);
        _users.GetLoginRecordAsync(_activeUser.UserName, Arg.Any<CancellationToken>()).Returns(rec);
        _hasher.Verify("Salah123", "$2a$11$stored").Returns(false);

        var res = await sut.ChangePasswordAsync(new ChangePasswordRequest("Salah123", "Baru1234", "Baru1234"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.WrongOldPassword, res.Error);
        Assert.Equal(Messages.WrongOldPassword, res.Message);
        Assert.Equal(Messages.WrongOldPassword, res.FieldErrors["OldPassword"]);
        await _users.DidNotReceiveWithAnyArgs().UpdatePasswordHashAsync(default, default!, default);
    }

    [Fact]
    public async Task ChangePasswordAsync_valid_request_hashes_with_bcrypt_and_never_sends_plaintext()
    {
        var realHasher = new BCryptPasswordHasher();
        var oldHash = realHasher.Hash("Lama1234");
        var sut = CreateSut(realHasher);

        var rec = new LoginRecord(_activeUser.UserId, _activeUser.UserName, _activeUser.Email, oldHash, _activeUser.City, null);
        _users.GetLoginRecordAsync(_activeUser.UserName, Arg.Any<CancellationToken>()).Returns(rec);

        string? capturedHash = null;
        await _users.UpdatePasswordHashAsync(
            _activeUser.UserId,
            Arg.Do<string>(h => capturedHash = h),
            Arg.Any<CancellationToken>());

        var res = await sut.ChangePasswordAsync(new ChangePasswordRequest("Lama1234", "Baru1234", "Baru1234"));

        Assert.True(res.IsSuccess);
        Assert.NotNull(capturedHash);
        Assert.StartsWith("$2", capturedHash!, StringComparison.Ordinal);
        Assert.NotEqual("Baru1234", capturedHash);
        Assert.True(realHasher.Verify("Baru1234", capturedHash!));
    }

    [Fact]
    public async Task ChangePasswordAsync_invalid_new_password_and_mismatch_fail_without_calling_repo()
    {
        var sut = CreateSut();

        var res = await sut.ChangePasswordAsync(new ChangePasswordRequest("Lama1234", "lemah", "beda"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.True(res.FieldErrors.ContainsKey("NewPassword"));
        Assert.Equal("Konfirmasi password tidak sama", res.FieldErrors["ConfirmNewPassword"]);
        await _users.DidNotReceiveWithAnyArgs().GetLoginRecordAsync(default!, default);
    }

    [Fact]
    public async Task GetProfileAsync_maps_DataAccessException_to_Failure()
    {
        var sut = CreateSut();
        _users.GetProfileAsync(_activeUser.UserId, Arg.Any<CancellationToken>())
              .Throws(new DataAccessException(ErrorCode.NetworkError, "app_get_profile"));

        var res = await sut.GetProfileAsync();

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, res.Error);
        Assert.Equal(Messages.NetworkError, res.Message);
    }

    [Fact]
    public void BackendRegistration_resolves_IProfileService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        Assert.IsType<ProfileService>(sp.GetRequiredService<IProfileService>());
    }
}
