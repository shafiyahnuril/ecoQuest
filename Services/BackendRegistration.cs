using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcoQuest.Services;

public static class BackendRegistration
{
    public static IServiceCollection AddEcoQuestBackend(this IServiceCollection s)
    {
        s.TryAddSingleton<IClock, SystemClock>();
        s.TryAddSingleton<IAppPaths, AppPaths>();
        s.AddSingleton<AppLogger>();
        s.AddSingleton<SupabaseClientProvider>();
        s.AddSingleton<RpcClient>();
        s.AddSingleton<CityCatalog>();
        s.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        s.AddSingleton<SupabaseActivityTypeRepository>();
        s.AddSingleton<IActivityTypeRepository>(sp =>
            new CachedActivityTypeRepository(sp.GetRequiredService<SupabaseActivityTypeRepository>()));
        s.AddSingleton<IActivityCategoryRepository, SupabaseActivityCategoryRepository>();
        s.AddSingleton<ILevelRepository, SupabaseLevelRepository>();
        s.AddSingleton<IBadgeRepository, SupabaseBadgeRepository>();
        s.AddSingleton<IUserRepository, SupabaseUserRepository>();
        s.AddSingleton<IDailyReviewRepository, SupabaseDailyReviewRepository>();
        s.AddSingleton<IReferenceDataService, ReferenceDataService>();
        s.AddSingleton<ISessionService, SessionService>();
        s.AddSingleton<IAuthService, AuthService>();
        return s;
    }
}
