
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Repositories;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http;

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
        s.AddSingleton<IProfileService, ProfileService>();
        s.AddSingleton<IGamificationService, GamificationService>();
        s.AddSingleton<PhotoStorageService>();
        s.AddSingleton<IPhotoStorageService>(sp => sp.GetRequiredService<PhotoStorageService>());
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
        s.AddSingleton<EcoQuest.Core.Validation.ActivityInputValidator>();
        s.AddSingleton<IDashboardService, DashboardService>();
        s.AddSingleton<DtoFactory>();
        s.AddSingleton<IEveningReviewService, EveningReviewService>();
        s.AddSingleton<IHistoryService, HistoryService>();
        s.AddSingleton(sp => new HttpClient { BaseAddress = new Uri("https://dataservice.accuweather.com/"), Timeout = TimeSpan.FromSeconds(5) });
        s.AddSingleton<IAccuWeatherClient, AccuWeatherClient>();
        s.AddSingleton<LocationKeyCache>();
        return s;
    }
}
