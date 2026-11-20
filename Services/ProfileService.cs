using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Rules;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;

namespace EcoQuest.Services;

/// <summary>
/// Layanan profil pengguna: membaca profil beserta progres level/streak efektif, mengubah kota domisili, dan ganti password.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private static readonly Regex PwdRx = new(@"^(?=.*[A-Za-z])(?=.*\d).{8,64}$", RegexOptions.Compiled);

    private readonly ISessionService _session;
    private readonly IUserRepository _users;
    private readonly ILevelRepository _levels;
    private readonly CityCatalog _cities;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly PhotoStorageService _photos;

    public ProfileService(
        ISessionService session,
        IUserRepository users,
        ILevelRepository levels,
        CityCatalog cities,
        IPasswordHasher hasher,
        IClock clock,
        PhotoStorageService photos)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _levels = levels ?? throw new ArgumentNullException(nameof(levels));
        _cities = cities ?? throw new ArgumentNullException(nameof(cities));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _photos = photos ?? throw new ArgumentNullException(nameof(photos));
    }

    public IReadOnlyList<string> GetSupportedCities() => _cities.Cities;

    public async Task<Result<UserProfileDto>> GetProfileAsync(CancellationToken ct = default)
    {
        var current = _session.RequireUser();
        try
        {
            var snapshot = await _users.GetProfileAsync(current.UserId, ct);
            if (snapshot is null)
                return Result<UserProfileDto>.Failure(ErrorCode.NotFound, Messages.Unknown);

            var allLevels = await _levels.GetAllAsync(ct);
            var levelDto = LevelProgressFactory.ToDto(LevelResolver.Progress(snapshot.User.TotalPoints, allLevels));
            var effectiveStreak = StreakCalculator.Effective(
                snapshot.User.CurrentStreak,
                snapshot.LastReviewDate,
                _clock.Today);

            var dto = new UserProfileDto(
                snapshot.User.UserId,
                snapshot.User.UserName,
                snapshot.User.Email,
                snapshot.User.City,
                snapshot.User.AvatarPath,
                snapshot.User.TotalPoints,
                snapshot.User.TotalCo2SavedKg,
                effectiveStreak,
                levelDto,
                snapshot.LastReviewDate,
                snapshot.TotalReviewCount,
                snapshot.TotalLogCount,
                snapshot.User.CreatedAt);

            return Result<UserProfileDto>.Success(dto);
        }
        catch (DataAccessException ex)
        {
            return Result<UserProfileDto>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = _session.RequireUser();
        if (string.IsNullOrWhiteSpace(request.City) || !_cities.IsSupported(request.City))
        {
            return Result<UserProfileDto>.Failure(
                ErrorCode.ValidationFailed,
                Messages.ValidationFailed,
                new Dictionary<string, string> { ["City"] = "Pilih kota domisili" });
        }

        var oldAvatar = current.AvatarPath;
        var newAvatar = oldAvatar;

        if (request.RemoveAvatar)
        {
            newAvatar = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.NewAvatarSourcePath))
        {
            var check = _photos.ValidatePhoto(request.NewAvatarSourcePath);
            if (!check.IsSuccess)
            {
                var errMsg = check.Message ?? Messages.FileError;
                return Result<UserProfileDto>.Failure(
                    check.Error,
                    errMsg,
                    new Dictionary<string, string> { ["Avatar"] = errMsg });
            }
        }

        try
        {
            if (!request.RemoveAvatar && !string.IsNullOrWhiteSpace(request.NewAvatarSourcePath))
                newAvatar = _photos.CopyAvatar(request.NewAvatarSourcePath, current.UserId);

            await _users.UpdateProfileAsync(current.UserId, request.City, newAvatar, ct);

            if (request.RemoveAvatar && !string.IsNullOrWhiteSpace(oldAvatar))
                _photos.TryDelete(oldAvatar);

            _session.Update(current with { City = request.City, AvatarPath = newAvatar });
            return await GetProfileAsync(ct);
        }
        catch (DataAccessException ex)
        {
            return Result<UserProfileDto>.Failure(ex.Code, Messages.For(ex.Code));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<UserProfileDto>.Failure(ErrorCode.FileError, Messages.FileError);
        }
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = _session.RequireUser();
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(request.NewPassword) || !PwdRx.IsMatch(request.NewPassword))
            errors["NewPassword"] = "Password minimal 8 karakter dan mengandung huruf serta angka";
        if (request.ConfirmNewPassword != request.NewPassword)
            errors["ConfirmNewPassword"] = "Konfirmasi password tidak sama";
        if (errors.Count > 0)
            return Result.Failure(ErrorCode.ValidationFailed, Messages.ValidationFailed, errors);

        try
        {
            var loginRecord = await _users.GetLoginRecordAsync(current.UserName, ct);
            if (loginRecord is null)
                return Result.Failure(ErrorCode.NotFound, Messages.Unknown);

            if (string.IsNullOrEmpty(request.OldPassword) || !_hasher.Verify(request.OldPassword, loginRecord.PasswordHash))
            {
                return Result.Failure(
                    ErrorCode.WrongOldPassword,
                    Messages.WrongOldPassword,
                    new Dictionary<string, string> { ["OldPassword"] = Messages.WrongOldPassword });
            }

            var newHash = _hasher.Hash(request.NewPassword);
            await _users.UpdatePasswordHashAsync(current.UserId, newHash, ct);
            return Result.Success();
        }
        catch (DataAccessException ex)
        {
            return Result.Failure(ex.Code, Messages.For(ex.Code));
        }
    }
}
