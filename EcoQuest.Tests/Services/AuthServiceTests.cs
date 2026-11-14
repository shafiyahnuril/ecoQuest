using System;
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
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EcoQuest.Tests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly CityCatalog _cities = new();

    private AuthService CreateSut(IPasswordHasher? hasher = null)
        => new(_users, hasher ?? _hasher, _session, _cities);

    [Fact]
    public async Task Register_hashes_password_never_sends_plain()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid();
        _users.CheckAvailabilityAsync("budi_1", "budi@example.com", Arg.Any<CancellationToken>())
              .Returns((false, false));
        _hasher.Hash("Rahasia123").Returns("$2a$11$hashedvalue");
        _users.RegisterAsync("budi_1", "budi@example.com", "$2a$11$hashedvalue", "Yogyakarta", Arg.Any<CancellationToken>())
              .Returns(new User(userId, "budi_1", "budi@example.com", "Yogyakarta", DateTimeOffset.UtcNow));

        var res = await sut.RegisterAsync(new RegisterRequest(" budi_1 ", " budi@example.com ", "Rahasia123", "Rahasia123", "Yogyakarta"));

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
        Assert.Equal(userId, res.Value!.UserId);
        Assert.Equal("budi_1", res.Value.UserName);
        await _users.Received(1).RegisterAsync(
            "budi_1",
            "budi@example.com",
            "$2a$11$hashedvalue",
            "Yogyakarta",
            Arg.Any<CancellationToken>());
        await _users.DidNotReceive().RegisterAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "Rahasia123",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        _session.Received(1).Start(Arg.Is<UserSession>(s => s.UserId == userId && s.UserName == "budi_1"));
    }

    [Fact]
    public async Task Register_maps_USERNAME_TAKEN()
    {
        var sut = CreateSut();
        _users.CheckAvailabilityAsync("budi_1", "budi@example.com", Arg.Any<CancellationToken>())
              .Returns((true, false));

        var res = await sut.RegisterAsync(new RegisterRequest("budi_1", "budi@example.com", "Rahasia123", "Rahasia123", "Yogyakarta"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.UsernameTaken, res.Error);
        Assert.Equal(Messages.UsernameTaken, res.Message);
        Assert.Equal(Messages.UsernameTaken, res.FieldErrors["UserName"]);
        await _users.DidNotReceiveWithAnyArgs().RegisterAsync(default!, default!, default!, default!, default);
        _session.DidNotReceiveWithAnyArgs().Start(default!);
    }

    [Fact]
    public async Task Register_maps_EmailTaken_and_does_not_register()
    {
        var sut = CreateSut();
        _users.CheckAvailabilityAsync("budi_1", "budi@example.com", Arg.Any<CancellationToken>())
              .Returns((false, true));

        var res = await sut.RegisterAsync(new RegisterRequest("budi_1", "budi@example.com", "Rahasia123", "Rahasia123", "Yogyakarta"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.EmailTaken, res.Error);
        Assert.Equal(Messages.EmailTaken, res.Message);
        Assert.Equal(Messages.EmailTaken, res.FieldErrors["Email"]);
        await _users.DidNotReceiveWithAnyArgs().RegisterAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task Register_invalid_format_returns_ValidationFailed_without_calling_repository()
    {
        var sut = CreateSut();

        var res = await sut.RegisterAsync(new RegisterRequest("ab", "bukan-email", "short", "mismatch", "Atlantis"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.Equal(Messages.ValidationFailed, res.Message);
        Assert.True(res.FieldErrors.ContainsKey("UserName"));
        Assert.True(res.FieldErrors.ContainsKey("Email"));
        Assert.True(res.FieldErrors.ContainsKey("Password"));
        Assert.True(res.FieldErrors.ContainsKey("ConfirmPassword"));
        Assert.True(res.FieldErrors.ContainsKey("City"));
        await _users.DidNotReceiveWithAnyArgs().CheckAvailabilityAsync(default!, default!, default);
    }

    [Fact]
    public async Task Register_maps_DataAccessException_race_condition()
    {
        var sut = CreateSut();
        _users.CheckAvailabilityAsync("budi_1", "budi@example.com", Arg.Any<CancellationToken>())
              .Returns((false, false));
        _hasher.Hash("Rahasia123").Returns("$2a$11$hashedvalue");
        _users.RegisterAsync("budi_1", "budi@example.com", "$2a$11$hashedvalue", "Yogyakarta", Arg.Any<CancellationToken>())
              .Throws(new DataAccessException(ErrorCode.UsernameTaken, "register"));

        var res = await sut.RegisterAsync(new RegisterRequest("budi_1", "budi@example.com", "Rahasia123", "Rahasia123", "Yogyakarta"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.UsernameTaken, res.Error);
        Assert.Equal(Messages.UsernameTaken, res.FieldErrors["UserName"]);
    }

    [Fact]
    public async Task Login_unknown_identifier_returns_generic_error()
    {
        var sut = CreateSut();
        _users.GetLoginRecordAsync("tidak_ada", Arg.Any<CancellationToken>())
              .Returns((LoginRecord?)null);

        var res = await sut.LoginAsync(new LoginRequest(" tidak_ada ", "Password123"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, res.Error);
        Assert.Equal(Messages.InvalidCredentials, res.Message);
        _hasher.Received(1).Verify("Password123", BCryptPasswordHasher.DummyHash);
        _session.DidNotReceiveWithAnyArgs().Start(default!);
    }

    [Fact]
    public async Task Login_wrong_password_returns_same_generic_error()
    {
        var sut = CreateSut();
        var rec = new LoginRecord(Guid.NewGuid(), "budi_1", "budi@example.com", "$2a$11$stored", "Yogyakarta", null);
        _users.GetLoginRecordAsync("budi_1", Arg.Any<CancellationToken>()).Returns(rec);
        _hasher.Verify("Salah123", "$2a$11$stored").Returns(false);

        var unknownRes = await sut.LoginAsync(new LoginRequest("lain", "Salah123"));
        var wrongPwdRes = await sut.LoginAsync(new LoginRequest("budi_1", "Salah123"));

        Assert.False(wrongPwdRes.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, wrongPwdRes.Error);
        Assert.Equal(Messages.InvalidCredentials, wrongPwdRes.Message);
        Assert.Equal(unknownRes.Error, wrongPwdRes.Error);
        Assert.Equal(unknownRes.Message, wrongPwdRes.Message);
        _session.DidNotReceiveWithAnyArgs().Start(default!);
    }

    [Fact]
    public async Task Login_success_starts_session()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid();
        var rec = new LoginRecord(userId, "budi_1", "budi@example.com", "$2a$11$stored", "Sleman", "avatar.png");
        _users.GetLoginRecordAsync("budi_1", Arg.Any<CancellationToken>()).Returns(rec);
        _hasher.Verify("Benar123", "$2a$11$stored").Returns(true);

        var res = await sut.LoginAsync(new LoginRequest(" budi_1 ", "Benar123"));

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
        Assert.Equal(userId, res.Value!.UserId);
        Assert.Equal("budi_1", res.Value.UserName);
        Assert.Equal("Sleman", res.Value.City);
        Assert.Equal("avatar.png", res.Value.AvatarPath);
        _session.Received(1).Start(res.Value);
    }

    [Fact]
    public async Task Login_blank_input_returns_InvalidCredentials_without_hitting_repository()
    {
        var sut = CreateSut();

        var res = await sut.LoginAsync(new LoginRequest("   ", ""));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, res.Error);
        Assert.Equal(Messages.InvalidCredentials, res.Message);
        await _users.DidNotReceiveWithAnyArgs().GetLoginRecordAsync(default!, default);
    }

    [Fact]
    public async Task Login_maps_DataAccessException_network_error()
    {
        var sut = CreateSut();
        _users.GetLoginRecordAsync("budi_1", Arg.Any<CancellationToken>())
              .Throws(new DataAccessException(ErrorCode.NetworkError, "get_login_record"));

        var res = await sut.LoginAsync(new LoginRequest("budi_1", "Benar123"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, res.Error);
        Assert.Equal(Messages.NetworkError, res.Message);
    }

    [Fact]
    public void Logout_ends_session()
    {
        var sut = CreateSut();
        sut.Logout();
        _session.Received(1).End();
    }

    [Fact]
    public void ValidateRegisterFormat_accepts_valid_request_and_checks_empty_fields()
    {
        var sut = CreateSut();

        var valid = sut.ValidateRegisterFormat(new RegisterRequest("budi_01", "budi@example.com", "Rahasia123", "Rahasia123", "Yogyakarta"));
        Assert.Empty(valid);

        var empty = sut.ValidateRegisterFormat(new RegisterRequest("", "", "weak", "diff", ""));
        Assert.Equal("Username wajib diisi", empty["UserName"]);
        Assert.Equal("Email wajib diisi", empty["Email"]);
        Assert.True(empty.ContainsKey("Password"));
        Assert.True(empty.ContainsKey("ConfirmPassword"));
        Assert.True(empty.ContainsKey("City"));
    }

    [Fact]
    public void BackendRegistration_resolves_AuthService_SessionService_and_CityCatalog()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<AuthService>(provider.GetRequiredService<IAuthService>());
        Assert.IsType<SessionService>(provider.GetRequiredService<ISessionService>());
        Assert.NotNull(provider.GetRequiredService<CityCatalog>());
        Assert.IsType<BCryptPasswordHasher>(provider.GetRequiredService<IPasswordHasher>());
    }
}
