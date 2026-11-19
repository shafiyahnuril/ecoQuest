using System;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcoQuest.Tests.Data;

public class BadgeRepositoryMappingTests
{
    [Fact]
    public void ToDomain_maps_UserBadgeJson_with_supplied_user_id()
    {
        var userId = Guid.Parse("6f1c2b0e-0000-0000-0000-000000000001");
        var earnedAt = new DateTimeOffset(2026, 11, 13, 21, 5, 0, TimeSpan.FromHours(7));
        var json = new UserBadgeJson { BadgeId = 3, EarnedAt = earnedAt };

        var domain = RecordMapper.ToDomain(json, userId);

        Assert.Equal(userId, domain.UserId);
        Assert.Equal(3, domain.BadgeId);
        Assert.Equal(earnedAt, domain.EarnedAt);
    }

    [Fact]
    public void BackendRegistration_resolves_level_and_badge_repositories_and_rpc_client()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "anon-key", null, UseFakes: false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(sp.GetRequiredService<RpcClient>());
        Assert.IsType<SupabaseLevelRepository>(sp.GetRequiredService<ILevelRepository>());
        Assert.IsType<SupabaseBadgeRepository>(sp.GetRequiredService<IBadgeRepository>());
    }
}
