using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Errors;
using EcoQuest.Infrastructure;

namespace EcoQuest.Services;

/// <summary>
/// Layanan autentikasi (registrasi, login, logout, dan validasi format) menggunakan BCrypt dan sesi lokal.
/// </summary>
public sealed class AuthService : IAuthService
{
    private static readonly Regex UserNameRx = new("^[A-Za-z0-9_.]{3,30}$", RegexOptions.Compiled);
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PwdRx = new(@"^(?=.*[A-Za-z])(?=.*\d).{8,64}$", RegexOptions.Compiled);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionService _session;
    private readonly CityCatalog _cities;

    public AuthService(IUserRepository users, IPasswordHasher hasher, ISessionService session, CityCatalog cities)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _cities = cities ?? throw new ArgumentNullException(nameof(cities));
    }

    public IReadOnlyDictionary<string, string> ValidateRegisterFormat(RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string>();
        var userName = request.UserName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(userName))
            errors["UserName"] = "Username wajib diisi";
        else if (!UserNameRx.IsMatch(userName))
            errors["UserName"] = "Username 3–30 karakter, hanya huruf, angka, titik, atau garis bawah";

        if (string.IsNullOrEmpty(email))
            errors["Email"] = "Email wajib diisi";
        else if (email.Length > 100 || !EmailRx.IsMatch(email))
            errors["Email"] = "Format email tidak valid";

        if (string.IsNullOrEmpty(request.Password) || !PwdRx.IsMatch(request.Password))
            errors["Password"] = "Password minimal 8 karakter dan mengandung huruf serta angka";

        if (request.ConfirmPassword != request.Password)
            errors["ConfirmPassword"] = "Konfirmasi password tidak sama";

        if (string.IsNullOrWhiteSpace(request.City) || !_cities.IsSupported(request.City))
            errors["City"] = "Pilih kota domisili";

        return errors;
    }

    public async Task<Result<UserSession>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var formatErrors = ValidateRegisterFormat(request);
        if (formatErrors.Count > 0)
            return Result<UserSession>.Failure(ErrorCode.ValidationFailed, Messages.ValidationFailed, formatErrors);

        var userName = request.UserName.Trim();
        var email = request.Email.Trim();

        try
        {
            var (usernameTaken, emailTaken) = await _users.CheckAvailabilityAsync(userName, email, ct);
            if (usernameTaken || emailTaken)
                return BuildConflictFailure(usernameTaken, emailTaken);

            var hash = _hasher.Hash(request.Password);
            var user = await _users.RegisterAsync(userName, email, hash, request.City, ct);

            var userSession = new UserSession(user.UserId, user.UserName, user.Email, user.City, user.AvatarPath);
            _session.Start(userSession);
            return Result<UserSession>.Success(userSession);
        }
        catch (DataAccessException ex) when (ex.Code is ErrorCode.UsernameTaken or ErrorCode.EmailTaken)
        {
            return BuildConflictFailure(ex.Code == ErrorCode.UsernameTaken, ex.Code == ErrorCode.EmailTaken);
        }
        catch (DataAccessException ex)
        {
            return Result<UserSession>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    public async Task<Result<UserSession>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrEmpty(request.Password))
            return Result<UserSession>.Failure(ErrorCode.InvalidCredentials, Messages.InvalidCredentials);

        try
        {
            var rec = await _users.GetLoginRecordAsync(request.Identifier.Trim(), ct);
            if (rec is null)
            {
                // Verifikasi terhadap dummy hash menyamakan durasi respons untuk mencegah user enumeration.
                _hasher.Verify(request.Password, BCryptPasswordHasher.DummyHash);
                return Result<UserSession>.Failure(ErrorCode.InvalidCredentials, Messages.InvalidCredentials);
            }

            if (!_hasher.Verify(request.Password, rec.PasswordHash))
                return Result<UserSession>.Failure(ErrorCode.InvalidCredentials, Messages.InvalidCredentials);

            var userSession = new UserSession(rec.UserId, rec.UserName, rec.Email, rec.City, rec.AvatarPath);
            _session.Start(userSession);
            return Result<UserSession>.Success(userSession);
        }
        catch (DataAccessException ex)
        {
            return Result<UserSession>.Failure(ex.Code, Messages.For(ex.Code));
        }
    }

    public void Logout() => _session.End();

    private static Result<UserSession> BuildConflictFailure(bool usernameTaken, bool emailTaken)
    {
        var fieldErrors = new Dictionary<string, string>();
        if (usernameTaken) fieldErrors["UserName"] = Messages.UsernameTaken;
        if (emailTaken) fieldErrors["Email"] = Messages.EmailTaken;

        var code = usernameTaken ? ErrorCode.UsernameTaken : ErrorCode.EmailTaken;
        return Result<UserSession>.Failure(code, Messages.For(code), fieldErrors);
    }
}
