using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EcoQuest.Services;

public static class BackendRegistration
{
    public static IServiceCollection AddEcoQuestBackend(this IServiceCollection s)
    {
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
        s.AddSingleton<IReferenceDataService, ReferenceDataService>();
        s.AddSingleton<ISessionService, SessionService>();
        s.AddSingleton<IAuthService, AuthService>();
        s.AddSingleton<EcoQuest.Core.Calculation.IImpactCalculator,
            EcoQuest.Core.Calculation.DistanceImpactCalculator>();

        s.AddSingleton<EcoQuest.Core.Calculation.IImpactCalculator,
            EcoQuest.Core.Calculation.DurationImpactCalculator>();

        s.AddSingleton<EcoQuest.Core.Calculation.IImpactCalculator,
            EcoQuest.Core.Calculation.CountImpactCalculator>();

        s.AddSingleton<EcoQuest.Core.Calculation.IImpactCalculator,
            EcoQuest.Core.Calculation.ToggleImpactCalculator>();

        s.AddSingleton<EcoQuest.Core.Calculation.IImpactCalculatorFactory,
            EcoQuest.Core.Calculation.ImpactCalculatorFactory>();

        s.AddSingleton<EcoQuest.Core.Calculation.IImpactCalculationService,
            EcoQuest.Core.Calculation.ImpactCalculationService>();
        return s;
    }
}
