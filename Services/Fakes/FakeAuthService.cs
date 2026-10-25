using System.Text.RegularExpressions;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

/// <summary>
/// Fake login: "dinda" (or dinda@mail.ugm.ac.id) / "Rahasia123". Registering the username "taken" returns UsernameTaken.
/// Any registration with a valid format succeeds and signs in. Passwords are fake demo data and never leave memory.
/// </summary>
public sealed partial class FakeAuthService : IAuthService
{
    private readonly FakeContext _ctx;
    private readonly FakeStore _store;
    private readonly ISessionService _session;

    public FakeAuthService(FakeContext ctx, FakeStore store, ISessionService session)
    {
        _ctx = ctx; _store = store; _session = session;
    }

    public async Task<Result<UserSession>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);
        if (_ctx.FailOnce("login")) return _ctx.NetworkFailure<UserSession>();

        var identifier = request.Identifier?.Trim() ?? "";
        var account = _store.Accounts.Values.FirstOrDefault(a =>
            a.UserName.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            a.Email.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (account is null || request.Password != account.Password)
            return Result<UserSession>.Failure(ErrorCode.InvalidCredentials, FakeMessages.InvalidCredentials);

        var session = ToSession(account);
        _session.Start(session);
        return Result<UserSession>.Success(session);
    }

    public async Task<Result<UserSession>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await _ctx.DelayAsync(ct);

        var formatErrors = ValidateRegisterFormat(request);
        if (formatErrors.Count > 0)
            return Result<UserSession>.Failure(ErrorCode.ValidationFailed, FakeMessages.ValidationFailed, formatErrors);

        var userName = request.UserName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (userName.Equals(FakeData.TakenUserName, StringComparison.OrdinalIgnoreCase) || _store.Accounts.ContainsKey(userName))
            return Result<UserSession>.Failure(ErrorCode.UsernameTaken, FakeMessages.UsernameTaken,
                new Dictionary<string, string> { ["UserName"] = FakeMessages.UsernameTaken });

        if (email.StartsWith("taken@", StringComparison.Ordinal) || _store.Accounts.Values.Any(a => a.Email == email))
            return Result<UserSession>.Failure(ErrorCode.EmailTaken, FakeMessages.EmailTaken,
                new Dictionary<string, string> { ["Email"] = FakeMessages.EmailTaken });

        // Errors scenario: a free username still hits one NetworkError first (retry then succeeds).
        if (_ctx.FailOnce("register")) return _ctx.NetworkFailure<UserSession>();

        var account = new FakeAccount(Guid.NewGuid(), userName, email, request.City, request.Password)
        { CreatedAt = _ctx.Clock.UtcNow };
        _store.Accounts[userName] = account;

        var session = ToSession(account);
        _session.Start(session);   // PRD 6.1: automatic login after register
        return Result<UserSession>.Success(session);
    }

    public void Logout() => _session.End();

    public IReadOnlyDictionary<string, string> ValidateRegisterFormat(RegisterRequest request)
    {
        // Rules and messages: SRS FR-1.2.
        var errors = new Dictionary<string, string>();
        var userName = request.UserName?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";
        var password = request.Password ?? "";

        if (userName.Length == 0) errors["UserName"] = "Username wajib diisi";
        else if (userName.Length is < 3 or > 30 || !UserNameRegex().IsMatch(userName))
            errors["UserName"] = "Username 3–30 karakter, hanya huruf, angka, titik, atau garis bawah";

        if (email.Length == 0) errors["Email"] = "Email wajib diisi";
        else if (email.Length > 100 || !EmailRegex().IsMatch(email)) errors["Email"] = "Format email tidak valid";

        if (password.Length is < 8 or > 64 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            errors["Password"] = "Password minimal 8 karakter dan mengandung huruf serta angka";

        if (request.ConfirmPassword != password) errors["ConfirmPassword"] = "Konfirmasi password tidak sama";

        if (string.IsNullOrWhiteSpace(request.City) || !FakeData.Cities.Contains(request.City))
            errors["City"] = "Pilih kota domisili";

        return errors;
    }

    private static UserSession ToSession(FakeAccount a) => new(a.UserId, a.UserName, a.Email, a.City, null);

    [GeneratedRegex("^[A-Za-z0-9_.]+$")]
    private static partial Regex UserNameRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
