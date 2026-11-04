using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

public sealed class FakeProfileService : IProfileService
{
    private readonly FakeContext _ctx;
    private readonly FakeStore _store;
    private readonly ISessionService _session;
    private string? _avatarPath;

    public FakeProfileService(FakeContext ctx, FakeStore store, ISessionService session)
    {
        _ctx = ctx; _store = store; _session = session;
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("profile")) return _ctx.NetworkFailure<UserProfileDto>();
        return CurrentAccount() is { } account
            ? Result<UserProfileDto>.Success(ToDto(account))
            : Result<UserProfileDto>.Failure(ErrorCode.NotAuthenticated, FakeMessages.NotAuthenticated);
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (CurrentAccount() is not { } account)
            return Result<UserProfileDto>.Failure(ErrorCode.NotAuthenticated, FakeMessages.NotAuthenticated);

        if (!FakeData.Cities.Contains(request.City))
            return Result<UserProfileDto>.Failure(ErrorCode.ValidationFailed, FakeMessages.ValidationFailed,
                new Dictionary<string, string> { ["City"] = "Pilih kota domisili" });

        account.City = request.City;
        if (request.RemoveAvatar) _avatarPath = null;
        else if (!string.IsNullOrWhiteSpace(request.NewAvatarSourcePath))
            _avatarPath = "avatars/" + Path.GetFileName(request.NewAvatarSourcePath);   // fake: no file copy

        _session.Update(new UserSession(account.UserId, account.UserName, account.Email, account.City, _avatarPath));
        return Result<UserProfileDto>.Success(ToDto(account));
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (CurrentAccount() is not { } account)
            return Result.Failure(ErrorCode.NotAuthenticated, FakeMessages.NotAuthenticated);

        if (request.OldPassword != account.Password)
            return Result.Failure(ErrorCode.WrongOldPassword, FakeMessages.WrongOldPassword,
                new Dictionary<string, string> { ["OldPassword"] = FakeMessages.WrongOldPassword });

        var errors = new Dictionary<string, string>();
        var password = request.NewPassword ?? "";
        if (password.Length is < 8 or > 64 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            errors["NewPassword"] = "Password minimal 8 karakter dan mengandung huruf serta angka";
        if (request.ConfirmNewPassword != password) errors["ConfirmNewPassword"] = "Konfirmasi password tidak sama";
        if (errors.Count > 0) return Result.Failure(ErrorCode.ValidationFailed, FakeMessages.ValidationFailed, errors);

        account.Password = password;
        return Result.Success();
    }

    public IReadOnlyList<string> GetSupportedCities() => FakeData.Cities;

    private FakeAccount? CurrentAccount() =>
        _session.Current is { } user && _store.Accounts.TryGetValue(user.UserName, out var account) ? account : null;

    private UserProfileDto ToDto(FakeAccount a) => new(
        a.UserId, a.UserName, a.Email, a.City, _avatarPath,
        _store.TotalPoints, _store.TotalCo2Kg, _store.CurrentStreak, _store.LevelProgress(),
        _store.LastReviewDate, _store.TotalReviewCount, _store.TotalLogCount, a.CreatedAt);
}
