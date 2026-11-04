using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcoQuest.Services.Fakes;

public static class FakesRegistration
{
    /// <summary>True when ECOQUEST_USE_FAKES is "true"/"1" in the process environment or in the nearest .env (UIR-09).</summary>
    public static bool IsEnabled() => FakeSettings.IsEnabled();

    /// <summary>Registers a fake for every interface in Core/Contracts/Services (ADR-018). Needs no .env and no Supabase key.</summary>
    public static IServiceCollection AddEcoQuestFakes(this IServiceCollection services) =>
        services.AddEcoQuestFakes(FakeSettings.ReadScenario());

    public static IServiceCollection AddEcoQuestFakes(this IServiceCollection services, FakeScenario scenario)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton(sp => new FakeContext(scenario, sp.GetRequiredService<IClock>()));
        services.AddSingleton<FakeStore>();

        services.AddSingleton<ISessionService, FakeSessionService>();
        services.AddSingleton<IAuthService, FakeAuthService>();
        services.AddSingleton<IProfileService, FakeProfileService>();
        services.AddSingleton<IReferenceDataService, FakeReferenceDataService>();
        services.AddSingleton<IEveningReviewService, FakeEveningReviewService>();
        services.AddSingleton<IDashboardService, FakeDashboardService>();
        services.AddSingleton<IHistoryService, FakeHistoryService>();
        services.AddSingleton<IWeatherSuggestionService, FakeWeatherSuggestionService>();
        services.AddSingleton<IGamificationService, FakeGamificationService>();
        services.AddSingleton<ILeaderboardService, FakeLeaderboardService>();
        services.AddSingleton<IPhotoStorageService, FakePhotoStorageService>();
        return services;
    }
}
