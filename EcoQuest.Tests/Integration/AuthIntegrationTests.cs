using System;
using System.Threading.Tasks;
using DotNetEnv;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthIntegrationTests
{
    [IntegrationFact]
    public async Task Register_login_and_duplicate_check_work_against_real_supabase()
    {
        Env.TraversePath().Load();
        var config = AppConfig.FromEnvironment();
        Assert.False(string.IsNullOrWhiteSpace(config.SupabaseUrl));
        Assert.False(string.IsNullOrWhiteSpace(config.SupabaseKey));

        var provider = new SupabaseClientProvider(config);
        await provider.InitializeAsync();

        var rpc = new RpcClient(provider);
        var users = new SupabaseUserRepository(rpc);
        var hasher = new BCryptPasswordHasher();
        var session = new SessionService();
        var cities = new CityCatalog();
        var auth = new AuthService(users, hasher, session, cities);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"it_{suffix}";
        var email = $"{username}@test.local";

        var reg = await auth.RegisterAsync(new RegisterRequest(username, email, "Rahasia123", "Rahasia123", "Yogyakarta"));
        Assert.True(reg.IsSuccess, $"Register failed: {reg.Error} - {reg.Message}");
        Assert.True(session.IsAuthenticated);
        Assert.Equal(username, reg.Value!.UserName);

        auth.Logout();
        Assert.False(session.IsAuthenticated);

        var loginByUsername = await auth.LoginAsync(new LoginRequest(username, "Rahasia123"));
        Assert.True(loginByUsername.IsSuccess, $"Login by username failed: {loginByUsername.Error} - {loginByUsername.Message}");

        auth.Logout();

        var loginByEmail = await auth.LoginAsync(new LoginRequest(email, "Rahasia123"));
        Assert.True(loginByEmail.IsSuccess, $"Login by email failed: {loginByEmail.Error} - {loginByEmail.Message}");

        auth.Logout();

        var wrongPwd = await auth.LoginAsync(new LoginRequest(username, "Salah123"));
        Assert.False(wrongPwd.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, wrongPwd.Error);
        Assert.Equal(Messages.InvalidCredentials, wrongPwd.Message);

        var unknownUser = await auth.LoginAsync(new LoginRequest($"missing_{suffix}", "Salah123"));
        Assert.False(unknownUser.IsSuccess);
        Assert.Equal(wrongPwd.Error, unknownUser.Error);
        Assert.Equal(wrongPwd.Message, unknownUser.Message);

        var dupUsername = await auth.RegisterAsync(new RegisterRequest(username, $"other_{suffix}@test.local", "Rahasia123", "Rahasia123", "Yogyakarta"));
        Assert.False(dupUsername.IsSuccess);
        Assert.Equal(ErrorCode.UsernameTaken, dupUsername.Error);
    }
}
